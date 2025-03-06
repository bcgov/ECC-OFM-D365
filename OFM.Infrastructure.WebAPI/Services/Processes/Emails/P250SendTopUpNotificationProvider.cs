using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models.Fundings;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    public class P250SendTopUpNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IEmailRepository emailRepository) : ID365ProcessProvider
    {
        private readonly IEmailRepository _emailRepository = emailRepository;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
        private ProcessParameter? _processParams;

        public short ProcessId => Setup.Process.Emails.CreateTopUpNotificationId;
        public string ProcessName => Setup.Process.Emails.CreateTopUpNotificationName;

        //To retrieve TopUp record
        private string RetrieveTopUp
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="ofm_top_up_fund">
                    <attribute name="ofm_end_date" />
                    <attribute name="ofm_facility" />
                    <attribute name="ofm_funding" />
                    <attribute name="ofm_name" />
                    <attribute name="ofm_programming_amount" />
                    <attribute name="ofm_start_date" />
                    <attribute name="statuscode" />
                    <attribute name="ofm_top_up_fundid" />
                    <filter>
                      <condition attribute="ofm_top_up_fundid" operator="eq" value="{_processParams.Topup.TopupId}" />
                    </filter>
                    <link-entity name="ofm_funding" from="ofm_fundingid" to="ofm_funding" link-type="outer">
                      <attribute name="statecode" />
                      <attribute name="statuscode" />
                      <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application">
                        <attribute name="ofm_contact" />
                        <link-entity name="contact" from="contactid" to="ofm_contact" link-type="outer">
                          <attribute name="ofm_first_name" />
                          <attribute name="ofm_last_name" />
                        </link-entity>
                      </link-entity>
                    </link-entity>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                         ofm_expenses?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;
                return requestUri.CleanCRLF();
            }
        }

        /// <summary>
        /// To frame ContactRequestUri
        /// </summary>
        /// <param name="contactId">GUID of contact</param>
        /// <returns>ContactRequestUri</returns>
        public string ContactRequestUri(string contactId)
        {

            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="contact">
                        <attribute name="contactid" />
                        <attribute name="ofm_first_name" />
                        <attribute name="ofm_last_name" />
                        <filter>
                           <condition attribute="contactid" operator="eq" value="{contactId}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         contacts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;

        }
        public async Task<ProcessData> GetDataAsync()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            _logger.LogDebug(CustomLogEvent.Process, "Calling GetTopUpDataAsync");

            response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveTopUp, formatted: true, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query top-up data to send notification with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No top-up found with query {requestUri}", RetrieveTopUp.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            
            if (_processParams == null || _processParams.Topup == null || _processParams.Topup.TopupId == null)
            {
                _logger.LogError(CustomLogEvent.Process, "TopupId is missing.");
                throw new Exception("TopupId is missing.");

            }

            _processParams = processParams;

            var localData = await GetDataAsync();

            if (localData == null || localData.Data == null)
            {
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            if (localData.Data.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "Send TopUp Notification process completed. No topup found.");
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            var topUpData = JsonSerializer.Deserialize<List<TopUp>>(localData.Data.ToString())?.FirstOrDefault();

            IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();

            var _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                                         .Select(s => s.ofm_communication_typeid).FirstOrDefault();

            var statusReason = topUpData?.statuscode;

            List<Guid> recipientsList = new List<Guid>();

            #region CreateDraftEmailNotification

            if (statusReason == ofm_top_up_fund_StatusCode.Draft || statusReason == ofm_top_up_fund_StatusCode.Approved)
            {
                _logger.LogInformation("Entered statusReason:", statusReason);
                
                // Get template details to create emails.                
                var localDataTemplate = await _emailRepository.GetTemplateDataAsync(Int32.Parse(_processParams.Notification.TemplateNumber));

                var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
                _logger.LogInformation("Got the Template", serializedDataTemplate.Count);

                var localDataContact = await GetContactDataAsync(mainApplicantContact.ToString());
                var deserializedData = JsonSerializer.Deserialize<List<D365Contact>>(localDataContact.Data.ToString());
                var contactobj = deserializedData?.FirstOrDefault();
                var firstName = contactobj?.ofm_first_name;
                var lastName = contactobj?.ofm_last_name;

                var templateobj = serializedDataTemplate?.FirstOrDefault();
                string? subject = _emailRepository.StripHTML(templateobj?.subjectsafehtml);
                string? emaildescription = templateobj?.safehtml;
                string? fundingNumber = localData.Data[0]?["ofm_funding_number"]?.ToString();
                subject = subject.Replace("#FANumber#", fundingNumber);
                string regardingData = string.Empty;

                Guid mainApplicantContact = localData.Data[0]["ofm_assistance_request.ofm_contact"] != null ? (Guid)localData.Data[0]["ofm_assistance_request.ofm_contact"] : Guid.Empty;
                Guid facilityContact = localData.Data[0]["ofm_application.ofm_contact"] != null ? (Guid)localData.Data[0]["ofm_application.ofm_contact"] : Guid.Empty;

                if (mainApplicantContact != Guid.Empty)
                {
                    recipientsList.Add(mainApplicantContact);
                    regardingData = statusReason == (int)ofm_expense_StatusCode.Approved ? string.Format("{0}#ofm_expense", _processParams.ExpenseApplication.expenseId) : string.Empty;

                    await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 235, regardingData);

                }

                if (facilityContact != Guid.Empty && facilityContact != mainApplicantContact)
                {
                    recipientsList.Clear();
                    recipientsList.Add(facilityContact);

                    regardingData = statusReason == (int)ofm_expense_StatusCode.Approved && mainApplicantContact == Guid.Empty ? string.Format("{0}#ofm_expense", _processParams.ExpenseApplication.expenseId) : string.Empty;

                    localDataContact = await GetContactDataAsync(facilityContact.ToString());
                    deserializedData = JsonSerializer.Deserialize<List<D365Contact>>(localDataContact.Data.ToString());
                    contactobj = deserializedData?.FirstOrDefault();
                    firstName = contactobj?.ofm_first_name;
                    lastName = contactobj?.ofm_last_name;

                

                    await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 235, regardingData);
                }

            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;

            #endregion
        }

        /// <summary>
        /// To get contact details
        /// </summary>
        /// <param name="contactId">GUID of contact</param>
        /// <returns>Contact details</returns>
        public async Task<ProcessData> GetContactDataAsync(string contactId)
        {
            _logger.LogDebug(CustomLogEvent.Process, "GetContactDataAsync");

            var contactRequestUri = this.ContactRequestUri(contactId);
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, contactRequestUri);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No Contact records found with query {requestUri}", contactRequestUri.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }
    }
}