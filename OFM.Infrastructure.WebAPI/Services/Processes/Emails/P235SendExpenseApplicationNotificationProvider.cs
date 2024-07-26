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

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    public class P235SendExpenseApplicationNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IEmailRepository emailRepository) : ID365ProcessProvider
    {
        private readonly IEmailRepository _emailRepository = emailRepository;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
        private ProcessParameter? _processParams;

        public short ProcessId => Setup.Process.Emails.CreateExpenseApplicationNotificationsId;
        public string ProcessName => Setup.Process.Emails.CreateExpenseApplicationNotificationsName;

        //To retrieve Expense application.
        private string RetrieveExpenseApplication
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                 <entity name='ofm_expense'>
                    <attribute name='ofm_expenseid' />
                    <attribute name='ofm_application' />
                    <attribute name='ofm_assistance_request' />
                    <attribute name='ofm_caption' />                
                    <attribute name='statuscode' />
                    <link-entity name='ofm_assistance_request' to='ofm_assistance_request' from='ofm_assistance_requestid' alias='ofm_assistance_request' link-type='outer'>
                      <attribute name='ofm_assistance_requestid' />
                      <attribute name='ofm_contact' />
                    </link-entity>
                    <link-entity name='ofm_application' to='ofm_application' from='ofm_applicationid' alias='ofm_application' link-type='outer'>
                      <attribute name='ofm_contact' />
                    </link-entity>
                    <filter>
                      <condition attribute='ofm_expenseid' operator='eq' value='{_processParams.ExpenseApplication.expenseId}' />
                    </filter>
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

            _logger.LogDebug(CustomLogEvent.Process, "Calling GetExpenseApplicationDataAsync");

            response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveExpenseApplication, formatted: true, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query expense application data to send notification with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No expense applications found with query {requestUri}", RetrieveExpenseApplication.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            _processParams = processParams;

            var localData = await GetDataAsync();

            if (localData == null || localData.Data == null)
            {
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            Guid mainApplicantContact = localData.Data[0]["ofm_assistance_request.ofm_contact"] != null ? (Guid)localData.Data[0]["ofm_assistance_request.ofm_contact"] : Guid.Empty;
            Guid facilityContact = localData.Data[0]["ofm_application.ofm_contact"] != null ? (Guid)localData.Data[0]["ofm_application.ofm_contact"] : Guid.Empty;
            string expenseCaption = (string)localData.Data[0]["ofm_caption"];

            IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();

            var _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                                         .Select(s => s.ofm_communication_typeid).FirstOrDefault();

            int statusReason = (int)localData.Data[0]["statuscode"];
            List<Guid> recipientsList = new List<Guid>();

            #region CreateEmailNotification

            if (statusReason == (int)ofm_expense_StatusCode.Approved || statusReason == (int)ofm_expense_StatusCode.Ineligible)
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
                string? subject = templateobj?.title;
                string? emaildescription = templateobj?.safehtml;
                string? emailBody = emaildescription?.Replace("{CONTACT_NAME}", $"{firstName} {lastName}");
                string regardingData = string.Empty;

                if (mainApplicantContact != Guid.Empty)
                {
                    recipientsList.Add(mainApplicantContact);
                    regardingData = statusReason == (int)ofm_expense_StatusCode.Approved ? string.Format("{0}#ofm_expense", _processParams.ExpenseApplication.expenseId) : string.Empty;

                    await _emailRepository.CreateAndUpdateEmail(subject, emailBody, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 235, regardingData);

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
                    emailBody = emaildescription?.Replace("{CONTACT_NAME}", $"{firstName} {lastName}");

                    await _emailRepository.CreateAndUpdateEmail(subject, emailBody, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 235, regardingData);
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