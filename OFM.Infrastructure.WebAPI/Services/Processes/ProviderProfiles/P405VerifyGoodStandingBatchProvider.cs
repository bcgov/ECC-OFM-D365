using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;

public class P405VerifyGoodStandingBatchProvider(IOptionsSnapshot<ExternalServices> ApiKeyBCRegistry, IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IEmailRepository emailRepository) : ID365ProcessProvider
{
    private readonly BCRegistrySettings _BCRegistrySettings = ApiKeyBCRegistry.Value.BCRegistryApi;
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly TimeProvider _timeProvider = timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string? _organizationId = string.Empty;
    private string? _ofm_standing_historyid = string.Empty;
    private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
    private readonly IEmailRepository _emailRepository = emailRepository;
    private string? _GScommunicationType;
    private List<JsonNode>? _allFacilitiesWithActiveFundingData;
    BcRegistryService _bcregService;

    public Int16 ProcessId => Setup.Process.ProviderProfiles.VerifyGoodStandingBatchId;
    public string ProcessName => Setup.Process.ProviderProfiles.VerifyGoodStandingBatchName;

    #region fetchxml queries

    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                            <fetch distinct="true" no-lock="true" >
                              <entity name="account" >
                                <attribute name="accountid" />
                                <attribute name="name" />
                                <attribute name="ofm_incorporation_number" />
                                <attribute name="ofm_business_number" />
                                <attribute name="ofm_bypass_bc_registry_good_standing" />
                                <attribute name="primarycontactid"/>
                                <attribute name="statecode" />
                               <filter type="and">
                                  <condition attribute="statecode" operator="eq" value="0" />
                                  <condition attribute="ofm_bypass_bc_registry_good_standing" operator="ne" value="1" />
                                  <condition attribute="ccof_accounttype" operator="eq" value="100000000" />
                                  <condition attribute="name" operator="not-null" value="" />
                                  <condition attribute="ofm_program" operator="in">
                                    <value>1</value>
                                    <value>4</value>
                                  </condition>
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

    public string AllFacilitiesWithActiveFundingRequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch>
                      <entity name="account">
                        <attribute name="accountid" />
                        <attribute name="name" />
                        <attribute name="ofm_business_number" />
                        <attribute name="ofm_primarycontact" />
                        <attribute name="statecode" />
                        <attribute name="parentaccountid" />
                        <filter type="and">
                          <condition attribute="statecode" operator="eq" value="0" />
                          <condition attribute="parentaccountid" operator="not-null" value="" />
                          <condition attribute="ccof_accounttype" operator="eq" value="{(int)ccof_AccountType.Facility}" />
                        </filter>
                        <link-entity name="ofm_application" from="ofm_facility" to="accountid" link-type="inner" alias="ofm_app">
                          <link-entity name="ofm_funding" from="ofm_application" to="ofm_applicationid">
                            <filter>
                              <condition attribute="statecode" operator="eq" value="0" />
                            </filter>
                          </link-entity>
                        </link-entity>
                        <link-entity name="contact" from="contactid" to="ofm_primarycontact" link-type="inner" alias="contact">
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

    public string StandingHistoryRequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="ofm_standing_history">
                        <attribute name="ofm_standing_historyid" />
                        <attribute name="ofm_organization" />
                        <attribute name="ofm_good_standing_status" />
                        <attribute name="ofm_start_date" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_validated_on" />
                        <attribute name="ofm_no_counter" />
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

    public string RecordstoCreateTaskUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="ofm_standing_history">
                        <attribute name="ofm_standing_historyid" />
                        <attribute name="ofm_organization" />
                        <attribute name="ofm_good_standing_status" />
                        <attribute name="ofm_start_date" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_validated_on" />
                        <attribute name="ofm_no_counter" />
                        <attribute name="ofm_duration" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <order attribute="ofm_start_date" descending="true" />
                        <filter type="and">
                          <condition attribute="statecode" operator="eq" value="0" />
                          <condition attribute="ofm_standing_historyid" operator="eq" value="{_ofm_standing_historyid}" />  
                          </filter>  
                       <link-entity name="account" from="accountid" to="ofm_organization" link-type="inner" alias="dx">
                      <link-entity name="task" from="regardingobjectid" to="accountid" link-type="inner" alias="dy">
                        <filter type="and">
                          <filter type="or">
                            <condition attribute="ofm_process_responsible" operator="eq" value="{_BCRegistrySettings.batchtaskprocess}" />
                            <condition attribute="ofm_process_responsible" operator="eq" value="{_BCRegistrySettings.singletaskprocess}" />
                          </filter>
                        </filter>
                      </link-entity>
                    </link-entity>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_standing_histories?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

