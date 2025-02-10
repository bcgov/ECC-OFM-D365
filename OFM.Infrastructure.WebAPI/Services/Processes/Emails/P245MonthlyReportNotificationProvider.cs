using ECC.Core.DataContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.FundingReports;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P245MonthlyReportNotificationProvider : ID365ProcessProvider
{
    const string EntityNameSet = "emails";
    private readonly NotificationSettings _notificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly IEmailRepository _emailRepository;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string? _informationCommunicationType;
    private List<D365Email> _createdEmail;

    public P245MonthlyReportNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IEmailRepository emailRepository)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _emailRepository = emailRepository;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Emails.CreateMonthlyReportNotificationId;
    public string ProcessName => Setup.Process.Emails.CreateMonthlyReportNotificationName;

    #region fetchxml queries
    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch version="1.0" mapping="logical" distinct="true" no-lock="true">
                      <entity name="contact">
                        <attribute name="emailaddress1" />
                        <attribute name="ofm_first_name" />
                        <attribute name="ofm_last_name" />
                        <link-entity name="account" from="ofm_primarycontact" to="contactid">
                          <link-entity name="ofm_survey_response" from="ofm_facility" to="accountid">
                            <filter>
                              <condition attribute="createdon" operator="on" value="{}" />
                              <condition attribute="ofm_start_date" operator="on" value="{_processParams.Notification.startDate}" />
                            </filter>
                          </link-entity>
                        </link-entity>
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
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P245MonthlyReportNotificationProvider));

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
        _processParams = processParams;
        var startTime = _timeProvider.GetTimestamp();

        IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
        _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                .Select(s => s.ofm_communication_typeid).FirstOrDefault();

        
        var localData = await GetDataAsync();
        var deserializedData = JsonSerializer.Deserialize<List<Contact>>(localData.Data.ToString());
        if (deserializedData == null || deserializedData.Count == 0)
        {
            _logger.LogInformation("No records returned from FetchXml", deserializedData.Count);
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }
        var recipientsList = deserializedData.Select(contact => contact.Id).Distinct().ToList();
        var localDataTemplate = await _emailRepository.GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.TemplateNumber == 215).TemplateNumber);
        var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
        var templateobj = serializedDataTemplate?.FirstOrDefault();
        var subject = _emailRepository.StripHTML(templateobj?.subjectsafehtml);
        var emaildescription = templateobj?.safehtml;

        await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 245);


        var result = ProcessResult.Success(ProcessId, deserializedData!.Count);

        var endTime = _timeProvider.GetTimestamp();

        _logger.LogInformation(CustomLogEvent.Process, "Send Notification process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, result.ToString());

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;


    }


}