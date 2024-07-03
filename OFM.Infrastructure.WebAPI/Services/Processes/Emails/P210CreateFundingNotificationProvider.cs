using ECC.Core.DataContext;
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
    private string _contactId;

    public P210CreateFundingNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IFundingRepository fundingRepository, IEmailRepository emailRepository)
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

    #region fetchxml queries
    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="contact">
                        <attribute name="contactid" />
                        <attribute name="ofm_first_name" />
                        <attribute name="ofm_last_name" />
                        <filter>
                           <condition attribute="contactid" operator="eq" value="{_contactId}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         contacts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }
    #endregion

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "GetContactDataAsync");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Contact records with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Contact records found with query {requestUri}", RequestUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
        _fundingAgreementCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.FundingAgreement)
                                                                     .Select(s => s.ofm_communication_typeid).FirstOrDefault();

        _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                                   .Select(s => s.ofm_communication_typeid).FirstOrDefault();
        _processParams = processParams;
        Funding? _funding = await _fundingRepository!.GetFundingByIdAsync(new Guid(processParams.Funding!.FundingId!));
        var expenseOfficer = _funding.ofm_application!._ofm_expense_authority_value;
        var primaryContact = _funding.ofm_application!._ofm_contact_value;
        var providerApprover = _funding._ofm_provider_approver_value;      // Provider FA Approver

        int statusReason = (int)_funding!.statuscode;                      // funding status

        _contactId = providerApprover.ToString();
        var localData = await GetDataAsync();
        var deserializedData = JsonSerializer.Deserialize<List<D365Contact>>(localData.Data.ToString());

        var contactobj = deserializedData!.FirstOrDefault();
        var firstName = contactobj!.ofm_first_name;
        var lastName = contactobj!.ofm_last_name;

        var startTime = _timeProvider.GetTimestamp();

        #region Create the funding email notifications 


        if (statusReason == (int)ofm_funding_StatusCode.FASignaturePending)
        {
            // Get template details to create emails.
            var localDataTemplate = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 210).TemplateNumber);

            var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
            var hyperlink = _notificationSettings.FundingUrl + _funding.Id;
            var hyperlinkFATab = _notificationSettings.FundingTabUrl;

            var templateobj = serializedDataTemplate?.FirstOrDefault();
            string? subject = templateobj?.title;
            string? emaildescription = templateobj?.safehtml;
            emaildescription = emaildescription?.Replace("{FA_NUMBER}", _funding.ofm_funding_number);
            emaildescription = emaildescription?.Replace("{FACILITY_NAME}", _funding.ofm_facility?.name);
            emaildescription = emaildescription?.Replace("{HYPERLINK_FA}", $"<a href=\"{hyperlink}\">View Funding</a>");
            emaildescription = emaildescription?.Replace("{HYPERLINK_FATAB}", $"<a href=\"{hyperlinkFATab}\">Funding Overview</a>");
            List<Guid> recipientsList = new List<Guid>();
            recipientsList.Add((Guid)expenseOfficer);

            await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _fundingAgreementCommunicationType, appUserService, d365WebApiService, 210);

            if (expenseOfficer != primaryContact)
            {

                localDataTemplate = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 215).TemplateNumber);
                serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
                templateobj = serializedDataTemplate?.FirstOrDefault();
                subject = templateobj?.title;
                emaildescription = templateobj?.safehtml;
                emaildescription = emaildescription?.Replace("{HYPERLINK_FA}", $"<a href=\"{hyperlink}\">View Funding</a>");
                recipientsList.Clear();
                recipientsList.Add((Guid)primaryContact);
                await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 210);
            }
        }

        if (statusReason == (int)ofm_funding_StatusCode.Active)
        {
            // Get template details to create emails.
            var localDataTemplate = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 235).TemplateNumber);

            var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());

            var templateobj = serializedDataTemplate?.FirstOrDefault();
            string? subject = templateobj?.subjectsafehtml;
            string? emaildescription = templateobj?.safehtml;
            emaildescription = emaildescription?.Replace("{CONTACT_NAME}", $"{firstName} {lastName}");
            List<Guid> recipientsList = new List<Guid>();
            recipientsList.Add((Guid)providerApprover);
            await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 210);
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;

        #endregion
    }
}