using CsvHelper;
using CsvHelper.Configuration;
using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Handlers;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace OFM.Infrastructure.WebAPI.Services.Processes.DataImports;

public class P705ECERCertificateProvider(IOptionsSnapshot<ExternalServices> ECERSettings,ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly TimeProvider _timeProvider = timeProvider;
    private ProcessParameter? _processParams;
    private ProcessData? _data;
    private int certificateStatus = 0;  // 1 Passed, 0 Failed
    private string CertificateNumber = string.Empty;
    private string reportID = string.Empty;
    private readonly ECERSettings _ECERSettings = ECERSettings.Value.ECERApi;
    public Int16 ProcessId => Setup.Process.DataImports.ProcessECERCertificatesId;
    public string ProcessName => Setup.Process.DataImports.ProcessECERCertificatesName;
   

    public string RequestUri
    {
        get
        {
            // fetch xml doesn't support binary data type
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                        <fetch>
                          <entity name=""ofm_data_import"">
                            <attribute name=""createdon"" />
                            <attribute name=""ofm_data_file"" />
                            <attribute name=""ofm_data_importid"" />
                            <attribute name=""ofm_import_type"" />
                            <attribute name=""ofm_message"" />
                            <filter>
                              <condition attribute=""ofm_data_importid"" operator=""eq"" value=""{_processParams.DataImportId}"" />
                            </filter>
                          </entity>
                        </fetch>";
            var requestUri = $"""                                
                                ofm_data_imports({_processParams.DataImportId})/ofm_data_file
                                """;
            return requestUri;
        }
    }
    private string providerEmployeeRequestUri
    {
        // Provider Employee of Applicaiton
        get
        {
            // Application StatusReason 3 Submitted, 4 In Review, 5, Awaiting Provider
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                    <fetch>
                      <entity name=""ofm_provider_employee"">
                        <attribute name=""ofm_application"" />
                        <attribute name=""ofm_employee_type"" />
                        <attribute name=""ofm_caption"" />
                        <attribute name=""ofm_certificate_number"" />
                        <attribute name=""ofm_certificate_status"" />
                        <attribute name=""statecode"" />
                        <filter>
                          <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                        </filter>
                        <link-entity name=""ofm_application"" from=""ofm_applicationid"" to=""ofm_application"" link-type=""inner"" alias=""app"">
                          <filter>
                            <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                            <condition attribute=""statuscode"" operator=""in"">
                              <value>5</value>
                              <value>3</value>
                              <value>4</value>
                            </condition>
                          </filter>
                        </link-entity>
                    <link-entity name=""ofm_employee_certificate"" from=""ofm_certificate_number"" to=""ofm_certificate_number"" link-type=""outer"" alias=""ofm_employee_certificate"">
                    <attribute name=""ofm_certificate_level"" />
                  <attribute name=""ofm_certificate_number"" />
                   <filter type=""and"">
                 <condition attribute=""ofm_effective_date"" operator=""le"" value=""{DateTime.Today}"" />
                  <condition attribute=""ofm_expiry_date"" operator=""ge"" value=""{DateTime.Today}"" />
                        </filter>
                       </link-entity>
                      </entity>
                    </fetch>";
            var requestUri = $"""
                            ofm_provider_employees?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    public string EmployeeCertificateRequestUri
    {
        get
        {
            // 5000 records limits every query
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                <fetch>
                  <entity name=""ofm_employee_certificate"">
                    <attribute name=""ofm_expiry_date"" />
                    <attribute name=""ofm_certificate_number"" />
                    <attribute name=""ofm_certificate_level"" />
                    <attribute name=""ofm_effective_date"" />
                    <attribute name=""ofm_first_name"" />
                    <attribute name=""ofm_is_active"" />
                    <attribute name=""ofm_last_name"" />
                    <attribute name=""ofm_middle_name"" />
                    <filter>
                      <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                    </filter>
                    <order attribute=""ofm_certificate_number"" />
                  </entity>
                </fetch>";
            var requestUri = $"""                                
                                ofm_employee_certificates?$select=ofm_expiry_date,ofm_certificate_number,ofm_certificate_level,ofm_effective_date,ofm_first_name,ofm_is_active,ofm_last_name,ofm_middle_name&$orderby=ofm_certificate_number asc&$filter=(statecode eq 0)
                                """;
            return requestUri;
        }
    }

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P705ECERCertificateProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query the requests with the server error {responseBody}", responseBody);

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {

                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString());
        }

        return await Task.FromResult(_data);


    }

    public async Task<List<JsonNode>> FetchAllRecordsFromCRMAsync(string requestUri)
    {
        _logger.LogDebug(CustomLogEvent.Process, "Getting records with query {requestUri}", requestUri.CleanLog());
        var allRecords = new List<JsonNode>();  // List to accumulate all records
        string nextPageLink = requestUri;  // Initial request URI
        do
        {
            // 5000 is limit number can retrieve from crm
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, nextPageLink, false, 5000, isProcess: false);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query records with server error {responseBody}", responseBody.CleanLog());
                var returnJsonNodeList = new List<JsonNode>();
                returnJsonNodeList.Add(responseBody);
                return returnJsonNodeList;
                // null;
            }
            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();
            JsonNode currentBatch = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No more records found with query {nextPageLink}", nextPageLink.CleanLog());
                    break;  // Exit the loop if no more records
                }
                currentBatch = currentValue!;
                allRecords.AddRange(currentBatch.AsArray());  // Add current batch to the list
            }
            _logger.LogDebug(CustomLogEvent.Process, "Fetched {batchSize} records. Total records so far: {totalRecords}", currentBatch.AsArray().Count, allRecords.Count);

            // Check if there's a next link in the response for pagination
            nextPageLink = null;
            if (jsonObject?.TryGetPropertyValue("@odata.nextLink", out var nextLinkValue) == true)
            {
                nextPageLink = nextLinkValue.ToString();
            }
        }
        while (!string.IsNullOrEmpty(nextPageLink));

        _logger.LogDebug(CustomLogEvent.Process, "Total records fetched: {totalRecords}", allRecords.Count);
        return allRecords;
    }

    private bool validateCertStatus(CertificationDetail cert, string certificate_level)
    {
        DateTime today = DateTime.Today;

        if (today >= cert.effectivedate && today <= cert.expirydate && cert.certificatelevel.ToLower().Contains(certificate_level.ToLower().ToString()))
        {
            return true;
        }
        return false;
    }
    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _logger.LogInformation(CustomLogEvent.Process, "Beging to P705 Data process");
        _processParams = processParams;
        var startTime = _timeProvider.GetTimestamp();
        string dataImportMessages = string.Empty;
        string upsertMessages = string.Empty;
        string deactiveMessages = string.Empty;
        bool upsertSucessfully = false;
        bool deactiveSucessfully = false;
        var EmployeeType = new Dictionary<int, string> { { 1, "ECE" }, { 2, "ECEA" }, {3,"SNE" } };
       try
        {
            #region Connect with ECER API 

            using (var client = new HttpClient())
            {
                // ===========================
                // Step 1. Retrieve an access token
                // ===========================
                var tokenUrl = _ECERSettings.InterfaceURL;
                var tokenRequestBody = "grant_type=client_credentials&client_id="+_ECERSettings.ClientId+"&client_secret="+_ECERSettings.ClientSecret;

                var tokenResult = await ECERAPIHandler.GetTokenAsync(tokenUrl, tokenRequestBody);

                // Set the authorization header for subsequent API calls
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult);

                // ===========================
                // Step 2. Retrieve certification files list
                // ===========================
                var filesUrl = string.Concat(_ECERSettings.ECERURL, "s");
                HttpResponseMessage filesResponse = await client.GetAsync(filesUrl);
                filesResponse.EnsureSuccessStatusCode();

                string filesContent = await filesResponse.Content.ReadAsStringAsync();
                List<CertificationFile> certificationFiles = JsonConvert.DeserializeObject<List<CertificationFile>>(filesContent);

                if (certificationFiles == null || certificationFiles.Count == 0)
                {
                    return ProcessResult.Failure(ProcessId, new String[] { "ECER Certificate is not found" }, 0, 0).SimpleProcessResult;

                }

                // ===========================
                // Step 3. Download certification details from the first file
                // ===========================
                string firstFileId = certificationFiles[0].id;
                Console.WriteLine("Retrieving details for file id: " + firstFileId);

                var downloadUrl = string.Concat(_ECERSettings.ECERURL, "/download/", firstFileId);// $"https://dev-ecer-api.apps.silver.devops.gov.bc.ca/api/certifications/file/download/{firstFileId}";
                HttpResponseMessage downloadResponse = await client.GetAsync(downloadUrl);
                downloadResponse.EnsureSuccessStatusCode();

                string downloadContent = await downloadResponse.Content.ReadAsStringAsync();
                bool savePFEResult=await SaveImportFile(appUserService, d365WebApiService, downloadContent);
                List<CertificationDetail> certificationDetails = JsonConvert.DeserializeObject<List<CertificationDetail>>(downloadContent);
                #endregion


                #region comment
           //     if (certificationDetails == null || certificationDetails.Count == 0)
           //     {
           //         return ProcessResult.Failure(ProcessId, new String[] { "ECER Certificate file is blank" }, 0, 0).SimpleProcessResult;

           //     }
           //     var filteredRecords = certificationDetails.Where(r => !string.IsNullOrWhiteSpace(r.registrationnumber) && !string.IsNullOrWhiteSpace(r.certificatelevel)).ToList();

           //     // get all ECE certification from CRM
           //     List<JsonNode> oldECECertData = await FetchAllRecordsFromCRMAsync(EmployeeCertificateRequestUri);
           //     List<CertificationDetail> distinctRecords = filteredRecords
           //.GroupBy(r => new { r.registrationnumber, r.certificatelevel })
           //.Select(g => g.OrderByDescending(r => r.effectivedate).First())
           //.ToList();

           //     var crmRecordsDict = oldECECertData.ToDictionary(cr => ((string)cr["ofm_certificate_level"], (string)cr["ofm_certificate_number"]), cr => cr);

                //List<CertificationDetail> differenceCsvRecords = new List<CertificationDetail>();
                //List<CertificationDetail> impactCertStatusRecords = new List<CertificationDetail>();
                //foreach (var csvRecord in certificationDetails)
                //{
                //    var key = (csvRecord.registrationnumber, csvRecord.certificatelevel);
                //    if (crmRecordsDict.TryGetValue(key, out var crmRecord))
                //    {
                //       if (csvRecord.expirydate.HasValue && ((DateTime)crmRecord["ofm_expiry_date"]).Date != csvRecord.expirydate)
                //        {
                //            differenceCsvRecords.Add(csvRecord);
                //            impactCertStatusRecords.Add(csvRecord);
                //            continue;
                //        }
                //        if (csvRecord.effectivedate.HasValue)
                //       if(csvRecord.effectivedate.HasValue &&(((DateTime)crmRecord["ofm_effective_date"]).Date != csvRecord.effectivedate.Value.Date))
                //       {
                //            differenceCsvRecords.Add(csvRecord);
                //            impactCertStatusRecords.Add(csvRecord);
                //            continue;
                //        }
                //        if ((!string.IsNullOrEmpty(((string)crmRecord["ofm_first_name"])?.Trim()) ? ((string)crmRecord["ofm_first_name"])?.Trim() : null) != (!string.IsNullOrEmpty(csvRecord.firstname?.Trim()) ? csvRecord.firstname.Trim() : null))
                //        {
                //            differenceCsvRecords.Add(csvRecord);
                //            continue;
                //        }
                //        if ((!string.IsNullOrEmpty(((string)crmRecord["ofm_last_name"])?.Trim()) ? ((string)crmRecord["ofm_last_name"])?.Trim() : null) != (!string.IsNullOrEmpty(csvRecord.lastname?.Trim()) ? csvRecord.lastname.Trim() : null))
                //        {
                //            differenceCsvRecords.Add(csvRecord);
                //            continue;
                //        }

                //    }
                //    else
                //    {
                //        differenceCsvRecords.Add(csvRecord);
                //        impactCertStatusRecords.Add(csvRecord);
                //    }
                //}
                #endregion

                var upsertECERequests = new List<HttpRequestMessage>() { };
                foreach (var record in certificationDetails)
                {
                    var ECECert = new JsonObject
                    {
                        { "ofm_first_name", record?.firstname},
                        { "ofm_last_name", record?.lastname},
                        { "ofm_effective_date", record?.effectivedate?.ToString("yyyy-MM-dd")},
                        { "ofm_expiry_date", record?.expirydate?.ToString("yyyy-MM-dd")},
                        { "statecode", 0 }
                    };
                    // upsertECERequests.Add(new UpsertRequest(new D365EntityReference("ofm_employee_certificates", new Dictionary<string, string> { { "ofm_certificate_level",record?.certificatelevel },{ "ofm_certificate_number",record?.registrationnumber } }), ECECert)) ;
                    if (!String.IsNullOrEmpty(record?.certificatelevel) && !String.IsNullOrEmpty(record.registrationnumber))
                    {   //upsertECERequests.Add(new UpsertRequest(new D365EntityReference("ofm_employee_certificates", new Dictionary<string, string> { { "ofm_certificate_level", record?.certificatelevel }, { "ofm_certificate_number", record?.registrationnumber } }), ECECert));

                       upsertECERequests.Add(new UpsertRequest(new D365EntityReference("ofm_employee_certificates(ofm_certificate_number='" + record?.registrationnumber + "',ofm_certificate_level='"+ record?.certificatelevel?.Replace(",", " ").ToString()+"')"), ECECert));
                    }
                }
                var upsertECECertResults = await d365WebApiService.SendBatchMessageAsync(_appUserService.AZSystemAppUser, upsertECERequests, null);
                if (upsertECECertResults.Errors.Any())
                {
                    var errorInfos = ProcessResult.Failure(ProcessId, upsertECECertResults.Errors, upsertECECertResults.TotalProcessed, upsertECECertResults.TotalRecords);

                    _logger.LogError(CustomLogEvent.Process, "Failed to Upsert ECE Certification: {error}", JsonValue.Create(errorInfos)!.ToString());
                    upsertMessages += "Batch Upsert errors: " + JsonValue.Create(errorInfos) + "\n\r";
                }

                #region Update all Provider Employees of Application
                // Update existing Cert status of Provider Employee of Applicaton 
                // 1. Update EFFDate,EXPIREDATE,ISACTIVE changed records 
                List<JsonNode> providerEmployees = await FetchAllRecordsFromCRMAsync(providerEmployeeRequestUri);
                int batchSize = 1000;
                ////List<JsonNode> providerEmployeesUpdateForChanged = providerEmployees
                //.Where(employee => impactCertStatusRecords.Any(record =>
                //    record.registrationnumber.Trim() == employee["ofm_certificate_number"]?.ToString().Trim() && record.certificatelevel.Trim() == employee["ofm_certificate_level"]?.ToString().Trim()))
                //.ToList();
                for (int i = 0; i < providerEmployees.Count; i += batchSize)
                {
                    var updateRequests = new List<HttpRequestMessage>() { };
                    var batches = providerEmployees.Skip(i).Take(batchSize).ToList();
                    bool certStatus = false;
                    int _certiStatusflag;
                    foreach (var batch in batches)
                    {
                        //CertificationDetail firstUniqueRecord = distinctRecords
                        //    .Where(s => s.registrationnumber.Trim() == batch["ofm_certificate_number"].ToString().Trim())
                        //    .FirstOrDefault();

                        string employeeTypeString = batch["ofm_employee_type"]?.ToString();
                        if (int.TryParse(employeeTypeString, out int employeeTypeKey))
                        {
                            var application_certificate = EmployeeType.TryGetValue(employeeTypeKey, out string emp_type);

                            // var application_certificate = EmployeeType.TryGetValue(Convert.ToInt32(batch["ofm_employee_type"]), out string emp_type);
                            // bool certStatus = validateCertStatus(firstUniqueRecord,emp_type);
                            // if (batch.("ofm_employee_certificate.ofm_certificate_level") == null)

                            if (!string.IsNullOrEmpty(batch["ofm_employee_certificate.ofm_certificate_level"]?.ToString()) && batch["ofm_employee_certificate.ofm_certificate_level"].ToString().Contains(application_certificate.ToString()))
                                certStatus = true;
                        }
                        _certiStatusflag = certStatus ? 1 : 0;

                        if (batch["ofm_certificate_status"]?.GetValue<int>() == _certiStatusflag)
                            continue;

                        var tempObject = new JsonObject
                        {
                            ["ofm_certificate_status"] = _certiStatusflag
                        };
                        updateRequests.Add(new D365UpdateRequest(new D365EntityReference("ofm_provider_employees", (Guid)batch["ofm_provider_employeeid"]), tempObject));
                    }
                    if (updateRequests.Count == 0) continue;
                    var updateResults = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updateRequests, null);
                    if (updateResults.Errors.Any())
                    {
                        var errorInfos = ProcessResult.Failure(ProcessId, updateResults.Errors, updateResults.TotalProcessed, updateResults.TotalRecords);
                        _logger.LogError(CustomLogEvent.Process, "Failed to providerEmployeesUpdateForChanged : {error}", JsonValue.Create(errorInfos)!.ToString());
                        // deactiveMessages += "Batch Upsert errors: " + JsonValue.Create(errorInfos) + "\n\r";
                    }
                    // Console.WriteLine("providerEmployeesUpdateForChanged index:{0}",i);
                    _logger.LogDebug(CustomLogEvent.Process, "Batch providerEmployeesUpdateForChanged index:{0}", i);
                }
                #endregion
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }
        }

    

        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            var returnObject = ProcessResult.Failure(ProcessId, new String[] { "Critical error", ex.StackTrace }, 0, 0).ODProcessResult;
            return returnObject;
        }
    }

    private async Task<bool> SaveImportFile(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, string result)
    {
        var importfileName = ("Provider Certificate" + "-"+ DateTime.UtcNow.ToLocalPST().ToString("yyyyMMddHHmmss"));
        var requestBody = new JsonObject()
        {
            ["ofm_name"] = importfileName,
            ["ofm_import_type"] = 1
        };

        var pfeCreateResponse = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, ofm_data_import.EntitySetName, requestBody.ToString());

        if (!pfeCreateResponse.IsSuccessStatusCode)
        {
            var pfeCreateError = await pfeCreateResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create payment file exchange record with the server error {responseBody}", JsonValue.Create(pfeCreateError)!.ToString());

            return await Task.FromResult(false);
        }

        var pfeRecord = await pfeCreateResponse.Content.ReadFromJsonAsync<JsonObject>();

        if (pfeRecord is not null && pfeRecord.ContainsKey(ofm_data_import.Fields.ofm_data_importid))
        {
            if (importfileName.Length > 0)
            {
                // Update the new Payment File Exchange record with the new document
                HttpResponseMessage pfeUpdateResponse = await _d365webapiservice.SendDocumentRequestAsync(_appUserService.AZPortalAppUser, ofm_data_import.EntitySetName,
                                                                                                    new Guid(pfeRecord[ofm_data_import.Fields.ofm_data_importid].ToString()),
                                                                                                    Encoding.ASCII.GetBytes(result.TrimEnd()),
                                                                                                    importfileName);

                if (!pfeUpdateResponse.IsSuccessStatusCode)
                {
                    var pfeUpdateError = await pfeUpdateResponse.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to update data import document with ECER file with the server error {responseBody}", pfeUpdateError.CleanLog());

                    return await Task.FromResult(false);
                }
            }
        }
        return await Task.FromResult(true);
    }

}

