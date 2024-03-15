using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;
using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfile;

public class P405VerifyGoodStandingBatchProvider : ID365ProcessProvider
{
    private readonly BCRegistrySettings _BCRegistrySettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _organizationId;

    public P405VerifyGoodStandingBatchProvider(IOptionsSnapshot<ExternalServices> ApiKeyBCRegistry, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _BCRegistrySettings = ApiKeyBCRegistry.Value.BCRegistryApi;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.ProviderProfiles.VerifyGoodStandingBatchId;
    public string ProcessName => Setup.Process.ProviderProfiles.VerifyGoodStandingBatchName;

    public string RequestUri
    {
        get
        {
            //  <fetch top="2" distinct="true" no-lock="true" >
            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true" >
                      <entity name="account" >
                        <attribute name="accountid" />
                        <attribute name="name" />
                        <attribute name="ofm_incorporation_number" />
                        <attribute name="ofm_business_number" />
                        <attribute name="statecode" />
                        <filter type="and" >
                          <condition attribute="statecode" operator="eq" value="0" />
                          <condition attribute="parentaccountid" operator="null" />
                          <condition attribute="ccof_accounttype" operator="eq" value="100000000" />
                          <condition entityname="bu" attribute="name" operator="eq" value="OFM" />
                          <condition entityname="application" attribute="ofm_applicationid" operator="not-null" />
                        </filter>
                        <link-entity name="businessunit" from="businessunitid" to="owningbusinessunit" link-type="inner" alias="bu" >
                          <attribute name="name" />
                        </link-entity>
                        <link-entity name="ofm_application" from="ofm_organization" to="accountid" link-type="inner" alias="application" />
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         accounts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }
    public string StandingHistoryRequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch top="5" distinct="true" no-lock="true">
                      <entity name="ofm_standing_history">
                        <attribute name="ofm_standing_historyid" />
                        <attribute name="ofm_organization" />
                        <attribute name="ofm_good_standing_status" />
                        <attribute name="ofm_start_date" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_validated_on" />
                        <attribute name="ofm_duration" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <order attribute="ofm_start_date" descending="true" />
                        <filter type="and">
                          <condition attribute="statecode" operator="eq" value="0" />
                          <condition attribute="ofm_organization" operator="eq" value="{_organizationId}" /> 
                        </filter>  
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_standing_histories?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

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

    private async Task<ProcessData> GetStandingHistoryDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "GetStandingHistoryDataAsync");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, StandingHistoryRequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Standing History records with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Standing History records found with query {requestUri}", StandingHistoryRequestUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;

        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetDataAsync();

        var deserializedData = JsonSerializer.Deserialize<List<D365Organization_Account>>(localData.Data.ToString());

        deserializedData?.ForEach(async organization =>
        {
            string organizationId = organization.accountid;
            string legalName = organization.name;
            string incorporationNumber = organization.ofm_incorporation_number;
            string businessNumber = organization.ofm_business_number;

            string? queryValue = (!string.IsNullOrEmpty(incorporationNumber)) ? incorporationNumber.Trim() : legalName.Trim();

            var legalType = "A,B,BC,BEN,C,CC,CCC,CEM,CP,CS,CUL,EPR,FI,FOR,GP,LIC,LIB,LL,LLC,LP,MF,PA,PAR,PFS,QA,QB,QC,QD,QE,REG,RLY,S,SB,SP,T,TMY,ULC,UQA,UQB,UQC,UQD,UQE,XCP,XL,XP,XS";
            var status = "active";
            var queryString = $"?query=value:{queryValue}::identifier:::bn:::name:" +
                              $"&categories=legalType:{legalType}::status:{status}";

            var path = $"{_BCRegistrySettings.RegistrySearchUrl}" + $"{queryString}";

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Account-Id", "1");
            request.Headers.Add(_BCRegistrySettings.KeyName, _BCRegistrySettings.KeyValue);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();

            BCRegistrySearchResult? searchResult = await response.Content.ReadFromJsonAsync<BCRegistrySearchResult>();

            // Organization - Update
            var goodStandingStatus = 3;                    // 1 - Good, 2 - No Good, 3 - Error 

            // Integration Log - Create
            var externalService = "BC Registries";
            var subject = string.Empty;
            var logCategory = 1;                           // 1 - Info, 2 - Warning, 3 - Error, 4 - Critical
            var message = $"{responseBody.CleanLog()}";

            if (searchResult is null || searchResult.searchResults.totalResults < 1)
            {
                // Todo: Add a new message for this scenario or try seach by name
                _logger.LogError(CustomLogEvent.Process, "No results found.");

                goodStandingStatus = 3;
                logCategory = 3;
                subject = "No results found";
                await UpdateOrganizationCreateIntegrationLog(_appUserService, _d365webapiservice, organizationId, goodStandingStatus, subject, logCategory, message, externalService);
                // return ProcessResult.PartialSuccess(ProcessId, ["No records found."], 0, 0).SimpleProcessResult;
            }

            if (searchResult.searchResults.totalResults > 1)
            {
                // ToDo: Process and filter the result further
                _logger.LogError(CustomLogEvent.Process, "More than one records returned. Please resolve this issue to ensure uniqueness");

                goodStandingStatus = 3;
                logCategory = 3;
                subject = "Multiple results returned";
                await UpdateOrganizationCreateIntegrationLog(_appUserService, _d365webapiservice, organizationId, goodStandingStatus, subject, logCategory, message, externalService);
                // return ProcessResult.PartialSuccess(ProcessId, ["Multiple results returned."], 0, 0).SimpleProcessResult;
            }

            if (searchResult.searchResults.totalResults == 1)
            {
                goodStandingStatus = searchResult.searchResults.results.First().goodStanding ? 1 : 2;                // 1 - Good, 2 - No Good, 3 - Error 
                logCategory = 1;                                                                                     // 1 - Info, 2 - Warning, 3 - Error, 4 - Critical
                subject = "One result returned";
                await UpdateOrganizationCreateIntegrationLog(_appUserService, _d365webapiservice, organizationId, goodStandingStatus, subject, logCategory, message, externalService);

                // Handling Standing History
                var goodStandingStatusYN = searchResult.searchResults.results.First().goodStanding ? 1 : 0;          // 0 - No, 1 - Yes 
                await CreateUpdateStandingHistory(_appUserService, _d365webapiservice, organizationId, goodStandingStatusYN);

                // return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

        });

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    private async Task<JsonObject> UpdateOrganizationCreateIntegrationLog(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, string organizationId, int goodStandingStatus, string subject, int category, string message, string externalService)
    {
        var statement = $"accounts({organizationId})";
        var payload = new JsonObject {
                { "ofm_good_standing_status", goodStandingStatus},
                { "ofm_good_standing_validated_on", DateTime.UtcNow }
        };

        var requestBody = JsonSerializer.Serialize(payload);

        var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

        if (!patchResponse.IsSuccessStatusCode)
        {
            var responseBody = await patchResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to patch GoodStanding status on organization with the server error {responseBody}", responseBody.CleanLog());

            return ProcessResult.Failure(ProcessId, new string[] { responseBody }, 0, 0).SimpleProcessResult;
        }

        var entitySetName = "ofm_integration_logs";
        var payload2 = new JsonObject {
                { "ofm_subject", subject },
                { "ofm_category", category},
                { "ofm_message", message},
                { "ofm_service_name", externalService},
                { "ofm_regardingid_account@odata.bind", $"/accounts({organizationId})"}
        };
        var requestBody2 = JsonSerializer.Serialize(payload2);
        var CreateResponse = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody2);

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    private async Task<JsonObject> CreateUpdateStandingHistory(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, string organizationId, int goodStandingStatusYN)
    {
        _organizationId = organizationId;

        var localData = await GetStandingHistoryDataAsync();

        var deserializedData = JsonSerializer.Deserialize<List<D365StandingHistory>>(localData.Data.ToString());

        if (deserializedData.Count < 1)                  // no records found                                                              
        {
            // Operation - Create new record
            var entitySetName = "ofm_standing_histories";                                                      // Case 1. create new record --> open (active)
            var payload = new JsonObject {
                                { "ofm_good_standing_status", goodStandingStatusYN },                                 // 0 - No, 1 - Yes
                                { "ofm_start_date", DateTime.UtcNow },
                                //{ "ofm_end_date", endDate },
                                //{ "ofm_duration", duration.Days },
                                { "statecode", 0 },                                                                   // 0 - active, 1 - inactive
                                { "statuscode", 1 },                                                                  // 1 - Open (active), 2 - Closed (inactive)
                                //{ "ofm_validated_on", DateTime.UtcNow },
                                { "ofm_organization@odata.bind", $"/accounts({organizationId})"}
                            };
            var requestBody = JsonSerializer.Serialize(payload);
            var CreateResponse = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody);
        }

        if (deserializedData.Count >= 1)                // Records found
        {
            var standingHistoryId = deserializedData.First().ofm_standing_historyid;
            var goodStandingStatus_History = deserializedData.First().ofm_good_standing_status;
            //var organizationId_History = deserializedData.First()._ofm_organization_value;

            if (Equals(goodStandingStatus_History, goodStandingStatusYN))                                      // Case 2. open --> update validated_On
            {
                // Operation - update the existing record
                var statement = $"ofm_standing_histories({standingHistoryId})";
                var payload = new JsonObject {
                                //{ "ofm_good_standing_status", 0 },                                                 
                                //{ "ofm_start_date", startDate},
                                //{ "ofm_end_date", endDate },
                                //{ "ofm_duration", duration.Days },
                                //{ "statecode", 1 },                                                                 
                                //{ "statuscode", 2 },                                                                
                                { "ofm_validated_on", DateTime.UtcNow}
                            };
                var requestBody = JsonSerializer.Serialize(payload);
                var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);
            }
            else
            {
                DateTime startDate = (DateTime)deserializedData.First().ofm_start_date;
                DateTime endDate = DateTime.UtcNow;
                TimeSpan duration = endDate - startDate;

                // Operation - update the existing record and then deactivate it
                var statement = $"ofm_standing_histories({standingHistoryId})";                                // Case 3.1 update endDate/duration and deactivate previous record
                var payload = new JsonObject {
                                //{ "ofm_good_standing_status", 0 },                                                   
                                //{ "ofm_start_date", startDate},
                                { "ofm_end_date", endDate  },
                                { "ofm_duration", duration.Days },
                                { "statecode", 1 },                                                                    // 1 - inactive
                                { "statuscode", 2 },                                                                   // 2 - Closed (inactive)
                                { "ofm_validated_on", (DateTime?)null}
                            };
                var requestBody = JsonSerializer.Serialize(payload);
                var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

                // Operation - Create new record
                var entitySetName = "ofm_standing_histories";                                                  // Case 3.2 create new record --> open (active)
                var payload2 = new JsonObject {
                                { "ofm_good_standing_status", goodStandingStatusYN },
                                { "ofm_start_date", endDate },
                                //{ "ofm_end_date", endDate },
                                //{ "ofm_duration", duration.Days },
                                { "statecode", 0 },
                                { "statuscode", 1 },
                                //{ "ofm_validated_on", DateTime.UtcNow },
                                { "ofm_organization@odata.bind", $"/accounts({organizationId})"}
                            };
                var requestBody2 = JsonSerializer.Serialize(payload2);
                var CreateResponse2 = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody2);
            }
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}