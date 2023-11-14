using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System;
using System.Dynamic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P200EmailReminderProvider : ID365ProcessProvider
{
    private readonly NotificationSettings _notificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;

    public P200EmailReminderProvider(IOptions<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => CommonInfo.ProcessInfo.Email.SendEmailRemindersId;
    public string ProcessName => CommonInfo.ProcessInfo.Email.SendEmailRemindersName;
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
                    <attribute name="senton" />
                    <attribute name="regardingobjectid" />
                    <filter type="and">
                      <condition attribute="lastopenedtime" operator="null" />
                      <condition attribute="emailreminderexpirytime" operator="next-x-days" value="29" />
                      <condition attribute="torecipients" operator="not-null" />
                      <filter type="and">
                        <!--<condition attribute="senton" operator="not-null" />-->
                      </filter>
                    </filter>
                    <link-entity name="activityparty" from="activityid" to="activityid" link-type="outer" alias="To">
                      <attribute name="participationtypemask" />
                      <filter>
                        <condition attribute="participationtypemask" operator="eq" value="2" />
                      </filter>
                      <link-entity name="contact" from="contactid" to="partyid" link-type="outer" alias="contact">
                        <attribute name="ofm_first_name" />
                        <attribute name="ofm_last_name" />
                        <attribute name="emailaddress1" />
                      </link-entity>
                    </link-entity>
                  </entity>
                </fetch>
                """;

            var requestUri = $"""
                              emails?fetchXml={WebUtility.UrlEncode(fetchXml)}
                              """;

            return requestUri;
        }
    }

    public async Task<ProcessData> GetData()
    {
        using (_logger.BeginScope("ScopeProcess: ProcessId {processId}", ProcessId))
        {
            _logger.LogDebug(CustomLogEvents.Process, "Calling GetData of {nameof}", nameof(P200EmailReminderProvider));

            if (_data is null)
            {
                var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri);
                var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

                JsonNode d365Result = string.Empty;
                if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
                {
                    if (currentValue?.AsArray().Count == 0)
                    {
                        _logger.LogInformation(CustomLogEvents.Process, "No emails found with query {requestUri}", RequestUri);
                    }
                    d365Result = currentValue!;
                }

                _data = new ProcessData(d365Result);
            }
        }

        return await Task.FromResult(_data);
    }

    public async Task<ProcessResult> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        using (_logger.BeginScope("ScopeProcess: Running processs {processId} - {processName}", ProcessId, ProcessName))
        {
            _logger.LogDebug(CustomLogEvents.Process, "Getting due emails with query {requestUri}", RequestUri);

            var startTime = _timeProvider.GetTimestamp();

            var localData = await GetData();

            _logger.LogDebug(CustomLogEvents.Process, "Query Result {localData}", localData.Data);
            var serializedData = JsonSerializer.Deserialize<List<D365Email>>(localData.Data.ToJsonString());

            var filtered = serializedData!.Where(e => e.IsCompleted && (e.IsNewAndUnread || IsUnreadReminderRequired(e))).ToList();

            // Send only one email per contact
            var filtered2 = filtered.GroupBy(e => e.torecipients).ToList();

            foreach (var item in filtered2)
            {
                //Add to bulk email create
            }
            var endTime = _timeProvider.GetTimestamp();

            _logger.LogInformation(CustomLogEvents.Process, "Querying data finished in {totalElapsedTime} seconds", _timeProvider.GetElapsedTime(startTime, endTime).TotalSeconds);
            return ProcessResult.Success(ProcessId,serializedData.Count(), serializedData.Count());
        }
    }

    #region Local Validation Code

    private bool IsUnreadReminderRequired(D365Email email)
    {
        if (email == null)
            return false;

        if (email.senton is null || email.lastopenedtime is not null)
            return false;

        if (email.lastopenedtime is null)
        {
            var firstDays = _notificationSettings.UnreadEmailOptions.FirstReminderInDays;
            var secondDays = _notificationSettings.UnreadEmailOptions.FirstReminderInDays;
            var thirdDays = _notificationSettings.UnreadEmailOptions.FirstReminderInDays;

            if (email.senton.Value.Date.AddDays(firstDays).Equals(DateTime.Now.Date) ||
                email.senton.Value.Date.AddDays(secondDays).Equals(DateTime.Now.Date) ||
                email.senton.Value.Date.AddDays(thirdDays).Equals(DateTime.Now.Date)
                )
                return true;
        }
        return false;
    }

    #endregion
}