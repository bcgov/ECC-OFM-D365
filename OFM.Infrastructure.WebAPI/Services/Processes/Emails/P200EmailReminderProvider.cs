﻿using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Net;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P200EmailReminderProvider : ID365ProcessProvider
{
    private readonly NotificationSettings _notificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly D365AuthSettings _d365AuthSettings;
    private readonly IEmailRepository _emailRepository;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessData? _assistanceRequestdata;
    private string[] _activeCommunicationTypes = [];
    private string[] _communicationTypesForUnreadReminders = [];
    private string _requestUri = string.Empty;
    private string _assistanceRequestUri = string.Empty;

    public P200EmailReminderProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, IOptionsSnapshot<D365AuthSettings> d365AuthSettings, IEmailRepository emailRepository, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _d365AuthSettings = d365AuthSettings.Value;
        _emailRepository = emailRepository;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Emails.SendEmailRemindersId;
    public string ProcessName => Setup.Process.Emails.SendEmailRemindersName;
    public string RequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            // Use Paging Cookie for large datasets
            // Use exact filters to reduce the matching data and enhance performance

            if (string.IsNullOrEmpty(_requestUri))
            {
                var communicationTypesString = _activeCommunicationTypes.Aggregate((partialPhrase, id) => $"{partialPhrase},{id}");

                //for reference only
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
                                  <attribute name="ofm_expiry_time" />
                                  <filter type="and">
                                    <condition attribute="lastopenedtime" operator="null" />
                                    <condition attribute="torecipients" operator="not-null" />
                                    <condition attribute="ofm_expiry_time" operator="next-x-days" value="29" />
                                    <condition attribute="ofm_communication_type" operator="in" uitype="ofm_communication_type">
                                        <value>00000000-0000-0000-0000-000000000000</value>
                                        <value>00000000-0000-0000-0000-000000000000</value>                                       
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

                var requestUri = $"""                                
                                emails?$select=subject,lastopenedtime,torecipients,_emailsender_value,sender,submittedby,statecode,statuscode,_ofm_communication_type_value,_regardingobjectid_value,ofm_sent_on,ofm_expiry_time
                                &$expand=email_activity_parties($select=participationtypemask,addressused,_partyid_value;$filter=participationtypemask eq 2)
                                &$filter=(lastopenedtime eq null and torecipients ne null and (Microsoft.Dynamics.CRM.NextXDays(PropertyName='ofm_expiry_time',PropertyValue=29) or Microsoft.Dynamics.CRM.Today(PropertyName='createdon')) and Microsoft.Dynamics.CRM.In(PropertyName='ofm_communication_type',PropertyValues=[{communicationTypesString}]))
                                """;

                //                var requestUri = $"""
                //emails?$select=subject,lastopenedtime,torecipients,_emailsender_value,sender,submittedby,statecode,statuscode,_ofm_communication_type_value,_regardingobjectid_value,ofm_sent_on,ofm_expiry_time
                //&$expand=email_activity_parties($select=participationtypemask,_partyid_value;$filter=(participationtypemask eq 2);$expand=_partyid_value($select=emailaddress1))
                //&$filter=(lastopenedtime eq null and torecipients ne null and(Microsoft.Dynamics.CRM.NextXDays(PropertyName = 'ofm_expiry_time', PropertyValue = 29) or Microsoft.Dynamics.CRM.Today(PropertyName = 'createdon')) and Microsoft.Dynamics.CRM.In(PropertyName='ofm_communication_type', PropertyValues=[{communicationTypesString}]))
                //""";

                _requestUri = requestUri.CleanCRLF();
            }

            return _requestUri;
        }
    }
    public string AssistanceRequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            // Use Paging Cookie for large datasets
            // Use exact filters to reduce the matching data and enhance performance

            if (string.IsNullOrEmpty(_assistanceRequestUri))
            {
                var fetchXml = $"""
                                <fetch>
                                  <entity name="ofm_assistance_request">
                                    <attribute name="ofm_assistance_requestid" />
                                    <attribute name="ofm_name" />
                                    <attribute name="ofm_subject" />
                                    <attribute name="ofm_submission_time" />
                                    <attribute name="ofm_contact" />
                                    <attribute name="ofm_is_read" />
                                    <filter>
                                      <condition attribute="ofm_submission_time" operator="last-x-days" value="30" />
                                      <condition attribute="ofm_contact_method" operator="eq" value="1" />
                                      <condition attribute="ofm_contact" operator="not-null" value="" />
                                      <condition attribute="statecode" operator="eq" value="0" />
                                      <condition attribute="ofm_is_read" operator="eq" value="0" />
                                      <filter type="or">
                                        <condition attribute="statuscode" operator="eq" value="3" />
                                        <condition attribute="statuscode" operator="eq" value="4" />
                                      </filter>
                                    </filter>
                                    <link-entity name="contact" from="contactid" to="ofm_contact" link-type="inner" alias="contact">
                                      <attribute name="emailaddress1" />
                                    </link-entity>
                                  </entity>
                                </fetch>
                                """;

                var assistancerequestUri = $"""
                                   ofm_assistance_requests?fetchXml={WebUtility.UrlEncode(fetchXml)}
                                   """;

                _assistanceRequestUri = assistancerequestUri.CleanCRLF();
            }

            return _assistanceRequestUri;
        }
    }

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling Email GetData of {nameof}", nameof(P200EmailReminderProvider));

        if (_data is null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting due emails with query {requestUri}", AssistanceRequestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query email reminders with the server error {responseBody}", responseBody.CleanLog());

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

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString(Setup.s_writeOptionsForLogs));
        }

        return await Task.FromResult(_data);
    }

    public async Task<ProcessData> GetAssistanceRequestDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling AssisstanceRequestGetData of {nameof}", nameof(P200EmailReminderProvider));

        if (_assistanceRequestdata is null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting due Assistance Request with query {assistancerequestUri}", AssistanceRequestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, AssistanceRequestUri, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query Assistance Requests with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Assistance Request found with query {assistancerequestUri}", AssistanceRequestUri);
                }
                d365Result = currentValue!;
            }

            _assistanceRequestdata = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _assistanceRequestdata.Data.ToJsonString(Setup.s_writeOptionsForLogs));
        }

        return await Task.FromResult(_assistanceRequestdata);
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
        _activeCommunicationTypes = _communicationType.ToArray()
                                            .Select(comm_type => string.Concat("'", comm_type.ofm_communication_typeid, "'"))
                                            .ToArray<string>();
        _communicationTypesForUnreadReminders = _communicationType.ToArray().Where(type => type.ofm_communication_type_number == _notificationSettings.CommunicationTypes.ActionRequired ||
                                                                                         type.ofm_communication_type_number == _notificationSettings.CommunicationTypes.DebtLetter ||
                                                                                        type.ofm_communication_type_number == _notificationSettings.CommunicationTypes.FundingAgreement)
                                                                   .Select(type => type.ofm_communication_typeid!.ToString())!.ToArray<string>();
        if (!_activeCommunicationTypes.Any())
            throw new Exception("Communication Types are missing.");

        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetDataAsync();

        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<D365Email>>(localData.Data, Setup.s_writeOptionsForLogs);

        DateTime todayUtc = DateTime.UtcNow; //Should be today around 7:00PM PST based on the standard run schedule
        DateTime yesterdayUtc = todayUtc.AddDays(-1 + _notificationSettings.UnreadEmailOptions.TimeOffsetInDays); // Should be yesterday around 7:00PM PST based on the standard run schedule. Use TimeOffsetInDays adjustment to expand the range and handle any previous failures (Zero or negative numbers)
        var todayRange = new Range<DateTime>(yesterdayUtc, todayUtc);

        var dueEmails = serializedData!.Where(e => e.IsCompleted && (IsNewAndUnread(e, todayRange) || IsUnreadReminderRequired(e, todayRange)));

        // Send only one email per contact
        var uniqueContacts = dueEmails.SelectMany(e => e.email_activity_parties.Select(ap => new { ap.addressused, ap._partyid_value })).Distinct().ToList();

        var AssistanceRequestData = await GetAssistanceRequestDataAsync();
        var serializedAssistanceRequestData = System.Text.Json.JsonSerializer.Deserialize<List<D365AssistanceRequest>>(AssistanceRequestData.Data, Setup.s_writeOptionsForLogs);
        var dueAssistanceRequests = serializedAssistanceRequestData!.Where(e => AssistanceRequestIsNewAndUnread(e, todayRange) || IsUnreadReminderRequiredAssistanceRequest(e, todayRange)).ToList();

        if (uniqueContacts.Count == 0 && dueAssistanceRequests.Count == 0)
        {
            _logger.LogInformation(CustomLogEvent.Process, "Send email reminders completed. No unique contacts found.");
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        #region Step 1: Create the email reminders as Completed-Pending Send

        List<string> recipientsList = [];
        string? contactId = null;
        // foreach contact, check if the email address is on the safe list configured on the appsettings, if yes then carry on, else replace the email with a default email address
        uniqueContacts.ForEach(contact =>
        {
            contactId = contact._partyid_value;
            if (_notificationSettings.EmailSafeList.Enable &&
                !_notificationSettings.EmailSafeList.Recipients.Any(x => x.Equals(contact.addressused?.Trim(';'), StringComparison.CurrentCultureIgnoreCase)))
            {
                contactId = _notificationSettings.EmailSafeList.DefaultContactId;
            }
            recipientsList.Add(contactId);
        });
        dueAssistanceRequests.ForEach(assistanceReq =>
        {
            contactId = assistanceReq._ofm_contact_value;
            if (_notificationSettings.EmailSafeList.Enable &&
                !_notificationSettings.EmailSafeList.Recipients.Any(x => x.Equals(assistanceReq.emailaddress1?.Trim(';'), StringComparison.CurrentCultureIgnoreCase)))
            {
                contactId = _notificationSettings.EmailSafeList.DefaultContactId;
            }
            recipientsList.Add(contactId);
        });

        recipientsList = recipientsList.Distinct().ToList();
        List<HttpRequestMessage> SendEmailFromTemplateRequest = [];
        var templateData = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 201).TemplateNumber);
        var serializedtemplateData = JsonConvert.DeserializeObject<List<D365Template>>(templateData.Data.ToString());
        
        recipientsList?.ForEach(recipientContact =>
        {
            SendEmailFromTemplateRequest.Add(new SendEmailFromTemplateRequest(
                new JsonObject(){
                        { "TemplateId" , serializedtemplateData?.First().templateid}, //Action Required: A communication regarding OFM funding requires your attention.
                        { "Regarding" , new JsonObject {
                                                { "@odata.type" , "Microsoft.Dynamics.CRM.systemuser"},
                                                { "systemuserid",_notificationSettings.DefaultSenderId}
                                        }
                        },
                        { "Target", new JsonObject  {
                        { "ofm_show_notification_on_portal" , false},
                        { "email_activity_parties", new JsonArray(){
                                    new JsonObject
                                    {
                                        {"partyid_systemuser@odata.bind", $"/systemusers({_notificationSettings.DefaultSenderId})"},
                                        { "participationtypemask", 1 } //From Email
                                    },
                                    new JsonObject
                                    {
                                        { "partyid_contact@odata.bind", $"/contacts({recipientContact})" },
                                        { "participationtypemask",   2 } //To Email                             
                                    }
                                }},

                        { "@odata.type", "Microsoft.Dynamics.CRM.email" }

                } } }, _d365AuthSettings));
        });

        var sendEmailBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, SendEmailFromTemplateRequest,null);

        if (sendEmailBatchResult.Errors.Any())
        {
            var sendReminderError = ProcessResult.Failure(ProcessId, sendEmailBatchResult.Errors, sendEmailBatchResult.TotalProcessed, sendEmailBatchResult.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to send Reminder with an error: {error}", JsonValue.Create(sendReminderError)!.ToString());

            return sendReminderError.SimpleProcessResult;
        }

        _logger.LogInformation(CustomLogEvent.Process, "Total email reminders created {uniqueContacts}", uniqueContacts.Count);

        #endregion

        var endTime = _timeProvider.GetTimestamp();

        var result = ProcessResult.Success(ProcessId, uniqueContacts.Count);
        _logger.LogInformation(CustomLogEvent.Process, "Send email reminders process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, JsonValue.Create(result)!.ToString());

        return await Task.FromResult(result.SimpleProcessResult);
    }

    #region Local Validation & Setup Code

    private bool IsUnreadReminderRequired(D365Email email, Range<DateTime> todayRange)
    {
        if (email == null || email.ofm_sent_on is null || email.lastopenedtime is not null)
            return false;

        if (email.lastopenedtime is null)
        {
            var firstReminderInDays = _notificationSettings.UnreadEmailOptions.FirstReminderInDays;
            var secondReminderInDays = _notificationSettings.UnreadEmailOptions.SecondReminderInDays;
            var thirdReminderInDays = _notificationSettings.UnreadEmailOptions.ThirdReminderInDays;
            if (_communicationTypesForUnreadReminders.Contains(email._ofm_communication_type_value))
            {
                if (todayRange.WithinRange(email.ofm_sent_on.GetValueOrDefault().AddDays(firstReminderInDays)) ||
                todayRange.WithinRange(email.ofm_sent_on.GetValueOrDefault().AddDays(secondReminderInDays)) ||
                todayRange.WithinRange(email.ofm_sent_on.GetValueOrDefault().AddDays(thirdReminderInDays)))

                    return true;
            }
        }

        return false;
    }
    private bool IsUnreadReminderRequiredAssistanceRequest(D365AssistanceRequest assistanceRequest, Range<DateTime> todayRange)
    {
        if (assistanceRequest == null)
            return false;


        var firstReminderInDays = _notificationSettings.UnreadEmailOptions.FirstReminderInDays;
        var secondReminderInDays = _notificationSettings.UnreadEmailOptions.SecondReminderInDays;
        var thirdReminderInDays = _notificationSettings.UnreadEmailOptions.ThirdReminderInDays;
        if (todayRange.WithinRange(assistanceRequest.ofm_submission_time.GetValueOrDefault().AddDays(firstReminderInDays)) ||
        todayRange.WithinRange(assistanceRequest.ofm_submission_time.GetValueOrDefault().AddDays(secondReminderInDays)) ||
        todayRange.WithinRange(assistanceRequest.ofm_submission_time.GetValueOrDefault().AddDays(thirdReminderInDays)))
        {
            return true;
        }

        return false;
    }

    private bool IsNewAndUnread(D365Email email, Range<DateTime> todayRange)
    {
        if (email.ofm_sent_on is null)
            return false;

        // if the email is created/sent today and is unread
        if (email.lastopenedtime is null && todayRange.WithinRange(email.ofm_sent_on.GetValueOrDefault()))
            return true;

        return false;
    }

    private bool AssistanceRequestIsNewAndUnread(D365AssistanceRequest assistanceRequest, Range<DateTime> todayRange)
    {

        // if the email is created/sent today and is unread
        if (assistanceRequest.ofm_is_read == false && todayRange.WithinRange(assistanceRequest.ofm_submission_time.GetValueOrDefault()))
            return true;

        return false;
    }

    #endregion
}