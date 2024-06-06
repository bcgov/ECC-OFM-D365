using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P225AnnualIncreaseMessaging : ID365ProcessProvider
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

    public P225AnnualIncreaseMessaging(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, IEmailRepository emailRepository, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _emailRepository = emailRepository;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Emails.CreateAnnualIncreaseMessagingId;
    public string ProcessName => Setup.Process.Emails.CreateAnnualIncreaseMessaging;
    private string contactid = string.Empty;
    private string[] _activeCommunicationTypes = [];
    private string FundingsWithFacilityContactUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                    <fetch top=""1"">
                      <entity name=""ofm_funding"">
                        <attribute name=""ofm_envelope_grand_total"" />
                        <attribute name=""ofm_facility"" />
                        <attribute name=""ofm_fundingid"" />
                        <attribute name=""ofm_start_date"" />
                        <attribute name=""ofm_end_date"" />
                        <filter>
                          <condition attribute=""statuscode"" operator=""eq"" value=""8"" />
                          <condition attribute=""statecode"" operator=""eq"" value=""0"" />
        
                        </filter>
                        <link-entity name=""account"" from=""accountid"" to=""ofm_facility"" link-type=""inner"" alias=""facility"">
                          <attribute name=""accountnumber"" />
                          <attribute name=""ofm_primarycontact"" />
                          <filter>
                            <condition attribute=""ofm_primarycontact"" operator=""not-null"" value="""" />
                          </filter>
                        </link-entity>
                      </entity>
                    </fetch>";
            var requestUri = $"""
                            ofm_fundings?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    private string ContactFacilitiessUri
    {
        get
        {
            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                <fetch>
                  <entity name=""ofm_bceid_facility"">
                    <attribute name=""ofm_bceid"" />
                    <attribute name=""ofm_bceid_facilityid"" />
                    <attribute name=""ofm_facility"" />
                    <attribute name=""ofm_is_expense_authority"" />
                    <attribute name=""ofm_name"" />
                    <attribute name=""statecode"" />
                    <filter>
                      <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                      <condition attribute=""ofm_is_expense_authority"" operator=""eq"" value=""1"" />
                      <condition attribute=""ofm_bceid"" operator=""eq"" value=""{contactid}"" />
                    </filter>
                    <link-entity name=""account"" from=""accountid"" to=""ofm_facility"" link-type=""inner"" alias=""facility"">
                      <attribute name=""accountid"" />
                      <attribute name=""accountnumber"" />
                      <attribute name=""ofm_primarycontact"" />
                          <filter>
                            <condition attribute=""ofm_primarycontact"" operator=""not-null"" value="""" />
                          </filter>
                      </link-entity>
                  </entity>
                </fetch>";
            var requestUri = $"""
                            ofm_bceid_facilities?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    public async Task<ProcessData> GetDataAsync()
    {
        _logger!.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P215SendSupplementaryRemindersProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, FundingsWithFacilityContactUri, isProcess: true);
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
        string CommunicationTypeid = _remindercommunicationType.FirstOrDefault().ofm_communication_typeid;
        if (string.IsNullOrEmpty(CommunicationTypeid))
        {
            _logger.LogError(CustomLogEvent.Process, "Communication Type is not found for ID: {Id}", _processParams.Notification.CommunicationTypeNum);
        }

        //Get all fundings  Annual increase Messaging & Notification
        var localDataFundings = await GetDataFromCRMAsync(FundingsWithFacilityContactUri);
        JsonArray fundings = (JsonArray)localDataFundings.Data;

        if (fundings.Count == 0)
        {
            _logger.LogDebug(CustomLogEvent.Process, "No fundings found");
            return null;
        }
        // get emailtemplate
        var localDateEmailTemplate = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 225).TemplateNumber);
        JsonArray emailTemplate = (JsonArray)localDateEmailTemplate.Data;
        JsonNode templateobj = emailTemplate.FirstOrDefault();
        foreach (var funding in fundings)
        {
            #region  Get all contacts in Contact Facilities with Expense Authority 
            contactid = funding["facility.ofm_primarycontact"].ToString();
            List<Guid> recipientsList = new List<Guid>();
            recipientsList.Add((Guid)(funding["facility.ofm_primarycontact"]));
            var localDataContactFacilities = await GetDataFromCRMAsync(ContactFacilitiessUri);
            JsonArray contactFacilities = (JsonArray)localDataContactFacilities.Data;
            foreach(var contactFacility in contactFacilities)
            {
                if (!recipientsList.Contains((Guid)contactFacility["facility.ofm_primarycontact"]))
                {
                    recipientsList.Add((Guid)contactFacility["facility.ofm_primarycontact"]);
                }
            }
            // get all Facility Permission with Expense Authority
            #endregion Get all contacts in Contact Facilities with Expense Authority
            #region  Create the email notifications as Completed for each Contact
            string? subject = (string)templateobj["subjectsafehtml"];
            string? emaildescription = (string)templateobj["safehtml"];
            emaildescription = emaildescription?.Replace("#FacilityName#", (string)funding["facility.name"]);
            emaildescription = emaildescription?.Replace("#RateofIncrease#", _processParams.Notification.RateofIncrease.ToString());
            emaildescription = emaildescription?.Replace("#ExpiryDate#", "dddd");
            emaildescription = emaildescription?.Replace("#RenewalDeadlineDate#", " ddd");
            await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, CommunicationTypeid, appUserService, d365WebApiService, 225);
            #endregion Create the email notifications as Completed for each Contact
          }
        var result = ProcessResult.Success(ProcessId, fundings.Count);
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