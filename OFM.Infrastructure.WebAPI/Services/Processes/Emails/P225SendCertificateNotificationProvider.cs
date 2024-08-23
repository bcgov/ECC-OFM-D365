using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;


namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    public class P225SendCertificateNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IEmailRepository emailRepository) : ID365ProcessProvider
    {
        private readonly IEmailRepository _emailRepository = emailRepository;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
        private ProcessParameter? _processParams;

        public short ProcessId => Setup.Process.Emails.CreateECENotificationsId;
        public string ProcessName => Setup.Process.Emails.CreateECENotificationsName;

        //To retrieve submitted application.
        private string RetrieveApplications
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="ofm_provider_employee">
                    <attribute name="ofm_provider_employeeid" />
                    <attribute name="ofm_caption" />
                    <attribute name="createdon" />
                    <attribute name="ofm_initials" />
                    <attribute name="ofm_certificate_number" />
                    <order attribute="ofm_caption" descending="false" />
                    <filter type="and">
                      <condition attribute="ofm_application" operator="eq" value="{_processParams?.Application.applicationId}"/>
                       <condition attribute="ofm_employee_type" operator="in">
                        <value>1</value>
                        <value>2</value>
                      </condition>
                     <condition attribute="ofm_certificate_status" operator="eq" value="0" />
                    </filter>
                    <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" visible="false" link-type="outer" alias="application">
                      <attribute name="ofm_contact" />
                      <attribute name="ofm_application" />
                    </link-entity>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                         ofm_provider_employees?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;
                return requestUri.CleanCRLF();
            }
        }

        //To retrieve submitted report
        private string RetrieveReportStaff
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="ofm_employee_certificate_status">
                    <attribute name="ofm_employee_certificate_statusid" />
                    <attribute name="ofm_caption" />
                    <attribute name="createdon" />
                    <attribute name="ofm_initials" />
                    <attribute name="ofm_certificate_number" />
                    <attribute name="ofm_certificate_status" />
                    <order attribute="ofm_caption" descending="false" />
                    <filter type="and">
                    <condition attribute="ofm_survey_response" operator="eq"  value="{_processParams?.FundingReport?.FundingReportId}"/>
                   <condition attribute="ofm_certificate_status" operator="eq" value="0" />
                    </filter>
                    <link-entity name="ofm_survey_response" from="ofm_survey_responseid" to="ofm_survey_response" visible="false" link-type="outer" alias="report">
                      <attribute name="ofm_contact" />
                       <attribute name="ofm_facility" />
                        <attribute name="ofm_name" />
                 <link-entity name="account" from="accountid" to="ofm_facility" alias="facility">
                  <attribute name="ofm_primarycontact" />
                </link-entity>
                    </link-entity>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                         ofm_employee_certificate_statuses?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;
                return requestUri.CleanCRLF();
            }
        }

        public async Task<ProcessData> GetDataAsync()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            _logger.LogDebug(CustomLogEvent.Process, "Calling GetSupplementaryApplicationDataAsync");
            if(!String.IsNullOrEmpty(_processParams?.FundingReport?.FundingReportId))
             response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveReportStaff, formatted: true, isProcess: true);
            else
             response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveApplications, formatted: true, isProcess: true);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No supplementary applications found with query {requestUri}", RetrieveApplications.CleanLog());
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

            string staffdetails = "<ul>";
            List<ProviderStaff> _staffs = new List<ProviderStaff>();
            IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();

            var _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.ActionRequired)
                                                                         .Select(s => s.ofm_communication_typeid).FirstOrDefault();

            _staffs = string.IsNullOrEmpty(localData.Data.ToString()) ? null : System.Text.Json.JsonSerializer.Deserialize<List<ProviderStaff>>(localData.Data.ToString());

            #region Create the ECE email notifications 
            // Get template details to create emails.
            if (_staffs?.Count > 0)
            {
                Guid facility_primarycontact = (Guid)_staffs?[0].FacilityContactId;
                Guid report_provider = (Guid)_staffs?[0].ProviderId; ;
                foreach (var staff in _staffs)
                {
                    staffdetails += "<li>" + staff.Initials + " : " + staff.CertificateNumber + "</li>";
                }
                staffdetails += "</ul>";
                var localDataTemplate = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 245).TemplateNumber);
                var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
                var templateobj = serializedDataTemplate?.FirstOrDefault();
                string? subject = _emailRepository.StripHTML(templateobj?.subjectsafehtml);
                string? emaildescription = templateobj?.safehtml;
                emaildescription = emaildescription?.Replace("{Record_Name}", _staffs?[0].Name);
                emaildescription = emaildescription?.Replace("{Provider_Name}", _staffs?[0].ProviderName);
                emaildescription = emaildescription?.Replace("{Staff}", staffdetails);
                List<Guid> recipientsList = new List<Guid>();
                recipientsList.Add(report_provider);
                if(facility_primarycontact != report_provider)
                   recipientsList.Add(facility_primarycontact);

                await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams?.Notification?.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 225);

            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;

            #endregion
        }

    }
}
