using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    public class P220CreateRemindersForApplications : ID365ProcessProvider
    {
        private readonly NotificationSettings _notificationSettings;
        private readonly ID365AppUserService _appUserService;
        private readonly ID365WebApiService _d365webapiservice;
        private readonly ILogger _logger;
        private readonly TimeProvider _timeProvider;
        private ProcessData? _data;
        private string[] _communicationTypesForEmailSentToUserMailBox = [];
        private ProcessParameter? _processParams;
        private string _requestUri = string.Empty;
        public short ProcessId => Setup.Process.Reminders.CreateEmailRemindersId;

        public string ProcessName => Setup.Process.Reminders.CreateEmailRemindersName;

        //To retrieve application of any newly submitted supplementary applications.
        private string RetrieveApplications
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $$"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
                  <entity name="ofm_application">
                    <attribute name="ofm_applicationid" />
                    <attribute name="ofm_application" />
                    <order attribute="ofm_application" descending="false" />
                    <link-entity name="ofm_allowance" from="ofm_application" to="ofm_applicationid" link-type="inner" alias="ac">
                      <filter type="and">
                        <condition attribute="ofm_submittedon" operator="today" />
                      </filter>
                    </link-entity>
                    <link-entity name="ofm_reminder" from="ofm_application" to="ofm_applicationid" link-type="outer" alias="ad" />
                    <filter type="and">
                      <condition entityname="ad" attribute="ofm_application" operator="null" />
                    </filter>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                            ofm_applications?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

                return requestUri.CleanCRLF();
            }
        }
  

        public async Task<ProcessData> GetApplicationDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetDataToUpdate");

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveApplications, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query applications to update with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No applications found with query {requestUri}", RetrieveApplications.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }


        public async Task<ProcessData> GetSupplementaryAppDataAsync( string RetrieveSupplementaryApplications)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetDataToUpdate");

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveSupplementaryApplications, isProcess: true);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No pending emails on the contact list found with query {requestUri}", RetrieveSupplementaryApplications.CleanLog());
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

            var localData = await GetApplicationDataAsync();

            var deserializedData = JsonSerializer.Deserialize<List<ofm_application>>(localData.Data.ToString());

            #region  Step 1: Retrieve supplementary information for each application

            JsonArray applications = [];

            deserializedData?.ForEach(ofm_application =>
            {
                applications.Add($"ofm_applications({ofm_application.ofm_applicationid})");
            });

            int allowancetype;
            DateTime enddate;
            string vinnumber = string.Empty;
            string allowanceid = string.Empty;
            List<HttpRequestMessage> sendCreateEmailRequests = [];
            if (applications is not null) // Get template details to send bulk emails.
            {
                foreach (var application in applications)
                {
                    var fetchXml = $$"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="ofm_allowance">
                    <attribute name="ofm_allowanceid" />
                    <attribute name="ofm_allowance_number" />
                    <attribute name="createdon" />
                    <attribute name="ofm_transport_vehicle_vin" />
                    <attribute name="ofm_start_date" />
                    <attribute name="ofm_renewal_term" />
                    <attribute name="ofm_end_date" />
                    <attribute name="ofm_application" />
                    <attribute name="ofm_allowance_type" />
                    <order attribute="ofm_allowance_number" descending="false" />
                    <filter type="and">
                      <condition attribute="ofm_application" operator="eq"  value="{{{applications[0]}}" />
                      <condition attribute="ofm_submittedon" operator="today" />
                    </filter>
                  </entity>
                </fetch>
                """;

                    var requestUri = $"""
                            ofm_allowances?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;
                    var localDataTemplate = await GetSupplementaryAppDataAsync(requestUri);

                    var serializedDataTemplate = JsonSerializer.Deserialize<List<ofm_allowance>>(localDataTemplate.Data.ToString());

                    if (serializedDataTemplate.Count > 0)
                    {
                        var supplementaryObj = serializedDataTemplate.FirstOrDefault();
                        enddate = supplementaryObj.ofm_end_date;
                        allowancetype = supplementaryObj.ofm_allowance_type;


                        #region Step 2: Calculate Due date for one application.




                        #region  Step 3: Create reminders for application 30 days, 18 days & 60 days for 1st & 2nd Term.

                        deserializedData?.ForEach(ofm_allowance =>
                        {
                            sendCreateEmailRequests.Add(new CreateRequest("ofm_reminders",
                                new JsonObject(){
                        {"ofm_renewal_term",supplementaryObj.ofm_renewal_term },
                        {"ofm_due_date",supplementaryObj.ofm_end_date.ToString("yyyy-MM-dd") },

                        { "ofm_template_number", 220 },
                                }));
                        });
                    }
                }
                var sendEmailBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, sendCreateEmailRequests, new Guid(processParams.CallerObjectId.ToString()));

                if (sendEmailBatchResult.Errors.Any())
                {
                    var sendNotificationError = ProcessResult.Failure(ProcessId, sendEmailBatchResult.Errors, sendEmailBatchResult.TotalProcessed, sendEmailBatchResult.TotalRecords);
                    _logger.LogError(CustomLogEvent.Process, "Failed to send notifications with an error: {error}", JsonValue.Create(sendNotificationError)!.ToString());

                    return sendNotificationError.SimpleProcessResult;
                }
            }
            #endregion

            var result = ProcessResult.Success(ProcessId, deserializedData!.Count);

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

        Task<ProcessData> ID365ProcessProvider.GetDataAsync()
        {
            throw new NotImplementedException();
        }
    }
}
#endregion