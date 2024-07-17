using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using ECC.Core.DataContext;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;


namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    public class P230SendApplicationNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IEmailRepository emailRepository) : ID365ProcessProvider
    {
        private readonly IEmailRepository _emailRepository = emailRepository;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
        private ProcessParameter? _processParams;

        public short ProcessId => Setup.Process.Emails.CreateApplicationNotificationsId;
        public string ProcessName => Setup.Process.Emails.CreateApplicationNotificationsName;

        //To retrieve submitted application.
        private string RetrieveApplications
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="ofm_application">
                    <attribute name="ofm_applicationid" />
                    <attribute name="ofm_application" />
                    <attribute name="ofm_summary_submittedby" />
                    <attribute name="ofm_contact" />
                    <attribute name="statuscode" />
                    <order attribute="ofm_application" descending="false" />
                    <filter type="and">
                      <condition attribute="ofm_applicationid" operator="eq" value="{_processParams.Application.applicationId}" />
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

        public async Task<ProcessData> GetDataAsync()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            _logger.LogDebug(CustomLogEvent.Process, "Calling GetApplicationDataAsync");
            
             response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveApplications, formatted: true, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query  application data to snd notificatione with the server error {responseBody}", responseBody.CleanLog());

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

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            _processParams = processParams;
            _logger.LogInformation("EnteredRunProcessAsync", _processParams.Application.applicationId);
            var localData = await GetDataAsync();
            var deserializedData = JsonSerializer.Deserialize<List<Application>>(localData.Data.ToString());
            if (deserializedData == null || deserializedData.Count == 0)
            {
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }
            Guid primaryContact = (Guid)deserializedData.First()._ofm_contact_value != null ? (Guid)deserializedData.First()._ofm_contact_value : Guid.Empty;
            Guid submittedBy =deserializedData.First()._ofm_summary_submittedby_value != null ? (Guid)deserializedData.First()._ofm_summary_submittedby_value : Guid.Empty;

            IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();

            var _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                                         .Select(s => s.ofm_communication_typeid).FirstOrDefault();

            int statusReason = (int)deserializedData?.First().statuscode;
            List<Guid> recipientsList = new List<Guid>();

            #region CreateEmailNotification
         
                if (statusReason == (int)ofm_application_StatusCode.Ineligible)
                {
                    _logger.LogInformation("Entered Ineligible", statusReason);
                    // Get template details to create emails.
                    var localDataTemplate = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 250).TemplateNumber);


                    var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
                    _logger.LogInformation("Got the Template", serializedDataTemplate.Count);

                    var templateobj = serializedDataTemplate?.FirstOrDefault();
                    string? subject = templateobj?.title;
                    string? emaildescription = templateobj?.safehtml;
                if (submittedBy != Guid.Empty)
                {
                    _logger.LogInformation("Got the recipientsList submittedBy", submittedBy);
                    recipientsList.Add(submittedBy);
                }
                if (submittedBy != Guid.Empty && submittedBy != primaryContact)
                {
                    _logger.LogInformation("Got the recipientsList primaryContact", primaryContact);
                    recipientsList.Add(primaryContact);
                   
                }
                await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 210);

            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;

            #endregion
    }

    }
}
