﻿using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
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
    private string[] _activeCommunicationTypes = Array.Empty<string>();
    private string[] _communicationTypesForUnreadReminders = Array.Empty<string>();
    private string _requestUri = string.Empty;

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
            // Use Paging Cookie for large datasets
            // Use exact filters to reduce the matching data and enhance performance

            if (string.IsNullOrEmpty(_requestUri))
            {
                var communicationTypesString = _activeCommunicationTypes.Aggregate((partialPhrase, id) => $"{partialPhrase},{id}");

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

                //var requestUri = $"""
                //                  emails?fetchXml={WebUtility.UrlEncode(fetchXml)}
                //                  """;

                var requestUri = $"""                                
                                emails?$select=subject,lastopenedtime,torecipients,_emailsender_value,sender,submittedby,statecode,statuscode,_ofm_communication_type_value,_regardingobjectid_value,ofm_sent_on,ofm_expiry_time
                                &$expand=email_activity_parties($select=participationtypemask,_partyid_value;$filter=(participationtypemask eq 2))
                                &$filter=(lastopenedtime eq null and torecipients ne null and Microsoft.Dynamics.CRM.NextXDays(PropertyName='ofm_expiry_time',PropertyValue=29) and Microsoft.Dynamics.CRM.In(PropertyName='ofm_communication_type',PropertyValues=[{communicationTypesString}]))
                                """;

                _requestUri = requestUri.CleanCRLF();
            }

            return _requestUri;
        }
    }

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P200EmailReminderProvider));

        if (_data is null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting due emails with query {requestUri}", RequestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri);

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

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        await SetupCommunicationTypes();

        if (!_activeCommunicationTypes.Any())
            throw new Exception("Communication Types are missing.");

        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetData();

        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<D365Email>>(localData.Data, Setup.s_writeOptionsForLogs);

        DateTime todayUtc = DateTime.UtcNow; //Should be today around 7:00PM PST based on the standard run schedule
        DateTime yesterdayUtc = todayUtc.AddDays(-1 + _notificationSettings.UnreadEmailOptions.TimeOffsetInDays); // Should be yesterday around 7:00PM PST based on the standard run schedule. Use TimeOffsetInDays adjustment to expand the range and handle any previous failures (Zero or negative numbers)
        var todayRange = new Range<DateTime>(yesterdayUtc, todayUtc);

        var dueEmails = serializedData!.Where(e => e.IsCompleted && (IsNewAndUnread(e, todayRange) || IsUnreadReminderRequired(e, todayRange)));

        // Send only one email per contact
        var uniqueContacts = dueEmails.GroupBy(e => e.torecipients).ToList();

        if (uniqueContacts.Count == 0)
        {
            _logger.LogInformation(CustomLogEvent.Process, "Send email reminders completed. No unique contacts found.");
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        #region Step 1: Create the email reminders as Completed-Pending Send

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

        HttpResponseMessage bulkEmailsResponse = await d365WebApiService.SendBulkEmailTemplateMessageAsync(appUserService.AZSystemAppUser, contentBody, null);

        if (!bulkEmailsResponse.IsSuccessStatusCode)
        {
            var responseBody = await bulkEmailsResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create email reminder records with a server error: {error}", responseBody);

            return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, localData.Data.AsArray().Count).SimpleProcessResult;
        }

        _logger.LogInformation(CustomLogEvent.Process, "Total email reminders created {uniqueContacts}", uniqueContacts.Count);

        #endregion

        #region  Step 2 (ToDo): Send the emails
        //Via GC Notify or Exchange Online
        #endregion

        var endTime = _timeProvider.GetTimestamp();

        var result = ProcessResult.Success(ProcessId, uniqueContacts.Count);
        _logger.LogInformation(CustomLogEvent.Process, "Send email reminders process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, JsonValue.Create(result)!.ToString());

        return result.SimpleProcessResult;
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

    private bool IsNewAndUnread(D365Email email, Range<DateTime> todayRange)
    {
        if (email.ofm_sent_on is null)
            return false;

        // if the email is created/sent today and is unread
        if (email.lastopenedtime is null && todayRange.WithinRange(email.ofm_sent_on.GetValueOrDefault()))
            return true;

        return false;
    }

    private async Task SetupCommunicationTypes()
    {
        if (!_activeCommunicationTypes.Any())
        {
            var fetchXml = """
                            <fetch distinct="true" no-lock="true">
                              <entity name="ofm_communication_type">
                                <attribute name="ofm_communication_typeid" />
                                <attribute name="ofm_communication_type_number" />
                                <attribute name="ofm_name" />
                                <attribute name="statecode" />
                                <attribute name="statuscode" />
                                <filter>
                                  <condition attribute="statecode" operator="eq" value="0" />
                                </filter>
                              </entity>
                            </fetch>
                """;

            var requestUri = $"""
                              ofm_communication_types?fetchXml={WebUtility.UrlEncode(fetchXml)}
                              """;

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query the communcation types with a server error {responseBody}", responseBody.CleanLog());
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No communcation types found with query {requestUri}", requestUri);
                }
                d365Result = currentValue!;
            }

            _activeCommunicationTypes = d365Result.AsArray()
                                            .Select(comm_type => string.Concat("'", comm_type?["ofm_communication_typeid"], "'"))
                                            .ToArray<string>();
 
            _communicationTypesForUnreadReminders = d365Result.AsArray().Where(type => type?["ofm_communication_type_number"]?.ToString() == _notificationSettings.CommunicationTypes.ActionRequired.ToString() ||
                                                                                          type?["ofm_communication_type_number"]?.ToString() == _notificationSettings.CommunicationTypes.DebtLetter.ToString() ||
                                                                                          type?["ofm_communication_type_number"]?.ToString() == _notificationSettings.CommunicationTypes.FundingAgreement.ToString())
                                                                    .Select(type => type?["ofm_communication_typeid"]!.ToString())!.ToArray<string>();
        }
    }

    #endregion
}