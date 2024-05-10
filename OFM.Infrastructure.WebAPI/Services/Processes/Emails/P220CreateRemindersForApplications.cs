using ECC.Core.DataContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    public class P220CreateRemindersForApplications(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IOptionsSnapshot<NotificationSettings> notificationSettings) : ID365ProcessProvider
    {
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
        private ProcessParameter? _processParams;
        public short ProcessId => Setup.Process.Emails.CreateEmailRemindersId;
        public string ProcessName => Setup.Process.Emails.CreateEmailRemindersName;

        //To retrieve application of any newly submitted supplementary application.
        private string RetrieveSupplementaryApplications
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                //Ignore the supplementary applications for term 3 -> FA is expiring


                DateTime todayUtc = DateTime.UtcNow;
                DateTime yesterdayUtc = todayUtc.AddDays(-1);

                string todayUtcstr = todayUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                string yesterdayUtcstr = yesterdayUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

                var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
                  <entity name="ofm_allowance">
                    <attribute name="createdon" />
                    <attribute name="ofm_allowance_number" />
                    <attribute name="ofm_allowance_type" />
                    <attribute name="ofm_allowanceid" />
                    <attribute name="ofm_end_date" />
                    <attribute name="ofm_renewal_term" />
                    <attribute name="ofm_start_date" />
                    <attribute name="ofm_transport_vehicle_vin" />
                    <attribute name="ofm_application" />
                    <filter>
                      <condition attribute="ofm_submittedon" operator="today" />
                      <condition attribute="ofm_renewal_term" operator="ne" value="3" />
                    </filter>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                               ofm_allowances?$select=createdon,ofm_allowance_number,ofm_allowance_type,ofm_allowanceid,ofm_end_date,ofm_renewal_term,ofm_start_date,ofm_transport_vehicle_vin,_ofm_application_value,ofm_submittedon&$filter=(ofm_submittedon gt {yesterdayUtcstr} and ofm_submittedon lt {todayUtcstr} and ofm_renewal_term ne 3)
                               """;
                return requestUri.CleanCRLF();
            }
        }


        public async Task<ProcessData> GetDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetSupplementaryApplicationDataAsync");

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveSupplementaryApplications, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query supplementary applications to update with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No supplementary applications found with query {requestUri}", RetrieveSupplementaryApplications.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<ProcessData> GetReminderDataAsync(string reminderXml)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetReminderDataAsync");

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, reminderXml, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query supplementary applications to update with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No reminders found with query {requestUri}", RetrieveSupplementaryApplications.CleanLog());
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

            #region step 1: get supplementary applications created on today
            var supplementaryData = await GetDataAsync();

            var deserializedData = JsonSerializer.Deserialize<List<Supplementary>>(supplementaryData.Data, Setup.s_writeOptionsForLogs);

            if (deserializedData.Count > 0)
            {
                foreach (var supplememtary in deserializedData)
                {
                    //check if reminders for the term is created or not
                    var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
                      <entity name="ofm_reminder">
                        <filter>
                          <condition attribute="ofm_application" operator="eq" value="{supplememtary._ofm_application_value}" />
                          <condition attribute="ofm_year_number" operator="eq" value="{supplememtary.ofm_renewal_term}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                    var requestUri = $"""
                            ofm_reminders?$filter=(_ofm_application_value eq '{supplememtary._ofm_application_value}' and ofm_year_number eq {supplememtary.ofm_renewal_term})
                            """;

                    var reminderData = await GetReminderDataAsync(requestUri.CleanCRLF());
                    var deserializedReminderData = JsonSerializer.Deserialize<List<ofm_reminders>>(reminderData.Data, Setup.s_writeOptionsForLogs);

                    //if there is a reminder created for the term -> skip
                    if (deserializedReminderData.Count > 0)
                    {
                        continue;
                    }
                    else
                    {
                        //create the reminder for the year -> 3 reminders for each year

                        List<HttpRequestMessage> sendCreateReminderRequests = [];

                        DateTime enddate = (DateTime)supplememtary.ofm_end_date;
                        //put day numbers to config file
                        //check the current time and due date
                        DateTime sixtydaysduedate = enddate.AddDays(-_notificationSettings.RenewalReminderOptions.FirstReminderInDays);
                        DateTime thirtydaysduedate = enddate.AddDays(-_notificationSettings.RenewalReminderOptions.SecondReminderInDays);
                        DateTime eighteendaysduedate = enddate.AddDays(-_notificationSettings.RenewalReminderOptions.ThirdReminderInDays);

                        sendCreateReminderRequests.Add(new CreateRequest("ofm_reminders",
                        new JsonObject(){
                                            {"ofm_year_number",supplememtary.ofm_renewal_term },
                                            {"ofm_application@odata.bind",  $"/ofm_applications({supplememtary._ofm_application_value})"},
                                            {"ofm_due_date", sixtydaysduedate},
                                            { "ofm_template_number", 220 }
                        }));

                        sendCreateReminderRequests.Add(new CreateRequest("ofm_reminders",
                          new JsonObject(){
                                            {"ofm_year_number",supplememtary.ofm_renewal_term },
                                            {"ofm_application@odata.bind",  $"/ofm_applications({supplememtary._ofm_application_value})"},
                                            {"ofm_due_date", thirtydaysduedate},
                                            { "ofm_template_number", 220 }
                          }));
                        sendCreateReminderRequests.Add(new CreateRequest("ofm_reminders",
                          new JsonObject(){
                                            {"ofm_year_number",supplememtary.ofm_renewal_term },
                                            {"ofm_application@odata.bind",  $"/ofm_applications({supplememtary._ofm_application_value})"},
                                            {"ofm_due_date", eighteendaysduedate},
                                            { "ofm_template_number", 220 }
                          }));

                        var sendReminderBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, sendCreateReminderRequests, null);

                        if (sendReminderBatchResult.Errors.Any())
                        {
                            var createReminderNotificationError = ProcessResult.Failure(ProcessId, sendReminderBatchResult.Errors, sendReminderBatchResult.TotalProcessed, sendReminderBatchResult.TotalRecords);
                            _logger.LogError(CustomLogEvent.Process, "Failed to create reminders with an error: {error}", JsonValue.Create(createReminderNotificationError)!.ToString());

                            return createReminderNotificationError.SimpleProcessResult;
                        }

                        _logger.LogInformation(CustomLogEvent.Process, "email reminders created");

                    }


                }
            }

            var result = ProcessResult.Success(ProcessId, deserializedData!.Count);

            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string json = JsonSerializer.Serialize(result, serializeOptions);

            var endTime = _timeProvider.GetTimestamp();
            _logger.LogInformation(CustomLogEvent.Process, "Create email reminders process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, json);

            return result.SimpleProcessResult;
            #endregion

        }
    }
}