    #endregion

    #region Get data from fetchxml Uri

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P405VerifyGoodStandingBatchProvider));

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

    private async Task<ProcessData> GetRecordsDataAsync(string query)
    {
        _logger.LogDebug(CustomLogEvent.Process, "GetRecordToAReqDataAsync");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, query);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Standing History records for task creation {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No  records found with query {requestUri}", query.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
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

        return await Task.FromResult(allRecords);
    }

    #endregion

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
        var startTime = _timeProvider.GetTimestamp();
        _allFacilitiesWithActiveFundingData = await FetchAllRecordsFromCRMAsync(AllFacilitiesWithActiveFundingRequestUri);
        var localOrganizationsData = await GetDataAsync();
        var deserializedOrganizationsData = JsonSerializer.Deserialize<List<D365Organization_Account>>(localOrganizationsData.Data.ToString());
        int batchSize = _BCRegistrySettings.BatchSize;

        _bcregService = new BcRegistryService(_BCRegistrySettings);
        if (deserializedOrganizationsData.Any())
        {
            for (int i = 0; i < deserializedOrganizationsData?.Count; i += batchSize)
            {
                var tasks = deserializedOrganizationsData?.Skip(i).Take(batchSize).Select(org => CheckOrganizationGoodStanding(org));
                await Task.WhenAll(tasks!);

            }

        }

        var endTime = timeProvider.GetTimestamp();
        _logger.LogDebug(CustomLogEvent.Process, "P405VerifyGoodStandingBatchProvider process finished in {timer.ElapsedMilliseconds} miliseconds.", timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes);

        return await Task.FromResult(ProcessResult.Completed(ProcessId).SimpleProcessResult);
    }

    private async Task<DateTime> CheckOrganizationGoodStanding(D365Organization_Account organization)
    {
        string organizationId = organization.accountid;
        string legalName = organization.name;
        string incorporationNumber = organization.ofm_incorporation_number;
        var externalService = "BC Registries";
        var response = _bcregService.GetRegistryDataAsync(organizationId, legalName, incorporationNumber);
        var responseBody = await response.Result.Content.ReadAsStringAsync();

        if (!response.Result.IsSuccessStatusCode)
        {
            _logger.LogError(CustomLogEvent.Process, "Unable to call BC Registries API with an error {responseBody}.", responseBody.CleanLog());

            var standingStatusUpdate = (response.Result.StatusCode != HttpStatusCode.InternalServerError);

            await UpdateOrganizationCreateIntegrationLog(_appUserService, _d365webapiservice, organizationId, (int)ofm_good_standing_status.IntegrationError, subject: "Unable to call BC Registries API with an error.", (int)ecc_integration_log_category.Error, responseBody.CleanLog(), externalService, statusUpdate: standingStatusUpdate);

            return await Task.FromResult(DateTime.Now);
        }

        BCRegistrySearchResult? searchResult = await response.Result.Content.ReadFromJsonAsync<BCRegistrySearchResult>();

        // Organization - Update
        var goodStandingStatus = 3;                    // 1 - Good, 2 - No Good, 3 - Error 

        // Integration Log - Create
        var subject = string.Empty;
        var logCategory = (int)ecc_integration_log_category.Information;
        var message = $"{responseBody.CleanLog()}";

        if (searchResult is null || searchResult.searchResults is null || searchResult.searchResults.totalResults < 1)
        {
            // Todo: Add a new message for this scenario or try seach by name
            _logger.LogError(CustomLogEvent.Process, "No results found.");

            goodStandingStatus = 3;
            logCategory = 3;
            subject = "No results found";
            _ = await UpdateOrganizationCreateIntegrationLog(_appUserService, _d365webapiservice, organizationId, goodStandingStatus, subject, logCategory, message, externalService);
        }

        if (searchResult is not null && searchResult.searchResults is not null && searchResult.searchResults.totalResults > 1)
        {
            // ToDo: Process and filter the result further
            _logger.LogError(CustomLogEvent.Process, "More than one records returned. Please resolve this issue to ensure uniqueness");

            goodStandingStatus = 3;
            logCategory = 3;
            subject = "Multiple results returned";
            _ = await UpdateOrganizationCreateIntegrationLog(_appUserService, _d365webapiservice, organizationId, goodStandingStatus, subject, logCategory, message, externalService);
        }

        if (searchResult is not null && searchResult.searchResults is not null && searchResult.searchResults.totalResults == 1)
        {
            goodStandingStatus = searchResult.searchResults.results.First().goodStanding ? 1 : 2;                // 1 - Good, 2 - No Good, 3 - Error 
            logCategory = 1;                                                                                     // 1 - Info, 2 - Warning, 3 - Error, 4 - Critical
            subject = "One result returned";
            _ = await UpdateOrganizationCreateIntegrationLog(_appUserService, _d365webapiservice, organizationId, goodStandingStatus, subject, logCategory, message, externalService);

            // Handling Standing History
            var goodStandingStatusYN = searchResult.searchResults.results.First().goodStanding ? 1 : 0;          // 0 - No, 1 - Yes 
            _ = await CreateUpdateStandingHistory(_appUserService, _d365webapiservice, organization, goodStandingStatusYN);

            if (goodStandingStatusYN == 0) { await SendNoGoodStandingNotification(_appUserService, _d365webapiservice, organization, _notificationSettings); };
        }

        return await Task.FromResult(DateTime.Now);
    }

    private async Task<JsonObject> UpdateOrganizationCreateIntegrationLog(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, string organizationId, int goodStandingStatus, string subject, int category, string message, string externalService, bool statusUpdate = true)
    {
        var statement = $"accounts({organizationId})";
        var payload = new JsonObject {
                { "ofm_good_standing_status", goodStandingStatus},
                { "ofm_good_standing_validated_on", DateTime.UtcNow }
        };

        if (!statusUpdate)
        {
            payload.Remove("ofm_good_standing_status");
        }

        var requestBody = JsonSerializer.Serialize(payload);

        var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

        if (!patchResponse.IsSuccessStatusCode)
        {
            var responseBody = await patchResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to patch GoodStanding status on organization {organizationId} with the server error {responseBody}", organizationId, responseBody.CleanLog());

            return await Task.FromResult(ProcessResult.Failure(ProcessId, [responseBody], 0, 0).SimpleProcessResult);
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
        _ = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody2);

        return await Task.FromResult(ProcessResult.Completed(ProcessId).SimpleProcessResult);
    }

    private async Task<JsonObject> CreateUpdateStandingHistory(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, D365Organization_Account organization, int goodStandingStatusYN)
    {
        _organizationId = organization?.accountid;
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
                                { "ofm_organization@odata.bind", $"/accounts({organization?.accountid})"}
                            };
            var requestBody = JsonSerializer.Serialize(payload);
            _ = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody);
        }

        if (deserializedData.Count >= 1)                // Records found
        {
            var standingHistoryId = deserializedData.First().ofm_standing_historyid;
            var goodStandingStatus_History = deserializedData.First().ofm_good_standing_status;
            var counter = deserializedData.First().ofm_no_counter;
            DateTime startDate = (DateTime)deserializedData.First().ofm_start_date;

            if (Equals(goodStandingStatus_History, goodStandingStatusYN))                                      // Case 2. open --> update validated_On
            {
                DateTime validatedon = DateTime.Now;
                var noduration = validatedon - startDate.Date;
                // Operation - update the existing record
                var statement = $"ofm_standing_histories({standingHistoryId})";
                var payload = new JsonObject {
                                //{ "ofm_good_standing_status", 0 },                                                 
                                //{ "ofm_start_date", startDate},
                                //{ "ofm_end_date", endDate },
                                //{ "ofm_duration", duration.Days },
                                //{ "statecode", 1 },                                                                 
                                //{ "statuscode", 2 },                                                                
                                { "ofm_validated_on",DateTime.UtcNow}
                            };
                //Counter to create assisstance request
                if (goodStandingStatusYN == 0) { payload.Add("ofm_no_counter", noduration.Days.ToString()); }
                var requestBody = JsonSerializer.Serialize(payload);
                var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

                var tempFacilitiesList = _allFacilitiesWithActiveFundingData?
                    .Where(c => c["_parentaccountid_value"].ToString() == organization?.accountid)
                    .GroupBy(g => g["_ofm_primarycontact_value"])
                    .Select(s => s.First())
                    .ToList();

                if (tempFacilitiesList?.Count > 0 && goodStandingStatusYN == 0 && noduration.Days >= _BCRegistrySettings.NoDuration)
                {
                    _ = await CreateTaskForNotGoodStandingFrom90Days(_appUserService, _d365webapiservice, organization, standingHistoryId);
                }
            }
            else
            {
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
                                { "ofm_validated_on", DateTime.UtcNow}
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
                                { "ofm_organization@odata.bind", $"/accounts({organization.accountid})"}
                            };
                var requestBody2 = JsonSerializer.Serialize(payload2);
                _ = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody2);
            }
        }

        return await Task.FromResult(ProcessResult.Completed(ProcessId).SimpleProcessResult);
    }

    /// <summary>
    /// if the org is in not good standing => Create a task for more than 90 days
    /// </summary>
    /// <param name="appUserService"></param>
    /// <param name="d365WebApiService"></param>
    /// <param name="organization"></param>
    /// <param name="ofm_standing_historyid"></param>
    /// <returns></returns>
    private async Task<JsonObject> CreateTaskForNotGoodStandingFrom90Days(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, D365Organization_Account organization, string ofm_standing_historyid)
    {
        _ofm_standing_historyid = ofm_standing_historyid;
        var localStandingHistoryData = await GetRecordsDataAsync(RecordstoCreateTaskUri);
        var deserializedStandingHistoryData = JsonSerializer.Deserialize<List<D365StandingHistory>>(localStandingHistoryData.Data.ToString());

        if (deserializedStandingHistoryData?.Count <= 0)
        {
            var entitySetName = "tasks";                                                  // Case 3.2 create new record --> open (active)
            var assistanceReq = new JsonObject {
                                     { "subject", _BCRegistrySettings.TaskActivity.subject + organization.name },
                                     {"regardingobjectid_account@odata.bind", "/accounts("+organization.accountid+")"},
                                     { "description",_BCRegistrySettings.TaskActivity.description },
                                     {"ofm_process_responsible", _BCRegistrySettings.batchtaskprocess }
                                };
            var requestBody = JsonSerializer.Serialize(assistanceReq);
            _ = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody);
        }

        return await Task.FromResult(ProcessResult.Completed(ProcessId).SimpleProcessResult);
    }

    /// <summary>
    /// Send a Notification to the provider if the prganization is not in good standing
    /// </summary>
    /// <param name="appUserService"></param>
    /// <param name="d365WebApiService"></param>
    /// <param name="organization"></param>
    /// <returns></returns>
    private async Task<JsonObject> SendNoGoodStandingNotification(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, D365Organization_Account organization, NotificationSettings notificationSettings)
    {
        var sendList = _allFacilitiesWithActiveFundingData?.Where(c => c["_parentaccountid_value"].ToString() == organization?.accountid)
            .GroupBy(g => g["_ofm_primarycontact_value"])
            .Select(s => s.First())
            .ToList();

        if (sendList.Count > 0)
        {
            var distinctRecords = new Dictionary<string, JsonNode>();
            foreach (var node in sendList)
            {
                if (!distinctRecords.ContainsKey(node["_ofm_primarycontact_value"].ToString()))
                {
                    distinctRecords[node["_ofm_primarycontact_value"].ToString()] = node;
                }
            }

            IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
            _GScommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.ActionRequired)
                                                     .Select(s => s.ofm_communication_typeid).FirstOrDefault();

            var templateData = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 400).TemplateNumber);
            var serializedtemplateData = JsonSerializer.Deserialize<List<D365Template>>(templateData.Data.ToString());
            string? subject = serializedtemplateData?.Select(s => s.subjectsafehtml).FirstOrDefault();
            string? emaildescription = serializedtemplateData?.Select(sh => sh.safehtml).FirstOrDefault();
            List<Guid> recipientsList = new List<Guid>();
            // add contact safelist check code
            if (_notificationSettings.EmailSafeList.Enable &&
                !_notificationSettings.EmailSafeList.Recipients.Any(x => x.Equals(organization.primarycontactemail?.Trim(';'), StringComparison.CurrentCultureIgnoreCase)))
            {
                recipientsList.Add(new Guid(_notificationSettings.EmailSafeList.DefaultContactId));
            }
            else
            {
                recipientsList.Add(new Guid($"{organization?._primarycontactid_value}"));
            }

            foreach (var record in distinctRecords)
            {
                if (record.Value["_ofm_primarycontact_value"].ToString() != organization?._primarycontactid_value.ToString())
                {
                    if (_notificationSettings.EmailSafeList.Enable &&
                        !_notificationSettings.EmailSafeList.Recipients.Any(x => x.Equals(record.Value["contact.emailaddress1"]?.ToString().Trim(';'), StringComparison.CurrentCultureIgnoreCase)))
                    {
                        recipientsList.Add(new Guid(_notificationSettings.EmailSafeList.DefaultContactId));
                    }
                    else
                    {
                        recipientsList.Add(new Guid($"{record.Value?["_ofm_primarycontact_value"]}"));
                    }
                }
            }
            recipientsList = recipientsList.Distinct().ToList();
            await _emailRepository!.CreateAndUpdateEmail(subject, emaildescription, recipientsList, new Guid(_notificationSettings.DefaultSenderId), _GScommunicationType, appUserService, d365WebApiService, 400);
        }

        return await Task.FromResult(ProcessResult.Completed(ProcessId).SimpleProcessResult);
    }
}