using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P205SendNotificationProvider : ID365ProcessProvider
{
    private readonly NotificationSettings _notificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P205SendNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Email.SendNotificationsId;
    public string ProcessName => Setup.Process.Email.SendNotificationsName;
    public string RequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            var fetchXml = $"""
                <fetch distinct="true" no-lock="true">
                  <entity name="contact">
                    <attribute name="ccof_username" />
                    <attribute name="ccof_userid" />
                    <attribute name="ofm_first_name" />
                    <attribute name="ofm_last_name" />
                    <attribute name="contactid" />
                    <attribute name="donotbulkemail" />
                    <attribute name="donotpostalmail" />
                    <attribute name="emailaddress1" />
                    <attribute name="statecode" />
                    <attribute name="statuscode" />
                    <link-entity name="listmember" from="entityid" to="contactid" link-type="inner" alias="clist" intersect="true">
                      <attribute name="listid" />
                      <attribute name="name" />
                      <filter>
                        <condition attribute="listid" operator="eq" value="{_processParams?.MarketingListId}" />
                      </filter>
                    </link-entity>
                  </entity>
                </fetch>
                """;

            var requestUri = $"""
                            contacts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri;
        }
    }

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P205SendNotificationProvider));

        if (_data is null && _processParams is not null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZNoticationAppUser, RequestUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query members on the contact list with the server error {responseBody}", responseBody);

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No members on the contact list found with query {requestUri}", RequestUri);
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data?.Data.ToJsonString());

        return await Task.FromResult(_data!);
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;

        var startTime = _timeProvider.GetTimestamp();

        //Validate _processParams
        _logger.LogDebug(CustomLogEvent.Process, "Getting active contacts from a marketinglist with query {requestUri}", RequestUri);
        var localData = await GetData();

        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<D365Contact>>(localData.Data.ToJsonString());

        #region  Step 1: Create the email reminders as Completed - Pending Send

        JsonArray recipientsList = new() { };

        serializedData?.ForEach(contact =>
        {
            recipientsList.Add($"contacts({contact.contactid})");
        });

        if (_processParams.TemplateId is not null)
        {
            var contentBody = new JsonObject {
                { "TemplateId" , _processParams.TemplateId},
                { "Sender" , new JsonObject {
                                        { "@odata.type" , "Microsoft.Dynamics.CRM.systemuser"},
                                        { "systemuserid",_processParams.SenderId}
                                }
                },
                { "Recipients" , recipientsList },
                { "Regarding" , new JsonObject {
                                        { "@odata.type" , "Microsoft.Dynamics.CRM.systemuser"},
                                        { "systemuserid",_processParams.SenderId}
                                }
                } // Regarding is a required parameter.
            };

            HttpResponseMessage bulkEmailsResponse = await d365WebApiService.SendBulkEmailTemplateMessageAsync(appUserService.AZNoticationAppUser, contentBody, null);

            if (!bulkEmailsResponse.IsSuccessStatusCode)
            {
                var responseBody = await bulkEmailsResponse.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to create email notification records with error: {error}", responseBody);

                return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, localData.Data.AsArray().Count).SimpleProcessResult;
            }

            _logger.LogInformation(CustomLogEvent.Process, "Total notifications created {count}", serializedData?.Count);
        }
        else
        {
            List<HttpRequestMessage> sendCreateEmailRequests = new() { };
            serializedData?.ForEach(contact =>
            {
                sendCreateEmailRequests.Add(new CreateRequest("emails",
                    new JsonObject(){
                        {"subject",_processParams.Subject },
                        {"description",_processParams.EmailBody }
                    }));
            });

            var sendEmailBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZNoticationAppUser, sendCreateEmailRequests, null);

            if (sendEmailBatchResult.Errors.Any())
            {
                var sendNotificationError = ProcessResult.Failure(ProcessId, sendEmailBatchResult.Errors, sendEmailBatchResult.TotalProcessed, sendEmailBatchResult.TotalRecords);
                _logger.LogError(CustomLogEvent.Process, "Failed to send notifications with an error: {error}", JsonValue.Create(sendNotificationError)!.ToJsonString());

                return sendNotificationError.SimpleProcessResult;
            }
        }

        #endregion

        #region Step 2: email updates

        var emailToUpdate = new JsonObject {
                { "ofm_due_date",_processParams.DueDate },
                { "ofm_communication_type@odata.bind", $"/ofm_communication_types({_processParams.CommunicationTypeId})"}  // "/ofm_communication_types(709f1093-507f-ee11-8179-000d3af4865d)"}
             };

        var updateEmailRequests = new List<HttpRequestMessage>() {
                 new UpdateRequest(new EntityReference("emails",new Guid("2c120827-2857-ee11-be6f-000d3a09d4d4")), emailToUpdate)
            };

        var step3BatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZNoticationAppUser, updateEmailRequests, null);
        if (step3BatchResult.Errors.Any())
        {
            var errors = ProcessResult.Failure(ProcessId, step3BatchResult.Errors, step3BatchResult.TotalProcessed, step3BatchResult.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to send email reminders with an error: {error}", JsonValue.Create(errors)!.ToJsonString());

            return errors.SimpleProcessResult;
        }

        #endregion   

        var result = ProcessResult.Success(ProcessId, serializedData!.Count);
        //_logger.LogInformation(CustomLogEvent.Process, "Send Notification process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, JsonValue.Create(result)!.ToJsonString());

        return result.SimpleProcessResult;
    }
}