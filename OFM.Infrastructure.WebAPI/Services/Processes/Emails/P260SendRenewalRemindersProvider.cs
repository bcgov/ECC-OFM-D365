using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;
/// <summary>
/// Processes the reminders generated from P255CreateRenewalNotificationProvider Endpoint for applications where no renewal is not submitted in th eystem
/// Refer to : https://eccbc.atlassian.net/wiki/spaces/OOFMCC/pages/521405457/Send+Renewal+Notification
/// </summary>
public class P260SendRenewalRemindersProvider : ID365ProcessProvider
{
    private readonly NotificationSettings _notificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly IEmailRepository _emailRepository;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private string[] _communicationTypesForEmailSentToUserMailBox = [];
    private ProcessParameter? _processParams;
    private string _requestUri = string.Empty;

    public P260SendRenewalRemindersProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, IEmailRepository emailRepository, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _emailRepository = emailRepository;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Emails.SendFundingExpiryNotificationsId;
    public string ProcessName => Setup.Process.Emails.SendFundingExpiryNotificationsName;
    private string ofmapplicationids = string.Empty;
    private string[] _activeCommunicationTypes = [];
    private string RequestReminderWithDuedateUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                <fetch distinct=""true"">
                  <entity name=""ofm_reminder"">
                    <attribute name=""ofm_application"" />
                    <attribute name=""ofm_caption"" />
                    <attribute name=""ofm_due_date"" />
                    <attribute name=""ofm_reminderid"" />
                    <attribute name=""ofm_completed_on"" />
                    <attribute name=""ofm_template_number"" />
                    <attribute name=""ofm_year_number"" />
                    <attribute name=""statecode"" />
                    <filter>
                      <condition attribute=""ofm_due_date"" operator=""last-x-days"" value=""1"" />
                      <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                      <condition attribute=""ofm_template_number"" operator=""eq"" value=""310"" />

                    </filter>
                    <link-entity name=""ofm_application"" from=""ofm_applicationid"" to=""ofm_application"" link-type=""inner"" alias=""ofmapp"">
                      <attribute name=""ofm_application"" />
                      <attribute name=""ofm_applicationid"" />
                          <filter type=""or"">
                            <condition attribute=""ofm_renewalapplicationstatus"" operator=""eq"" value=""1"" />
                            <condition attribute=""ofm_renewalapplicationstatus"" operator=""null"" />
                          </filter>
                      <link-entity name=""account"" from=""accountid"" to=""ofm_facility"" link-type=""inner"" alias=""facility"">
                        <link-entity name=""ofm_bceid_facility"" from=""ofm_facility"" to=""accountid"" link-type=""inner"" alias=""contactfacility"">
                          <filter>
                            <condition attribute=""ofm_portal_access"" operator=""eq"" value=""1"" />
                            <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                          </filter>                          
                        </link-entity>
                            <link-entity name=""contact"" from=""contactid"" to=""ofm_primarycontact"" link-type=""inner"" alias=""contact"">
                            <attribute name=""ccof_username"" />
                            <attribute name=""ofm_first_name"" />
                            <attribute name=""ofm_last_name"" />
                            <attribute name=""contactid"" />
                          </link-entity>
                      </link-entity>
                    </link-entity>
                  </entity>
                </fetch>";

            var requestUri = $"""
                            ofm_reminders?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    private string FundingsUri
    {
        get
        {
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                <fetch>
                  <entity name=""ofm_funding"">
<attribute name=""ofm_end_date"" />
<attribute name=""ofm_application"" />
                    <link-entity alias=""account"" name=""account"" to=""ofm_facility"" from=""accountid"" link-type=""inner"" visible=""false"">
                          <attribute name=""accountid"" />
                          <attribute name=""name"" />
                          <link-entity alias=""contact"" name=""contact"" to=""ofm_primarycontact"" from=""contactid"" link-type=""outer"" visible=""false"">
                            <attribute name=""contactid"" />
                            <attribute name=""fullname"" />
                          </link-entity>
                        </link-entity>
                    <filter type=""and"">
                      <filter>
                        <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                        <condition attribute=""statuscode"" operator=""eq"" value=""8"" />
                      </filter>
                      <filter type=""or"">
                        {ofmapplicationids}
                      </filter>
                    </filter>
                  </entity>
                </fetch>";
            var requestUri = $"""
                            ofm_fundings?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    public async Task<ProcessData> GetDataAsync()
    {
        _logger!.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P215SendSupplementaryRemindersProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestReminderWithDuedateUri, isProcess: true);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                //_logger.LogError(CustomLogEvent.Process, "Failed to query the requests with the server error {responseBody}", responseBody);

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

