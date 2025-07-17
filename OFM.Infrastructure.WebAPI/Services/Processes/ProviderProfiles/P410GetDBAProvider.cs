using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;

public class P410GetDBAProvider : ID365ProcessProvider
{
    private readonly BCRegistrySettings _BCRegistrySettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _organizationId;


    public P410GetDBAProvider(IOptionsSnapshot<ExternalServices> ApiKeyBCRegistry, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _BCRegistrySettings = ApiKeyBCRegistry.Value.BCRegistryApi;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.ProviderProfiles.GetDBAId;
    public string ProcessName => Setup.Process.ProviderProfiles.GetDBAName;

    #region fetchxml queries
    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="account">
                        <attribute name="accountid" />
                        <attribute name="name" />
                        <attribute name="ofm_incorporation_number" />
                        <attribute name="ofm_business_number" />
                        <attribute name="primarycontactid" />
                        <attribute name="statecode" />
                        <attribute name="ofm_doing_business_as" />
                        <filter type="and">
                          <condition attribute="accountid" operator="eq" value="{_processParams?.Organization?.organizationId}" />
                        </filter>
                        <link-entity name="contact" from="contactid" to="primarycontactid" link-type="inner" alias="contact">
                          <attribute name="emailaddress1" />
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         accounts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }
    #endregion

    #region Get data from fetchxml Uri
    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P400VerifyGoodStandingProvider));

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
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No records found");
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString());
        }

        return await Task.FromResult(_data);
    }

    #endregion


    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;

        if (_processParams == null || _processParams.Organization == null || _processParams.Organization.organizationId == null)
        {
            _logger.LogError(CustomLogEvent.Process, "Organization Id is missing.");
            return ProcessResult.Failure(ProcessId, new String[] { "Organization Id is missing" }, 0, 0).SimpleProcessResult;

        }

        var startTime = _timeProvider.GetTimestamp();
        var localData = await GetDataAsync();

        if (String.IsNullOrEmpty(localData.Data.ToString()))
        {
            _logger.LogError(CustomLogEvent.Process, "Failed to query records");
            return ProcessResult.Failure(ProcessId, new String[] { "Failed to query records" }, 0, 0).SimpleProcessResult;
        }

        var deserializedData = JsonSerializer.Deserialize<List<ECC.Core.DataContext.Account>>(localData.Data.ToString());
        var organization = deserializedData?.FirstOrDefault();
        string organizationId = organization.Id.ToString();
        
        if(organization.ofm_incorporation_number is null)
        {
            _logger.LogError(CustomLogEvent.Process, "Incorporation Number is missing");
            return ProcessResult.Failure(ProcessId, new String[] { "Incorporation Number is missing" }, 0, 0).SimpleProcessResult;
        }

        string incorporationNumber = organization.ofm_incorporation_number.Trim();

        var path = $"{_BCRegistrySettings.BusinessSearchUrl}" + $"/{incorporationNumber}";

        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(_BCRegistrySettings.AccoutIdName, _BCRegistrySettings.AccoutIdValue);
        request.Headers.Add(_BCRegistrySettings.KeyName, _BCRegistrySettings.KeyValue);

        var response = await client.SendAsync(request);

        //If the business is found - status 200
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();

            BCRegistryBusinessResult? businessResult = await response.Content.ReadFromJsonAsync<BCRegistryBusinessResult>();

            //Check for DBA
            if (businessResult is null || businessResult.business.alternateNames is null || businessResult.business.alternateNames.Length < 1)
            {
                return ProcessResult.PartialSuccess(ProcessId, new String[] { "No DBA found" }, 0, 0).SimpleProcessResult;
            }
            else
            {
                //type should be "DBA" and identifier == business ofm_incorporation_number
                var DBAList = businessResult.business.alternateNames.Where(names => names.type == "DBA" && names.identifier == incorporationNumber).ToList();
                DBAList.Sort(delegate (Alternatename a, Alternatename b)
                {
                    if ((a.startDate == b.startDate) || (a.startDate == null && b.startDate == null)) return 0;
                    else if (a.startDate == null) return -1;
                    else if (b.startDate == null) return 1;
                    else return DateTime.Parse(b.startDate).CompareTo(DateTime.Parse(a.startDate));
                });

                var DBAInfo = DBAList.FirstOrDefault();

                if(DBAInfo != null)
                {
                    //Update Organization record
                    var statement = $"accounts({organizationId})";
                    var payload = new JsonObject {
                        { "ofm_doing_business_as", DBAInfo.name}
                    };

                    var requestBody = JsonSerializer.Serialize(payload);

                    var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

                    if (!patchResponse.IsSuccessStatusCode)
                    {
                        var patchResponseBody = await patchResponse.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Process, "Failed to patch DBA information on organization with the server error {patchResponseBody}", patchResponseBody.CleanLog());

                        return ProcessResult.Failure(ProcessId, new string[] { patchResponseBody }, 0, 0).SimpleProcessResult;
                    }
                }

            }
        }
        else if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Incorporation Number is not found in BC Business.", responseBody.CleanLog());
            return ProcessResult.Failure(ProcessId, new String[] { "Incorporation Number is not found in BC Business." }, 0, 0).SimpleProcessResult;

        }
        else
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Unable to call BC Registries API with an error {responseBody}.", responseBody.CleanLog());
            return ProcessResult.Failure(ProcessId, new String[] { "Unable to call BC Registries API with an error {responseBody}" }, 0, 0).SimpleProcessResult;
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}