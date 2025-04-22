using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    /// <summary>
    ///Generates the reminders for approved funding application for 30, 90 and 120 days before the expiry of the funding
    /// Refer to : https://eccbc.atlassian.net/wiki/spaces/OOFMCC/pages/521405457/Send+Renewal+Notification
    /// </summary>
    public class P255CreateRenewalNotificationProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IOptionsSnapshot<NotificationSettings> notificationSettings) : ID365ProcessProvider
    {
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
        private ProcessParameter? _processParams;
        public short ProcessId => Setup.Process.Emails.CreateFundingExpiryRemindersId;
        public string ProcessName => Setup.Process.Emails.CreateFundingExpiryRemindersName;

        //To retrieve all active fundings
        private string RetrieveActiveFundings
        {
            get
            {
                DateTime todayUtc = DateTime.UtcNow;
                DateTime yesterdayUtc = todayUtc.AddDays(-1);
                string todayUtcstr = todayUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                string yesterdayUtcstr = yesterdayUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                //all funding which are approved in last 1 day and are active in the system
                var requestUri = $"""
                               ofm_fundings?$select=createdon,_ofm_application_value,ofm_fundingid,ofm_end_date,ofm_start_date,ofm_ministry_approval_date&$filter=(ofm_ministry_approval_date gt {yesterdayUtcstr} and ofm_ministry_approval_date lt {todayUtcstr} and _ofm_application_value ne null and ofm_end_date ne null and statuscode eq 8 and statecode eq 0 and ofm_application/ofm_application_type eq 1)
                               """;

                //For testing in DEV
                //var requestUri = $"""
                //               ofm_fundings?$select=createdon,_ofm_application_value,ofm_fundingid,ofm_end_date,ofm_start_date,ofm_ministry_approval_date&$filter=_ofm_application_value ne null and ofm_end_date ne null and statuscode eq 8 and ofm_application/ofm_application_type eq 1
                //               """;
                return requestUri.CleanCRLF();
            }
        }


        public async Task<ProcessData> GetDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling SendRetrieveRequestAsync");

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveActiveFundings, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query base funding applications to update with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No funding applications found with query {requestUri}", RetrieveActiveFundings.CleanLog());
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
                _logger.LogError(CustomLogEvent.Process, "Failed to query expired funding applications to update with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No reminders found with query {requestUri}", RetrieveActiveFundings.CleanLog());
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

            #region step 1: get Funding applications created on today
            var fundingData = await GetDataAsync();

            if (String.IsNullOrEmpty(fundingData.Data.ToString()))
            {
                return ProcessResult.Failure(ProcessId, new String[] { "Failed to query records" }, 0, 0).SimpleProcessResult;
            }

            var deserializedData = JsonSerializer.Deserialize<List<Funding>>(fundingData.Data, Setup.s_writeOptionsForLogs);

            if (deserializedData?.Count > 0)
            {
                foreach (var funding in deserializedData)
                {
                    //check if reminders for the template number 310 is created or not
                    var requestUri = $"""
                            ofm_reminders?$filter=(_ofm_application_value eq '{funding._ofm_application_value}' and ofm_template_number eq 310)
                            """;

                    var reminderData = await GetReminderDataAsync(requestUri.CleanCRLF());

                    if (String.IsNullOrEmpty(reminderData.Data.ToString()))
                    {
                        continue;
                    }
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

                        DateTime enddate = (DateTime)funding.ofm_end_date;
                        //put day numbers to config file
                        //check the current time and due date
                        DateTime onetwentydaysduedate = enddate.AddDays(-_notificationSettings.FundingRenewalReminderOptions.FirstReminderInDays);
                        DateTime sixtydaysduedate = enddate.AddDays(-_notificationSettings.FundingRenewalReminderOptions.SecondReminderInDays);
                        DateTime thirtydaysduedate = enddate.AddDays(-_notificationSettings.FundingRenewalReminderOptions.ThirdReminderInDays);

                        sendCreateReminderRequests.Add(new CreateRequest("ofm_reminders",
                        new JsonObject(){                                                                                      {"ofm_application@odata.bind",  $"/ofm_applications({funding._ofm_application_value})"},
                                            {"ofm_due_date", onetwentydaysduedate},
                                            { "ofm_template_number", 310 }
                        }));

                        sendCreateReminderRequests.Add(new CreateRequest("ofm_reminders",
                          new JsonObject(){
                                           
                                            {"ofm_application@odata.bind",  $"/ofm_applications({funding._ofm_application_value})"},
                                            {"ofm_due_date", sixtydaysduedate},
                                            { "ofm_template_number", 310 }
                          }));
                        sendCreateReminderRequests.Add(new CreateRequest("ofm_reminders",
                          new JsonObject(){                                            
                                            {"ofm_application@odata.bind",  $"/ofm_applications({funding._ofm_application_value})"},
                                            {"ofm_due_date", thirtydaysduedate},
                                            { "ofm_template_number", 310 }
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
            _logger.LogInformation(CustomLogEvent.Process, "Create email reminders process for Funding expiry finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, json);

            return result.SimpleProcessResult;
            #endregion

        }
    }
}