    public async Task<ProcessData> GetDataFromCRMAsync(string requestUri)
    {
        _logger.LogDebug(CustomLogEvent.Process, "Getting records from with query {requestUri}", requestUri.CleanLog());

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUri, isProcess: true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query records with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No records found with query {requestUri}", requestUri.CleanLog());
            }
            d365Result = currentValue!;
        }
        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());
        return await Task.FromResult(new ProcessData(d365Result)!);

    }

    public async Task<JsonObject?> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
        var startTime = _timeProvider.GetTimestamp();
        IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
        // get reminder Couumication Type guid 
        IEnumerable<D365CommunicationType> _remindercommunicationType = _communicationType.Where(item => item.ofm_communication_type_number == (int)(_processParams.Notification.CommunicationTypeNum));
        string reminderCommunicationTypeid = _remindercommunicationType.FirstOrDefault().ofm_communication_typeid;
        if (string.IsNullOrEmpty(reminderCommunicationTypeid))
        {
            _logger.LogError(CustomLogEvent.Process, "Communication Type is not found for ID: {Id}", _processParams.Notification.CommunicationTypeNum);
        }

        //Get all reminders with duedate in Today include all contacts related to them
        var localDataReminders = await GetDataFromCRMAsync(RequestReminderWithDuedateUri);
        JsonArray reminders = (JsonArray)localDataReminders.Data;

        if (reminders.Count == 0)
        {
            _logger.LogDebug(CustomLogEvent.Process, "No Reminders found");
            return null;
        }

        // Get distinct reminders
        var distinctReminders = reminders.DistinctBy(item => (string)item["_ofm_application_value"]).ToArray();

        //conbine query string to get all Supplementaries records under all applications.
        foreach (var reminder in distinctReminders)  // conbine query string to get all Supplementaries records under all applications.
        {
            ofmapplicationids = ofmapplicationids + $@"<condition attribute = ""ofm_application"" operator= ""eq"" value = """ + reminder["_ofm_application_value"] + $@""" />";
        }
        // get all fundings records for all reminders
        var localDateFundings = await GetDataFromCRMAsync(FundingsUri);
        //get Email Template
        var localDateEmailTemplate = await _emailRepository.GetTemplateDataAsync(Int32.Parse(_processParams.Notification.TemplateNumber));
        JsonArray emailTemplate = (JsonArray)localDateEmailTemplate.Data;
        JsonNode templateobj = emailTemplate.FirstOrDefault();
        string? subject = _emailRepository.StripHTML((string)templateobj["subjectsafehtml"]);
        string? emaildescription = (string)templateobj["safehtml"];
        var updateRemindersRequests = new List<HttpRequestMessage>() { };
        //Group Reminders by Contact
        var RemindersByContact = distinctReminders
    .GroupBy(reminder => reminder["contact.contactid"]?.ToString())
    .OrderBy(contactGroups => contactGroups.Key);
        //Get Group of Reminders for each Contact
        foreach (var reminderGroup in RemindersByContact)
        {
            var contactId = reminderGroup.Key;
            //Get Reminders for Contact
            
                StringBuilder sbTable = new StringBuilder("<div style=\" overflow: auto;width: 100%;\" role=\"region\" tabindex=\"0\"><table style=\"text-align: center;border: 1px solid #dededf;    height: 100%;    width: 100%;    table-layout: fixed;    border-collapse: collapse;    border-spacing: 1px;    text-align: left;\" border=\"1\" border-style=\"solid\" rules=\"all\" width=\"100%\"><thead bgcolor=\"#eceff1\"><tr><th width=\"40%\" style=\"text-align: center;border: 1px solid #dededf;background-color: #eceff1;color: #000000;padding: 5px;\">Facility</th><th width=\"30%\" style=\"text-align: center;border: 1px solid #dededf;background-color: #eceff1;color: #000000;padding: 5px;\">Expiry Date</th><th width=\"30%\" style=\"text-align: center;border: 1px solid #dededf;background-color: #eceff1;color: #000000;padding: 5px;\">Renewal Deadline Date</th></tr></thead><tbody>");
                var fundings = localDateFundings.Data.AsArray()
                .Where(item => (Guid)item["contact.contactid"] == Guid.Parse(contactId)).ToArray();
                if (!fundings.Any()) continue;
                foreach (var funding in fundings)
                {
                    sbTable.Append($"<tr><td align=\"center\" style=\"text-align: center;border: 1px solid #dededf;background-color: #ffffff;   color: #000000;    padding: 5px;\">{funding?["account.name"]}</td><td align=\"center\"  style=\"text-align: center;border: 1px solid #dededf;    background-color: #ffffff;    color: #000000;    padding: 5px;\">{((DateTime)funding?["ofm_end_date"]).ToString("dddd, MMMM dd, yyyy")}</td><td align=\"center\"  style=\"text-align: center;border: 1px solid #dededf;    background-color: #ffffff; color: #000000;padding: 5px;\">{((DateTime)funding?["ofm_end_date"]).AddDays(-15).ToString("dddd, MMMM dd, yyyy")}</td></tr>");

                }
                sbTable.Append("</tbody></table></div>");
                string tempEmaildescription = emaildescription;
                tempEmaildescription = tempEmaildescription?.Replace("#FacilitiesExpiringFundings#", sbTable.ToString());
                #region  Create the email notifications as Completed for each Contact               
                var requestBody = new JsonObject(){
                            {"subject",subject },
                            {"description",tempEmaildescription},
                            {"email_activity_parties", new JsonArray(){
                                new JsonObject
                                {
                                   { "partyid_systemuser@odata.bind", $"/systemusers({_processParams.Notification.SenderId})"},
                                    //{ "partyid_systemuser@odata.bind", $"/systemusers({_notificationSettings.DefaultSenderId})"},
                                    { "participationtypemask", 1 } //From Email
                                },
                                new JsonObject
                                {
                                    { "partyid_contact@odata.bind", $"/contacts({contactId})" },
                                    { "participationtypemask",   2 } //To Email                             
                                }
                            }},
                            //{ "ofm_communication_type_Email@odata.bind", $"/ofm_communication_types({_processParams.Notification.CommunicationTypeId})"}
                            { "ofm_communication_type_Email@odata.bind", $"/ofm_communication_types({reminderCommunicationTypeid})"}
                        };

                var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, "emails", requestBody.ToString());
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to create the email record with the server error {responseBody}", responseBody.CleanLog());
                    continue;
                }
                var newEmail = await response.Content.ReadFromJsonAsync<JsonObject>();
                var newEmailId = newEmail?["activityid"];
                var emailStatement = $"emails({newEmailId})";
                var payload = new JsonObject {
                        { "ofm_sent_on", DateTime.UtcNow },
                        { "statuscode", (int)Email_StatusCode.Completed },
                        { "statecode", (int)email_statecode.Completed }};
                var requestBody1 = JsonSerializer.Serialize(payload);
                var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, emailStatement, requestBody1);
                if (!patchResponse.IsSuccessStatusCode)
                {
                    var responseBody = await patchResponse.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());
                    continue;
                }
                // compose deactive Reminder object
                var reminderToUpdate = new JsonObject
            {
                { "statecode", 1 }
            };
            foreach (var reminder in reminderGroup)
            {
                updateRemindersRequests.Add(new D365UpdateRequest(new D365EntityReference("ofm_reminders", (Guid)reminder["ofm_reminderid"]), reminderToUpdate));
            }
            #endregion Create the email notifications as Completed for each Contact
        }

        // Deactive reminders
        var updateRemindersResults = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updateRemindersRequests, null);
        if (updateRemindersResults.Errors.Any())
        {
            var sendNotificationError = ProcessResult.Failure(ProcessId, updateRemindersResults.Errors, updateRemindersResults.TotalProcessed, updateRemindersResults.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to send notifications with an error: {error}", JsonValue.Create(sendNotificationError)!.ToString());

            return await Task.FromResult(sendNotificationError.SimpleProcessResult);
        }

        var result = ProcessResult.Success(ProcessId, reminders.Count);

        var endTime = _timeProvider.GetTimestamp();

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string json = JsonSerializer.Serialize(result, serializeOptions);
        _logger.LogInformation(CustomLogEvent.Process, "Send Notification process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, json);

        return await Task.FromResult(result.SimpleProcessResult);
    }
}