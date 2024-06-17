using CsvHelper;
using CsvHelper.Configuration;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.DataImports;

public class P700ProviderCertificateProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly TimeProvider _timeProvider = timeProvider;
    private ProcessParameter? _processParams;
    private ProcessData? _data;

    public Int16 ProcessId => Setup.Process.DataImports.ProcessProviderCertificatesId;
    public string ProcessName => Setup.Process.DataImports.ProcessProviderCertificatesName;

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
                                ofm_employee_certificates?$select=ofm_expiry_date,ofm_certificate_number,ofm_effective_date,ofm_first_name,ofm_is_active,ofm_last_name,ofm_middle_name&$orderby=ofm_certificate_number asc&$filter=(statecode eq 0)
                                """;
            return requestUri;
        }
    }
    private string DataImportActiveRequestUri
    {
        get
        {
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                    <fetch>
                      <entity name=""ofm_data_import"">
                        <attribute name=""ofm_name"" />
                        <attribute name=""statecode"" />
                        <filter>
                          <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                        </filter>
                      </entity>
                    </fetch>";
            var requestUri = $"""
                            ofm_data_imports?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    public class Record
    {
        public string CLIENTID { get; set; }
        public string LASTNAME { get; set; }
        public string FIRSTNAME { get; set; }
        public string MIDDLENAME { get; set; }
        public string CERTLEVEL { get; set; }
        public DateTime EFFDATE { get; set; }
        public DateTime EXPDATE { get; set; }
        public string ISACTIVE { get; set; }
    }
    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P700ProviderCertificateProvider));

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
    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
        var startTime = _timeProvider.GetTimestamp();
        string dataImportMessages = string.Empty;
        string upsertMessages = string.Empty;
        string deactiveMessages = string.Empty;
        bool upsertSucessfully = false;
        bool deactiveSucessfully = false;
        try
        {
            // retrieve csv file from crm and parse 
            var localData = await GetDataAsync();
            var downloadfile = Convert.FromBase64String(localData.Data.ToString());
            var downloadfileUTF8 = Encoding.UTF8.GetString(downloadfile);
            List<Record> csvRecords;
            // Validate csv file
            using (var reader = new StringReader(downloadfileUTF8))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                var records = csv.GetRecords<dynamic>().ToList();
                bool containsSearchString = false;
                if (records.Count > 0)
                {
                    // Get the first record
                    var firstRecord = records[0] as IDictionary<string, object>;
                    containsSearchString = firstRecord.Keys.Any(key => key.Equals("CLIENTID", StringComparison.OrdinalIgnoreCase)) &&
                       firstRecord.Keys.Any(key => key.Equals("LASTNAME", StringComparison.OrdinalIgnoreCase)) &&
                       firstRecord.Keys.Any(key => key.Equals("FIRSTNAME", StringComparison.OrdinalIgnoreCase)) &&
                       firstRecord.Keys.Any(key => key.Equals("MIDDLENAME", StringComparison.OrdinalIgnoreCase)) &&
                       firstRecord.Keys.Any(key => key.Equals("EFFDATE", StringComparison.OrdinalIgnoreCase)) &&
                       firstRecord.Keys.Any(key => key.Equals("EXPDATE", StringComparison.OrdinalIgnoreCase)) &&
                       firstRecord.Keys.Any(key => key.Equals("ISACTIVE", StringComparison.OrdinalIgnoreCase));
                }
                if ((records.Count == 0) || !containsSearchString)
                {
                    var ECECertStatement = $"ofm_data_imports({_processParams.DataImportId})";
                    var payload = new JsonObject {
                        { "ofm_message", "CSV File has error format"},
                        { "statuscode", 5},
                        { "statecode", 0 }
                    };
                    var requestBody = JsonSerializer.Serialize(payload);
                    var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, ECECertStatement, requestBody);
                    if (!patchResponse.IsSuccessStatusCode)
                    {
                        var responseBody = await patchResponse.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());
                        return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
                    }
                    return ProcessResult.Failure(ProcessId, new String[] { "CSV File has error format" }, 0, 0).SimpleProcessResult;
                }
            }
            // retrieve records from csv file
            using (var reader = new StringReader(downloadfileUTF8))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                csvRecords = csv.GetRecords<Record>().ToList();
            }

            var filteredRecords = csvRecords.Where(r => !string.IsNullOrWhiteSpace(r.CLIENTID)).ToList();
            // get all ECE certification from CRM
            List<JsonNode> oldECECertData = await FetchAllRecordsFromCRMAsync(EmployeeCertificateRequestUri);
            var crmRecordsDict = oldECECertData.ToDictionary(cr => (string)cr["ofm_certificate_number"]);
            List<Record> distinctRecords = filteredRecords
            .GroupBy(r => r.CLIENTID)
            .Select(g => g.OrderByDescending(r => r.EFFDATE).First())
            .ToList();
            List<Record> differenceCsvRecords = new List<Record>();
            foreach (var csvRecord in distinctRecords)
            {
                // var crmRecord = oldECECertData.FirstOrDefault(cr => (string)cr["ofm_certificate_number"] == csvRecord.CLIENTID);
                // if (crmRecord != null)
                if (crmRecordsDict.TryGetValue(csvRecord.CLIENTID, out var crmRecord))
                {
                    if ((!string.IsNullOrEmpty(((string)crmRecord["ofm_first_name"])?.Trim()) ? ((string)crmRecord["ofm_first_name"])?.Trim() : null) != (!string.IsNullOrEmpty(csvRecord.FIRSTNAME?.Trim()) ? csvRecord.FIRSTNAME.Trim() : null))
                    {
                        differenceCsvRecords.Add(csvRecord);
                        continue;
                    }
                    if ((!string.IsNullOrEmpty(((string)crmRecord["ofm_last_name"])?.Trim()) ? ((string)crmRecord["ofm_last_name"])?.Trim() : null) != (!string.IsNullOrEmpty(csvRecord.LASTNAME?.Trim()) ? csvRecord.LASTNAME.Trim() : null))
                    {
                        differenceCsvRecords.Add(csvRecord);
                        continue;
                    }
                    if ((!string.IsNullOrEmpty(((string)crmRecord["ofm_middle_name"])?.Trim()) ? ((string)crmRecord["ofm_middle_name"])?.Trim() : null) != (!string.IsNullOrEmpty(csvRecord.MIDDLENAME?.Trim()) ? csvRecord.MIDDLENAME.Trim() : null))
                    {
                        differenceCsvRecords.Add(csvRecord);
                        continue;
                    }
                    if ((bool)crmRecord["ofm_is_active"] != (csvRecord.ISACTIVE?.ToLower() == "yes"))
                    {
                        differenceCsvRecords.Add(csvRecord);
                        continue;
                    }
                    if (((DateTime)crmRecord["ofm_expiry_date"]).Date != csvRecord.EXPDATE)
                    {
                        differenceCsvRecords.Add(csvRecord);
                        continue;
                    }
                    if (((DateTime)crmRecord["ofm_effective_date"]).Date != csvRecord.EFFDATE)
                    {
                        differenceCsvRecords.Add(csvRecord);
                        continue;
                    }
                }
                else { differenceCsvRecords.Add(csvRecord); }
            }
            // Batch processing
            int batchSize = 1000;
            for (int i = 0; i < differenceCsvRecords.Count; i += batchSize)
            {
                var upsertECERequests = new List<HttpRequestMessage>() { };
                var batch = differenceCsvRecords.Skip(i).Take(batchSize).ToList();
                foreach (var record in batch)
                {
                    var ECECert = new JsonObject
                    {
                        { "ofm_first_name", record?.FIRSTNAME},
                        { "ofm_last_name", record?.LASTNAME},
                        { "ofm_middle_name", record?.MIDDLENAME},
                        { "ofm_effective_date", record?.EFFDATE.ToString("yyyy-MM-dd")},
                        { "ofm_expiry_date", record?.EXPDATE.ToString("yyyy-MM-dd")},
                        { "ofm_is_active", record?.ISACTIVE.ToLower() == "yes" },
                        { "statecode", 0 }
                    };
                    upsertECERequests.Add(new UpsertRequest(new EntityReference("ofm_employee_certificates(ofm_certificate_number='" + record?.CLIENTID + "')"), ECECert));

                }
                var upsertECECertResults = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, upsertECERequests, null);
                if (upsertECECertResults.Errors.Any())
                {
                    var errorInfos = ProcessResult.Failure(ProcessId, upsertECECertResults.Errors, upsertECECertResults.TotalProcessed, upsertECECertResults.TotalRecords);

                    _logger.LogError(CustomLogEvent.Process, "Failed to Upsert ECE Certification: {error}", JsonValue.Create(errorInfos)!.ToString());
                    upsertMessages += "Batch Upsert errors: " + JsonValue.Create(errorInfos) + "\n\r";
                }
                Console.WriteLine("Upsert Batch process record index:", i.ToString());
            }
            if (string.IsNullOrEmpty(upsertMessages))
            {
                upsertSucessfully = true;
            }
            else
            {
                dataImportMessages += dataImportMessages + "Upsert records Failed \r\n";
            }
            // deal with missing record in CRM and deactive them
            HashSet<string> csvClientIds = new HashSet<string>(distinctRecords.Select(r => r.CLIENTID));
            List<JsonNode> missingInCsv = oldECECertData
            .Where(cert => !csvClientIds.Contains((string)cert["ofm_certificate_number"]))
             .ToList();

            for (int i = 0; i < missingInCsv.Count; i += batchSize)
            {
                var updateMissingECERequests = new List<HttpRequestMessage>() { };
                var batch = missingInCsv.Skip(i).Take(batchSize).ToList();
                foreach (var record in batch)
                {
                    var ECECert = new JsonObject
                    {
                        { "statecode", 1 }
                    };
                    updateMissingECERequests.Add(new D365UpdateRequest(new EntityReference("ofm_employee_certificates", (Guid)record["ofm_employee_certificateid"]), ECECert));

                }
                var upsertMissingECECertResults = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updateMissingECERequests, null);
                if (upsertMissingECECertResults.Errors.Any())
                {
                    var errorInfos = ProcessResult.Failure(ProcessId, upsertMissingECECertResults.Errors, upsertMissingECECertResults.TotalProcessed, upsertMissingECECertResults.TotalRecords);

                    _logger.LogError(CustomLogEvent.Process, "Failed to Upsert ECE Certification: {error}", JsonValue.Create(errorInfos)!.ToString());
                    deactiveMessages += "Batch Upsert errors: " + JsonValue.Create(errorInfos) + "\n\r";
                }
                Console.WriteLine("Batch Deactive missing process record index:", i.ToString());
            }

            if (string.IsNullOrEmpty(deactiveMessages))
            {
                deactiveSucessfully = true;
            }
            else
            {
                dataImportMessages += dataImportMessages + "Deactive records do not existing in csv file Failed\r\n";
            }

            // update Data Import  message field
            if (upsertSucessfully && deactiveSucessfully)
            {
                //TimeZoneInfo pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                //DateTime pstTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pstZone);
                //dataImportMessages = pstTime.ToString("yyyy-MM-dd HH:mm:ss") +"\r\n"+"Upsert " + differenceCsvRecords.Count + " record(s) sucessfully\r\n" + "Deactive all " + missingInCsv.Count + " records not existing in csv file sucessfully\r\n";
                dataImportMessages = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + "Upsert " + differenceCsvRecords.Count + " record(s) sucessfully\r\n" + "Deactive all " + missingInCsv.Count + " records not existing in csv file sucessfully\r\n";
                var ECECertStatement = $"ofm_data_imports({_processParams.DataImportId})";
                var payload = new JsonObject {
                        { "ofm_message", dataImportMessages},
                        { "statuscode", 4},
                        { "statecode", 0 }
                    };
                var requestBody = JsonSerializer.Serialize(payload);
                var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, ECECertStatement, requestBody);
                if (!patchResponse.IsSuccessStatusCode)
                {
                    var responseBody = await patchResponse.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());
                    return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
                }
                // Deactive Previous Data Imports 
                List<JsonNode> allActiveDataImports = await FetchAllRecordsFromCRMAsync(DataImportActiveRequestUri);
                allActiveDataImports = allActiveDataImports.Where(item => !item["ofm_data_importid"].ToString().Equals(_processParams.DataImportId.ToString())).ToList();
                foreach (var dataImport in allActiveDataImports)
                {
                    var deactiveDataImport = $"ofm_data_imports({dataImport["ofm_data_importid"].ToString()})";
                    payload = new JsonObject {
                        { "statecode", 1 }
                    };
                    requestBody = JsonSerializer.Serialize(payload);
                    patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, deactiveDataImport, requestBody);
                    if (!patchResponse.IsSuccessStatusCode)
                    {
                        var responseBody = await patchResponse.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());
                        return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
                    }
                }
                Console.WriteLine("End Upsert Data Import ");
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }
            else
            {
                var ECECertStatement = $"ofm_data_imports({_processParams.DataImportId})";
                var payload = new JsonObject {
                        { "ofm_message", dataImportMessages},
                        { "statuscode", 5},
                        { "statecode", 0 }
                    };
                var requestBody = JsonSerializer.Serialize(payload);
                var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, ECECertStatement, requestBody);
                if (!patchResponse.IsSuccessStatusCode)
                {
                    var responseBody = await patchResponse.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());
                    return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
                }
                return ProcessResult.Failure(ProcessId, new String[] { "Upsert action failed" }, 0, 0).SimpleProcessResult;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            var returnObject = ProcessResult.Failure(ProcessId, new String[] { "Critical error", ex.StackTrace }, 0, 0).ODProcessResult;
            return returnObject;
        }
    }
}