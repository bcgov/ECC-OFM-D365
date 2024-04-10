using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P210CreateFundingNotificationProvider : ID365ProcessProvider
{
    const string EntityNameSet = "emails";
    private readonly NotificationSettings _notificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly IFundingRepository _fundingRepository;
    private readonly IEmailRepository _emailRepository;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private string[] _communicationTypesForEmailSentToUserMailBox = [];
    private ProcessParameter? _processParams;
    private string _requestUri = string.Empty;
    private string? _fundingAgreementCommunicationType;
    private string? _informationCommunicationType;

    public P210CreateFundingNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IFundingRepository fundingRepository,IEmailRepository emailRepository)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _fundingRepository = fundingRepository;
        _emailRepository = emailRepository;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Emails.SendFundingNotificationsId;
    public string ProcessName => Setup.Process.Emails.SendFundingNotificationsName;

    private string TemplatetoRetrieveUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="template">
                    <attribute name="title" />
                    <attribute name="templatetypecode" />
                    <attribute name="safehtml" />
                    <attribute name="languagecode" />
                    <attribute name="templateid" />
                    <attribute name="subject" />
                    <attribute name="description" />
                    <attribute name="body" />
                    <order attribute="title" descending="false" />
                    <filter type="or">
                      <condition attribute="templateid" operator="eq"  uitype="template" value="{_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 210).TemplateId}" />
                      <condition attribute="templateid" operator="eq"  uitype="template" value="{_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 215).TemplateId}" />
                    </filter>
                  </entity>
                </fetch>
                """;

            var requestUri = $"""
                            templates?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetTemplateToSendEmail");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, TemplatetoRetrieveUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Emmail Template to update with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No template found with query {requestUri}", TemplatetoRetrieveUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
      _fundingAgreementCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number  == _notificationSettings.CommunicationTypes.FundingAgreement)
                                                                   .Select(s => s.ofm_communication_typeid).FirstOrDefault();
       
        _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                                   .Select(s => s.ofm_communication_typeid).FirstOrDefault();
        _processParams = processParams;
        Funding? _funding = await _fundingRepository!.GetFundingByIdAsync(new Guid(processParams.Funding!.FundingId!));
        var expenseOfficer = _funding.ofm_application!._ofm_expense_authority_value;
        var primaryContact = _funding.ofm_application!._ofm_contact_value;

        var startTime = _timeProvider.GetTimestamp();

        #region Create the funding email notifications 

        // Get template details to create emails.
        var localDataTemplate = await GetDataAsync();

        var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
        var hyperlink = _notificationSettings.fundingUrl + _funding.Id;
        var hyperlinkFATab = _notificationSettings.fundingTabUrl;

        if (serializedDataTemplate?.Count > 0)
        {
            var templateobj = serializedDataTemplate.Where(t => t.templateid == _notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 210).TemplateId);
            string? subject = templateobj?.Select(s => s.title).FirstOrDefault();
            string? emaildescription = templateobj?.Select(sh => sh.safehtml).FirstOrDefault();
            emaildescription = emaildescription?.Replace("{FA_NUMBER}", _funding.ofm_funding_number);
            emaildescription = emaildescription?.Replace("{FACILITY_NAME}", _funding.ofm_facility?.name);
            emaildescription = emaildescription?.Replace("{HYPERLINK_FA}", $"<a href=\"{hyperlink}\">View Funding</a>");
            emaildescription = emaildescription?.Replace("{HYPERLINK_FATAB}", $"<a href=\"{hyperlinkFATab}\">Funding Overview</a>");

            await CreateAndUpdateEmail(subject, emaildescription, expenseOfficer, _fundingAgreementCommunicationType, appUserService, d365WebApiService, _processParams);

            if (expenseOfficer != primaryContact)
            {
                templateobj = serializedDataTemplate.Where(t => t.templateid == _notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 215).TemplateId);
                subject = templateobj?.Select(s => s.title).FirstOrDefault();
                emaildescription = templateobj?.Select(sh => sh.safehtml).FirstOrDefault();
                emaildescription = emaildescription?.Replace("{HYPERLINK_FA}", $"<a href=\"{hyperlink}\">View Funding</a>");

                await CreateAndUpdateEmail(subject, emaildescription, primaryContact, _informationCommunicationType, appUserService, d365WebApiService, _processParams);
            }
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;

        #endregion
    }

    #region Create and Update Email

    private async Task<JsonObject> CreateAndUpdateEmail(string subject, string emailDescription, Guid? toRecipient, string communicationType, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        var requestBody = new JsonObject(){
                            {"subject",subject },
                            {"description",emailDescription },
                            {"email_activity_parties", new JsonArray(){
                                new JsonObject
                                {
                                    { "partyid_systemuser@odata.bind", $"/systemusers({_processParams.Notification?.SenderId})"},
                                    { "participationtypemask", 1 } //From Email
                                },
                                new JsonObject
                                {
                                    { "partyid_contact@odata.bind", $"/contacts({toRecipient})" },
                                    { "participationtypemask",   2 } //To Email                             
                                }
                            }},
                            { "ofm_communication_type_Email@odata.bind", $"/ofm_communication_types({communicationType})"}
                        };

        var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, EntityNameSet, requestBody.ToString());

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            //log the error
            return await Task.FromResult<JsonObject>(new JsonObject() { });
        }

        var newEmail = await response.Content.ReadFromJsonAsync<JsonObject>();

        var newEmailId = newEmail?["activityid"];

        var email = $"emails({newEmailId})";

        var payload = new JsonObject {
                        { "ofm_sent_on", DateTime.UtcNow },
                        { "statuscode", 6 },   // 6 = Pending Send 
                        { "statecode", 1 }};

        var requestBody1 = JsonSerializer.Serialize(payload);

        var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, email, requestBody1);

        if (!patchResponse.IsSuccessStatusCode)
        {
            var responseBody = await patchResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());

            return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
   
    #endregion

    #region Private methods
    //private async Task SetupCommunicationTypes()
    //{
    //    var fetchXml = """
    //                        <fetch distinct="true" no-lock="true">
    //                          <entity name="ofm_communication_type">
    //                            <attribute name="ofm_communication_typeid" />
    //                            <attribute name="ofm_communication_type_number" />
    //                            <attribute name="ofm_name" />
    //                            <attribute name="statecode" />
    //                            <attribute name="statuscode" />
    //                            <filter>
    //                              <condition attribute="statecode" operator="eq" value="0" />
    //                            </filter>
    //                          </entity>
    //                        </fetch>
    //            """;

    //    var requestUri = $"""
    //                          ofm_communication_types?fetchXml={WebUtility.UrlEncode(fetchXml)}
    //                          """;

    //    var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUri);

    //    if (!response.IsSuccessStatusCode)
    //    {
    //        var responseBody = await response.Content.ReadAsStringAsync();
    //        _logger.LogError(CustomLogEvent.Process, "Failed to query the communcation types with a server error {responseBody}", responseBody.CleanLog());
    //    }

    //    var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

    //    JsonNode d365Result = string.Empty;
    //    if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
    //    {
    //        if (currentValue?.AsArray().Count == 0)
    //        {
    //            _logger.LogInformation(CustomLogEvent.Process, "No communcation types found with query {requestUri}", requestUri);
    //        }
    //        d365Result = currentValue!;
    //    }

    //    _fundingAgreementCommunicationType = d365Result.AsArray().Where(type => type?["ofm_communication_type_number"]?.ToString() == _notificationSettings.CommunicationTypes.FundingAgreement.ToString())
    //                                                               .Select(type => type?["ofm_communication_typeid"]!.ToString())!.ToArray<string>();

    //    _informationCommunicationType = d365Result.AsArray().Where(type => type?["ofm_communication_type_number"]?.ToString() == _notificationSettings.CommunicationTypes.Information.ToString())
    //                                                               .Select(type => type?["ofm_communication_typeid"]!.ToString())!.ToArray<string>();

    //}
    #endregion
}