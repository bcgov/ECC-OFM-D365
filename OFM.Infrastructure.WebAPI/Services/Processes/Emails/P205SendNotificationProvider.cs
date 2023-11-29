using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json;
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
    private string _requestUri = string.Empty;

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
            // Use Paging Cookie for large datasets
            // Use exact filters to reduce the matching data and enhance performance
            if (string.IsNullOrEmpty(_requestUri))
            {
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
                        <condition attribute="listid" operator="eq" value="{_processParams.Notification.MarketingListId}" />
                      </filter>
                    </link-entity>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                            contacts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

                _requestUri = requestUri.CleanCRLF();
            }

            return _requestUri;
        }
    }

    private string EmailsToUpdateRequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            var fetchXml = $"""
                <fetch distinct="true" no-lock="true">
                  <entity name="email">
                    <attribute name="activityid" />
                    <attribute name="templateid" />
                    <attribute name="subject" />
                    <attribute name="emailreminderstatus" />
                    <attribute name="statecode" />
                    <attribute name="statuscode" />
                    <attribute name="ofm_communication_type" />
                    <attribute name="emailremindertype" />
                    <attribute name="emailsender" />
                    <attribute name="messageid" />
                    <attribute name="ofm_due_date" />
                    <attribute name="ofm_expiry_time" />
                    <attribute name="ofm_sent_on" />
                    <attribute name="sender" />
                    <attribute name="senton" />
                    <attribute name="submittedby" />
                    <attribute name="torecipients" />
                    <attribute name="createdonbehalfby" />
                    <attribute name="createdby" />
                    <attribute name="createdon" />
                    <attribute name="isworkflowcreated" />
                    <filter>
                      <condition attribute="ofm_due_date" operator="null" />
                      <condition attribute="ofm_communication_type" operator="not-null" />
                      <condition attribute="createdon" operator="today" />
                      <!--<condition attribute="createdonbehalfby" operator="eq" value="{_processParams.Notification.SenderId}" uitype="systemuser" />-->                     
                    </filter>
                  </entity>
                </fetch>
                """;

            var requestUri = $"""
                            emails?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P205SendNotificationProvider));

        if (_data is null && _processParams is not null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting active contacts from a marketinglist with query {requestUri}", RequestUri.CleanLog());

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query members on the contact list with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No members on the contact list found with query {requestUri}", RequestUri.CleanLog());
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data?.Data.ToString().CleanLog());
        }

        return await Task.FromResult(_data!);
    }

    private async Task<ProcessData> GetDataToUpdate()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetDataToUpdate");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, EmailsToUpdateRequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query pending emails to update with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No pending emails on the contact list found with query {requestUri}", EmailsToUpdateRequestUri.CleanLog());
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

        var localData = await GetData();

        var serializedData = JsonSerializer.Deserialize<List<D365Contact>>(localData.Data.ToString());

        #region  Step 1: Create the email notifications as Completed - Pending Send

        JsonArray recipientsList = new() { };

        serializedData?.ForEach(contact =>
        {
            recipientsList.Add($"contacts({contact.contactid})");
        });

        if (_processParams.Notification.TemplateId is not null) // Send notifications using template by default
        {
            var contentBody = new JsonObject {
                { "TemplateId" , _processParams.Notification.TemplateId},
                { "Sender" , new JsonObject {
                                        { "@odata.type" , "Microsoft.Dynamics.CRM.systemuser"},
                                        { "systemuserid",_processParams.Notification.SenderId}
                                }
                },
                { "Recipients" , recipientsList },
                { "Regarding" , new JsonObject {
                                        { "@odata.type" , "Microsoft.Dynamics.CRM.systemuser"},
                                        { "systemuserid",_processParams.Notification.SenderId}
                                }
                } // Regarding is a required parameter.
            };

            HttpResponseMessage bulkEmailsResponse = await d365WebApiService.SendBulkEmailTemplateMessageAsync(appUserService.AZSystemAppUser, contentBody, _processParams.Notification.SenderId);

            if (!bulkEmailsResponse.IsSuccessStatusCode)
            {
                var responseBody = await bulkEmailsResponse.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to create email notification records with error: {error}", responseBody.CleanLog());

                return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, localData.Data.AsArray().Count).SimpleProcessResult;
            }

            _logger.LogInformation(CustomLogEvent.Process, "Total notifications created {count}", serializedData?.Count);
        }
        else // Send notification by provided user email content
        {
            List<HttpRequestMessage> sendCreateEmailRequests = new() { };
            serializedData?.ForEach(contact =>
            {
                sendCreateEmailRequests.Add(new CreateRequest("emails",
                    new JsonObject(){
                        {"subject",_processParams.Notification.Subject },
                        {"description",_processParams.Notification.EmailBody },
                        {"email_activity_parties", new JsonArray(){
                            new JsonObject
                            {
                                {"partyid_systemuser@odata.bind", $"/systemusers({_processParams.Notification.SenderId})"},
                                { "participationtypemask", 1 } //From Email
                            },
                            new JsonObject
                            {
                                { "partyid_contact@odata.bind", $"/contacts({contact.contactid})" },
                                { "participationtypemask",   2 } //To Email                             
                            }
                        }}
                    }));
            });

            var sendEmailBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, sendCreateEmailRequests, null);

            if (sendEmailBatchResult.Errors.Any())
            {
                var sendNotificationError = ProcessResult.Failure(ProcessId, sendEmailBatchResult.Errors, sendEmailBatchResult.TotalProcessed, sendEmailBatchResult.TotalRecords);
                _logger.LogError(CustomLogEvent.Process, "Failed to send notifications with an error: {error}", JsonValue.Create(sendNotificationError)!.ToString());

                return sendNotificationError.SimpleProcessResult;
            }
        }

        #endregion

        #region Step 2: Update emails with due date, communication type etc.

        var localDataStep2 = await GetDataToUpdate();
        var serializedDataStep2 = JsonSerializer.Deserialize<List<D365Email>>(localDataStep2.Data.ToString());

        var updateEmailRequests = new List<HttpRequestMessage>() { };
        serializedDataStep2.ForEach(email =>
        {
            var emailToUpdate = new JsonObject {
                { "ofm_sent_on",DateTime.UtcNow },
                { "ofm_due_date",_processParams.Notification.DueDate },
                { "ofm_communication_type_Email@odata.bind", $"/ofm_communication_types({_processParams.Notification.CommunicationTypeId})"}
             };

            updateEmailRequests.Add(new UpdateRequest(new EntityReference("emails", new Guid(email.activityid)), emailToUpdate));
        });

        var step2BatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updateEmailRequests, null);
        if (step2BatchResult.Errors.Any())
        {
            var errors = ProcessResult.Failure(ProcessId, step2BatchResult.Errors, step2BatchResult.TotalProcessed, step2BatchResult.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to update email notifications with an error: {error}", JsonValue.Create(errors)!.ToString());

            return errors.SimpleProcessResult;
        }

        #endregion

        #region Step 3: Send the notifications

        // Emails are created with "Completed - Pending Send" status. Step 3 is needed to send emails via GC-Notify or Exchange Online

        #endregion

        var result = ProcessResult.Success(ProcessId, serializedData!.Count);

        var endTime = _timeProvider.GetTimestamp();

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = JsonSerializer.Serialize(result, serializeOptions);

        //_logger.LogInformation(CustomLogEvent.Process, "Send Notification process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, JsonValue.Create(result)!.ToString().CleanLog());
        _logger.LogInformation(CustomLogEvent.Process, "Send Notification process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, json);

        return result.SimpleProcessResult;
    }
}