﻿using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;

public class P400VerifyGoodStandingProvider : ID365ProcessProvider
{
    private readonly BCRegistrySettings _BCRegistrySettings;
    private readonly NotificationSettings _NotificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly IEmailRepository _emailRepository;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _organizationId;
    private string _ofm_standing_historyid;
    private string? _GScommunicationType;
    BcRegistryService _bcregService;

    public P400VerifyGoodStandingProvider(IOptionsSnapshot<ExternalServices> ApiKeyBCRegistry, IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IEmailRepository emailRepository)
    {
        _BCRegistrySettings = ApiKeyBCRegistry.Value.BCRegistryApi;
        _NotificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
        _emailRepository = emailRepository;
    }

    public Int16 ProcessId => Setup.Process.ProviderProfiles.VerifyGoodStandingId;
    public string ProcessName => Setup.Process.ProviderProfiles.VerifyGoodStandingName;

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
                        <attribute name="ofm_bypass_bc_registry_good_standing" />
                        <attribute name="primarycontactid"/>
                        <attribute name="statecode" />
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
                        <attribute name="ofm_duration" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_no_counter" />
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

    public string RecordstoSendNotificationUri
    {
        // get all Facilities which  Funding Status is Active 
        get
        {
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="account">
                        <attribute name="name" />
                        <attribute name="telephone1" />
                        <attribute name="accountid" />
                        <attribute name="ofm_primarycontact" />
                        <order attribute="name" descending="false" />
                        <filter type="and">
                          <condition attribute="parentaccountid" operator="eq" value="{_processParams?.Organization?.organizationId}" />
                        </filter>
                        <link-entity name="ofm_application" from="ofm_facility" to="accountid" link-type="inner" alias="ofm_app">
                          <attribute name="ofm_application" />
                          <attribute name="ofm_applicationid" />
                          <link-entity name="ofm_funding" from="ofm_application" to="ofm_applicationid" link-type="inner" alias="funding">
                            <attribute name="ofm_funding_number" />
                            <attribute name="ofm_fundingid" />
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
    // OFM ticket 4631
    //public string RecordstoSendNotificationUri
    //{
    //    get
    //    {
    //        var fetchXml = $"""
    //                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
    //                  <entity name="account">
    //                    <attribute name="name" />
    //                     <attribute name="telephone1" />
    //                    <attribute name="accountid" />
    //                    <attribute name="ofm_primarycontact" />
    //                    <order attribute="name" descending="false" />
    //                    <filter type="and">
    //                      <condition attribute="parentaccountid" operator="eq" uitype="account" value="{_processParams?.Organization?.organizationId}" />
    //                    </filter>
    //                  </entity>
    //                </fetch>
    //                """;

    //        var requestUri = $"""
    //                     accounts?fetchXml={WebUtility.UrlEncode(fetchXml)}
    //                     """;

    //        return requestUri;
    //    }
    //}
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

    //private async Task<ProcessData> GetRecordToNotifyDataAsync()
    //{
    //    _logger.LogDebug(CustomLogEvent.Process, "GetRecordToNotifyDataAsync");

    //    var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RecordstoSendNotificationUri);

    //    if (!response.IsSuccessStatusCode)
    //    {
    //        var responseBody = await response.Content.ReadAsStringAsync();
    //        _logger.LogError(CustomLogEvent.Process, "Failed to query Standing History records for task creation {responseBody}", responseBody.CleanLog());

    //        return await Task.FromResult(new ProcessData(string.Empty));
    //    }

    //    var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

    //    JsonNode d365Result = string.Empty;
    //    if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
    //    {
    //        if (currentValue?.AsArray().Count == 0)
    //        {
    //            _logger.LogInformation(CustomLogEvent.Process, "No Contact email found with query {requestUri}", StandingHistoryRequestUri.CleanLog());
    //        }
    //        d365Result = currentValue!;
    //    }

    //    _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

    //    return await Task.FromResult(new ProcessData(d365Result));
    //}

    #endregion


    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
        var externalService = "BC Registries";

        var startTime = _timeProvider.GetTimestamp();
        var localData = await GetDataAsync();

        var deserializedData = JsonSerializer.Deserialize<List<D365Organization_Account>>(localData.Data.ToString());

        deserializedData?.Where(c => c.ofm_bypass_bc_registry_good_standing != true).ToList().ForEach(async organization =>
        {
            string organizationId = organization.accountid;
            string legalName = organization.name;
            string incorporationNumber = organization.ofm_incorporation_number;
            string businessNumber = organization.ofm_business_number;
            _bcregService = new BcRegistryService(_BCRegistrySettings);
            var response = _bcregService.GetRegistryDataAsync(organizationId, legalName, incorporationNumber);

         
            if (response.Result.IsSuccessStatusCode)
            {
                var responseBody = await response.Result.Content.ReadAsStringAsync();

                BCRegistrySearchResult? searchResult = await response.Result.Content.ReadFromJsonAsync<BCRegistrySearchResult>();

                // Organization - Update
                var goodStandingStatus = 3;                    // 1 - Good, 2 - No Good, 3 - Error 

                // Integration Log - Create
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
                    await CreateUpdateStandingHistory(_appUserService, _d365webapiservice, organization, goodStandingStatusYN);
                    if (goodStandingStatusYN == 0)
                    {
                        await SendNotification(_appUserService, _d365webapiservice, organization, _NotificationSettings);
                    };
                }
            }
            else
            {
                var responseBody = await response.Result.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Unable to call BC Registries API with an error {responseBody}.", responseBody.CleanLog());

                var standingStatusUpdate = (response.Result.StatusCode != HttpStatusCode.InternalServerError);
                var subject = "Unable to call BC Registries API with an error.";
                await UpdateOrganizationCreateIntegrationLog(_appUserService, _d365webapiservice, organizationId, (int)ofm_good_standing_status.IntegrationError, subject, (int)ecc_integration_log_category.Error, responseBody.CleanLog(), externalService, statusUpdate: standingStatusUpdate);
            }
        });

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
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

    private async Task<JsonObject> CreateUpdateStandingHistory(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, D365Organization_Account organization, int goodStandingStatusYN)
    {
        _organizationId = organization.accountid;

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
                                { "ofm_organization@odata.bind", $"/accounts({organization.accountid})"}
                            };
            var requestBody = JsonSerializer.Serialize(payload);
            var CreateResponse = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody);
        }

        if (deserializedData.Count >= 1)                // Records found
        {
            var standingHistoryId = deserializedData.First().ofm_standing_historyid;
            var goodStandingStatus_History = deserializedData.First().ofm_good_standing_status;
            var counter = deserializedData.First().ofm_no_counter;
            DateTime startDate = (DateTime)deserializedData.First().ofm_start_date;
            //var organizationId_History = deserializedData.First()._ofm_organization_value;

            if (Equals(goodStandingStatus_History, goodStandingStatusYN))                                      // Case 2. open --> update validated_On
            {
                DateTime validatedon = DateTime.Now;
                TimeSpan noduration = validatedon - startDate.Date;
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
                if (goodStandingStatusYN == 0) { payload.Add("ofm_no_counter", noduration.Days.ToString()); }
                var requestBody = JsonSerializer.Serialize(payload);
                var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

                if (goodStandingStatusYN == 0 && noduration.Days >= _BCRegistrySettings.NoDuration)
                {   //Handling task creation if rcord is in not good standing fro 90 days
                    // Tikcet 4631 add condition should have Active Funding
                    var facilitiesWithActiveFunding = await GetRecordsDataAsync(RecordstoSendNotificationUri);
                    var deserializedFacilities = JsonSerializer.Deserialize<List<D365Organization_Account>>(facilitiesWithActiveFunding.Data.ToString());
                    if (deserializedFacilities.Count > 0)
                    {
                        await CreateTask(_appUserService, _d365webapiservice, organization, standingHistoryId);
                    }
                }
            }
            else
            {
                //DateTime startDate = (DateTime)deserializedData.First().ofm_start_date;
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
                                { "ofm_organization@odata.bind", $"/accounts({organization.accountid})"}
                            };
                var requestBody2 = JsonSerializer.Serialize(payload2);
                var CreateResponse2 = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody2);
            }
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    // Create task if org is in not good standing for more than 90 days
    private async Task<JsonObject> CreateTask(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, D365Organization_Account organization, string ofm_standing_historyid)
    {
        // _organizationId = organizationId;
        _ofm_standing_historyid = ofm_standing_historyid;
        var localData = await GetRecordsDataAsync(RecordstoCreateTaskUri);

        var deserializedData = JsonSerializer.Deserialize<List<D365StandingHistory>>(localData.Data.ToString());


        if (deserializedData.Count <= 0)                // Records found
        {

            // Operation - Create new record
            var entitySetName = "tasks";                                                  // Case 3.2 create new record --> open (active)
            var assistanceReq = new JsonObject {
                                 { "subject", _BCRegistrySettings.TaskActivity.subject + organization.name },
                                 {"regardingobjectid_account@odata.bind", "/accounts("+organization.accountid+")"},
                                 { "description",_BCRegistrySettings.TaskActivity.description },
                                 {"ofm_process_responsible",_BCRegistrySettings.singletaskprocess }
                                  };
            // create assistance request
            var requestBody = JsonSerializer.Serialize(assistanceReq);
            var CreateResponse = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody);


        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    //Send Notification if Record is not in good standing
    private async Task<JsonObject> SendNotification(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, D365Organization_Account organization, NotificationSettings notificationSettings)
    {
        // _organizationId = organizationId;
        var _notificationSettings = notificationSettings;
        var localData = await GetRecordsDataAsync(RecordstoSendNotificationUri);
        var deserializedData = JsonSerializer.Deserialize<List<D365Organization_Account>>(localData.Data.ToString());
        if (deserializedData == null || deserializedData.Count == 0)
        {
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }
        IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
        _GScommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _NotificationSettings.CommunicationTypes.ActionRequired)
                                                                     .Select(s => s.ofm_communication_typeid).FirstOrDefault();
        var distinctDeserializedData = deserializedData
           .GroupBy(g => g.accountid)
           .Select(s => s.First())
           .ToList();
        var templateData = await _emailRepository.GetTemplateDataAsync(_NotificationSettings.EmailTemplates.First(t => t.TemplateNumber == 400).TemplateNumber);
        var serializedtemplateData = JsonSerializer.Deserialize<List<D365Template>>(templateData.Data.ToString());
        string? subject = _emailRepository.StripHTML(serializedtemplateData?.Select(s => s.subjectsafehtml).FirstOrDefault());
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

        distinctDeserializedData?.Where(c => c._ofm_primarycontact_value != organization?._primarycontactid_value).ToList().ForEach(recipient =>
        {
            if (_notificationSettings.EmailSafeList.Enable &&
                !_notificationSettings.EmailSafeList.Recipients.Any(x => x.Equals(recipient.primarycontactemail?.Trim(';'), StringComparison.CurrentCultureIgnoreCase)))
            {
                recipientsList.Add(new Guid(_notificationSettings.EmailSafeList.DefaultContactId));
            }
            else
            {
                recipientsList.Add(new Guid($"{recipient?._ofm_primarycontact_value}"));
            }
        });
        recipientsList = recipientsList.Distinct().ToList();
        await _emailRepository!.CreateAndUpdateEmail(subject, emaildescription, recipientsList, new Guid(_NotificationSettings.DefaultSenderId), _GScommunicationType, appUserService, d365WebApiService, 400);
        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }


}