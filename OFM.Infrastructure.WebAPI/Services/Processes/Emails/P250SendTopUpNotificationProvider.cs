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
                    <attribute name="ofm_funding_number" />
                    <filter>
                      <condition attribute="ofm_top_up_fundid" operator="eq" value="{_processParams.Topup.TopupId}" />
                    </filter>
                    <link-entity name="ofm_funding" from="ofm_fundingid" to="ofm_funding" link-type="outer">
                      <attribute name="statecode" />
                      <attribute name="statuscode" />
                      <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application">
                        <attribute name="ofm_contact" />
                        <attribute name="ofm_expense_authority" />
                      </link-entity>
                    </link-entity>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                         ofm_top_up_funds?$select=ofm_funding_number,ofm_end_date,_ofm_facility_value,_ofm_funding_value,ofm_name,ofm_programming_amount,ofm_start_date,statuscode,ofm_top_up_fundid&$expand=ofm_funding($select=statecode,statuscode;$expand=ofm_application($select=_ofm_contact_value,_ofm_expense_authority_value))&$filter=(ofm_top_up_fundid eq {_processParams.Topup.TopupId})
                         """;
                return requestUri.CleanCRLF();
            }
        }

        private string RetrieveTopUpNotification
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $"""
                <fetch>
                  <entity name="email">
                    <attribute name="subject" />
                    <filter>
                      <condition attribute="ofm_regarding_data" operator="eq" value="{string.Format("{0}#ofm_top_up_funds", _processParams?.Topup?.TopupId)}" />
                    </filter>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                         emails?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri.CleanCRLF();
            }
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

        public async Task<ProcessData> GetTopupNotification()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetTopupNotification));

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveTopUpNotification, false, 0, true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query notification record information with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {

                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            _processParams = processParams;

            if (_processParams == null || _processParams.Topup == null || _processParams.Topup.TopupId == null)
            {
                _logger.LogError(CustomLogEvent.Process, "TopupId is missing.");
                throw new Exception("TopupId is missing.");

            }

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

                var faState = topUpData.ofm_funding.statecode;
                if (faState == ofm_funding_statecode.Inactive)
                {
                    _logger.LogError(CustomLogEvent.Process, "Funding is Inactive with Id {FundingId}", processParams.Funding!.FundingId);
                    return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                }


                //If the status is approved, need to check FA status and if previous notification generated
                if (statusReason == ofm_top_up_fund_StatusCode.Approved)
                {
                    var faStatus = topUpData.ofm_funding.statuscode;
                    if(faStatus != ofm_funding_StatusCode.Active)
                    {
                        _logger.LogError(CustomLogEvent.Process, "Funding is not active with Id {FundingId}", processParams.Funding!.FundingId);
                        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                    }

                    var topUpNotification = await GetTopupNotification();

                    if (topUpNotification.Data.AsArray().Count > 0)
                    {
                        var approvalNotifications = topUpNotification.Data.AsArray().Where(notification => notification["subject"].ToString().Contains("Final")).ToList();
                        if(approvalNotifications.Count > 0)
                        {
                            _logger.LogError(CustomLogEvent.Process, "Approval Notification is sent with TopUp {TopUpId}", processParams.Topup!.TopupId);
                            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                        }
                    }
                }

                if (statusReason == ofm_top_up_fund_StatusCode.Draft)
                {

                    var faStatus = topUpData.ofm_funding.statuscode;
                    if (faStatus == ofm_funding_StatusCode.Draft || faStatus == ofm_funding_StatusCode.FAReview)
                    {
                        _logger.LogError(CustomLogEvent.Process, "Funding is still in Draft or Review with Id {FundingId}", processParams.Funding!.FundingId);
                        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                    }

                    var topUpNotification = await GetTopupNotification();

                    if (topUpNotification.Data.AsArray().Count > 0)
                    {
                        var draftNotifications = topUpNotification.Data.AsArray().Where(notification => notification["subject"].ToString().Contains("Draft")).ToList();
                        if (draftNotifications.Count > 0)
                        {
                            _logger.LogError(CustomLogEvent.Process, "Draft Notification is sent with TopUp {TopUpId}", processParams.Topup!.TopupId);
                            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                        }
                    }
                }

                // Get template details to create emails.                
                var localDataTemplate = await _emailRepository.GetTemplateDataAsync(Int32.Parse(_processParams.Notification.TemplateNumber));

                var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
                _logger.LogInformation("Got the Template", serializedDataTemplate.Count);

                var expenseAuthorityContact = topUpData.ofm_funding?.ofm_application?._ofm_expense_authority_value;
                var primaryContact = topUpData.ofm_funding.ofm_application._ofm_contact_value;

                var templateobj = serializedDataTemplate?.FirstOrDefault();
                string? subject = _emailRepository.StripHTML(templateobj?.subjectsafehtml);
                string? emaildescription = templateobj?.safehtml;
                string? fundingNumber = topUpData.ofm_funding_number;
                subject = subject.Replace("#FANumber#", fundingNumber);
                string regardingData = string.Empty;

                if (expenseAuthorityContact != null && expenseAuthorityContact != Guid.Empty)
                {
                    recipientsList.Add((Guid)expenseAuthorityContact);
                    regardingData = string.Format("{0}#ofm_top_up_funds", _processParams?.Topup?.TopupId);
                    await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 250, regardingData);

                }
                else if(primaryContact != null && primaryContact != Guid.Empty)
                {
                    recipientsList.Add((Guid)primaryContact);
                    regardingData = string.Format("{0}#ofm_top_up_funds", _processParams?.Topup?.TopupId);
                    await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 250, regardingData);
                }

            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;

            #endregion
        }
    }
}