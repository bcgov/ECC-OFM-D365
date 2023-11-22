using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P200EmailReminderProvider : ID365ProcessProvider
{
    private readonly NotificationSettings _notificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;

    public P200EmailReminderProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Email.SendEmailRemindersId;
    public string ProcessName => Setup.Process.Email.SendEmailRemindersName;
    public string RequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            var fetchXml = $"""
                <fetch distinct="true" no-lock="true">
                <entity name="email">
                  <attribute name="subject" />
                  <attribute name="lastopenedtime" />
                  <attribute name="torecipients" />
                  <attribute name="emailsender" />
                  <attribute name="sender" />
                  <attribute name="submittedby" />
                  <attribute name="statecode" />
                  <attribute name="statuscode" />
                  <attribute name="ofm_communication_type" />
                  <attribute name="emailreminderexpirytime" />
                  <attribute name="regardingobjectid" />
                  <attribute name="ofm_sent_on" />
                  <filter type="and">
                    <condition attribute="lastopenedtime" operator="null" />
                    <condition attribute="torecipients" operator="not-null" />
                    <condition attribute="ofm_expiry_time" operator="next-x-days" value="29" />
                    <condition attribute="ofm_communication_type" operator="in" uitype="ofm_communication_type">
                        <value>{_notificationSettings.CommunicationTypeOptions.ActionRequired}</value>
                        <value>{_notificationSettings.CommunicationTypeOptions.DebtLetter}</value>
                        <value>{_notificationSettings.CommunicationTypeOptions.FundingAgreement}</value>
                        <value>{_notificationSettings.CommunicationTypeOptions.Information}</value>
                        <value>{_notificationSettings.CommunicationTypeOptions.Reminder}</value>
                      </condition>
                    </filter>
                   <link-entity name="activityparty" from="activityid" to="activityid" link-type="inner" alias="To">
                       <attribute name="participationtypemask" />
                       <attribute name="partyid" />
                       <filter>
                         <condition attribute="participationtypemask" operator="eq" value="2" />
                       </filter>
                    </link-entity>
                  </entity>
                </fetch>
                """;

            //var requestUri = $"""
            //                  emails?fetchXml={WebUtility.UrlEncode(fetchXml)}
            //                  """;

            var requestUri = $"""
                                emails?$select=subject,lastopenedtime,torecipients,_emailsender_value,sender,submittedby,statecode,statuscode,_ofm_communication_type_value,emailreminderexpirytime,_regardingobjectid_value,ofm_sent_on&$expand=email_activity_parties($select=participationtypemask,_partyid_value;$filter=(participationtypemask eq 2))&$filter=(lastopenedtime eq null and torecipients ne null and Microsoft.Dynamics.CRM.NextXDays(PropertyName='ofm_expiry_time',PropertyValue=29) and Microsoft.Dynamics.CRM.In(PropertyName='ofm_communication_type',PropertyValues=['{_notificationSettings.CommunicationTypeOptions.ActionRequired}','{_notificationSettings.CommunicationTypeOptions.DebtLetter}','{_notificationSettings.CommunicationTypeOptions.FundingAgreement}', '{_notificationSettings.CommunicationTypeOptions.Information}', '{_notificationSettings.CommunicationTypeOptions.Reminder}']))
                             """;

            return requestUri;
        }
    }

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P200EmailReminderProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZNoticationAppUser, RequestUri);

            if (!response.IsSuccessStatusCode) {
                var responseBody = await response.Content.ReadAsStringAsync();  
                _logger.LogError(CustomLogEvent.Process, "Failed to query email reminders with the server error {responseBody}", responseBody);

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No emails found with query {requestUri}", RequestUri);
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString());

        return await Task.FromResult(_data);
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _logger.LogDebug(CustomLogEvent.Process, "Getting due emails with query {requestUri}", RequestUri);

        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetData();

        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<D365Email>>(localData.Data.ToJsonString());

        var dueEmails = serializedData!.Where(e => e.IsCompleted && (e.IsNewAndUnread || IsUnreadReminderRequired(e))).ToList();

        // Send only one email per contact
        var uniqueContacts = dueEmails.GroupBy(e => e.torecipients).ToList();

        if (uniqueContacts.Count == 0) {

            _logger.LogInformation(CustomLogEvent.Process, "Send email reminders completed. No unique contacts found.");
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        #region  Step 1: Create the email reminders as Completed-Pending Send

        JsonArray recipientsList = new() { };

        uniqueContacts.ForEach(contact =>
        {
            var contactId = contact.ElementAt(0).email_activity_parties?.First()._partyid_value?.Replace("\\u0027", "'");
            recipientsList.Add($"contacts({contactId})");
        });

        var contentBody = new JsonObject {
                { "TemplateId" , _notificationSettings.EmailTemplates.First(t=>t.TemplateNumber == 201).TemplateId}, //Action Required: A communication regarding OFM funding requires your attention.
                { "Sender" , new JsonObject {
                                        { "@odata.type" , "Microsoft.Dynamics.CRM.systemuser"},
                                        { "systemuserid",_notificationSettings.DefaultSenderId}
                                }
                },
                { "Recipients" , recipientsList },
                { "Regarding" , new JsonObject {
                                        { "@odata.type" , "Microsoft.Dynamics.CRM.systemuser"},
                                        { "systemuserid",_notificationSettings.DefaultSenderId}
                                }
                } // Regarding is a required parameter.Temporarily set it to the PA service account, but this will not set the Regarding on the new records
        };

        HttpResponseMessage bulkEmailsResponse = await d365WebApiService.SendBulkEmailTemplateMessageAsync(appUserService.AZNoticationAppUser, contentBody, null);

        if (!bulkEmailsResponse.IsSuccessStatusCode)
        {
            var responseBody = await bulkEmailsResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create email reminder records with error: {error}", responseBody);

            return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, localData.Data.AsArray().Count).SimpleProcessResult;
        }

        _logger.LogInformation(CustomLogEvent.Process, "Total email reminders created {uniqueContacts}", uniqueContacts.Count);

        #endregion

        #region Step 2: Send the reminders (TODO)

        // Emails are created with "Completed - Pending Send" status. Step 2 will be needed if the sender is not configured for Exchange Online Mailbox.
        // Query all Draft emails created in the last 24 hours and by the OFM system user or Notification Service in the future. Must use the correct conditions to find only the Draft emails created in Step 1

        //var sendEmailBody = new JsonObject {
        //        { "IssueSend", false}
        //};

        //var sendEmailRequests = new List<HttpRequestMessage>() {
        //         new SendEmailRequest(new Guid("00000000-0000-0000-0000-000000000000"), sendEmailBody),
        //         new SendEmailRequest(new Guid("00000000-0000-0000-0000-000000000000"), sendEmailBody)
        //    };

        //var step2BatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, sendEmailRequests, null);

        var endTime = _timeProvider.GetTimestamp();

        //if (step2BatchResult.Errors.Any())
        //{
        //    var errorResult = ProcessResult.Failure(ProcessId, step2BatchResult.Errors, step2BatchResult.TotalProcessed, step2BatchResult.TotalRecords);
        //    _logger.LogError(CustomLogEvents.Process, "Failed to send email reminders with an error: {error}", JsonValue.Create(errorResult)!.ToJsonString());

        //    return errorResult.SimpleProcessResult;
        //}

        #endregion

        #region Step3:  Other email updates (TODO)

        //var emailToUpdate = new JsonObject {
        //        { "scheduledstart",DateTime.Now.ToShortDateString()},
        //        { "scheduledend",DateTime.Now.AddDays(30).ToShortDateString()},
        //        { "ofm_is_read",true },
        //        { "ofm_communication_type@odata.bind", "ofm_communication_types(00000000-0000-0000-0000-000000000000)"}
        //     };

        //var updateEmailRequests = new List<HttpRequestMessage>() {
        //         new UpdateRequest(new EntityReference("emails",new Guid("00000000-0000-0000-0000-000000000000")), emailToUpdate)
        //    };

        //var step3BatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updateEmailRequests, null);
        //if (step3BatchResult.Errors.Any())
        //{
        //    var errors = ProcessResult.Failure(ProcessId, step3BatchResult.Errors, step3BatchResult.TotalProcessed, step3BatchResult.TotalRecords);
        //    _logger.LogError(CustomLogEvents.Process, "Failed to send email reminders with an error: {error}", JsonValue.Create(errors)!.ToJsonString());

        //    return errors.SimpleProcessResult;
        //}

        #endregion   

        var result = ProcessResult.Success(ProcessId, uniqueContacts.Count);
        _logger.LogInformation(CustomLogEvent.Process, "Send email reminders process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, JsonValue.Create(result)!.ToJsonString());

        return result.SimpleProcessResult;
    }

    #region Local Validation Code

    private bool IsUnreadReminderRequired(D365Email email)
    {
        if (email == null)
            return false;

        if (email.ofm_sent_on is null || email.lastopenedtime is not null)
            return false;

        if (email.lastopenedtime is null)
        {
            var firstReminderInDays = _notificationSettings.UnreadEmailOptions.FirstReminderInDays;
            var secondReminderInDays = _notificationSettings.UnreadEmailOptions.SecondReminderInDays;
            var thirdReminderInDays = _notificationSettings.UnreadEmailOptions.ThirdReminderInDays;

            var communicationType = email._ofm_communication_type_value;

            if ((email.ofm_sent_on.Value.Date.AddDays(firstReminderInDays).Equals(DateTime.Now.Date) ||
                email.ofm_sent_on.Value.Date.AddDays(secondReminderInDays).Equals(DateTime.Now.Date) ||
                email.ofm_sent_on.Value.Date.AddDays(thirdReminderInDays).Equals(DateTime.Now.Date)
                ) && (communicationType.Equals(_notificationSettings.CommunicationTypeOptions.ActionRequired) ||
                communicationType.Equals(_notificationSettings.CommunicationTypeOptions.DebtLetter) ||
                communicationType.Equals(_notificationSettings.CommunicationTypeOptions.FundingAgreement)))
                return true;
        }

        return false;
    }

    #endregion
}