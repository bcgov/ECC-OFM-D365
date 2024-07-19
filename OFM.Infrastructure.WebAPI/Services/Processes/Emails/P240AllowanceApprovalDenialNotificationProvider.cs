using ECC.Core.DataContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json.Linq;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P240AllowanceApprovalDenialNotificationProvider : ID365ProcessProvider
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
    private Guid? _allowanceId;
    private bool emailCreated = false;

    public P240AllowanceApprovalDenialNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IFundingRepository fundingRepository, IEmailRepository emailRepository)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _fundingRepository = fundingRepository;
        _emailRepository = emailRepository;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Emails.AllowanceApprovalDenialNotificationId;
    public string ProcessName => Setup.Process.Emails.AllowanceApprovalDenialNotificationName;

    #region fetchxml queries
    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch>
                      <entity name="ofm_allowance">
                        <attribute name="ofm_allowance_number" />
                        <attribute name="ofm_allowance_type" />
                        <attribute name="ofm_allowanceid" />
                        <attribute name="ofm_monthly_amount" />
                        <attribute name="statuscode" />
                    <attribute name="ofm_start_date" />
                    <attribute name="ofm_transport_vehicle_vin" />
                    <attribute name="ofm_retroactive_date" />
                    <attribute name="ofm_retroactive_amount" />
                    
                        <filter>
                          <condition attribute="ofm_allowanceid" operator="eq" value="{_allowanceId}" />
                        </filter>
                        <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" alias= "app">
                          <attribute name="ofm_contact" />
                          <attribute name="statuscode" />
                          <attribute name="ofm_summary_submittedby" />
                        <attribute name="ofm_funding_number_base" />
                          <link-entity name="contact" from="contactid" to="ofm_contact" alias= "con">
                            <attribute name="ofm_last_name" />
                            <attribute name="ofm_first_name" />
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_allowances?fetchXml={WebUtility.UrlEncode(fetchXml)}
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
        _processParams = processParams;
        _allowanceId = _processParams.SupplementaryApplication.allowanceId;

        IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
        _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                .Select(s => s.ofm_communication_typeid).FirstOrDefault();
        var localData = await GetDataAsync();
        var deserializedData = JsonSerializer.Deserialize<List<SupplementaryApplication>>(localData.Data.ToString());
        if (deserializedData == null || deserializedData.Count == 0)
        {
            _logger.LogInformation("No records returned from FetchXml", deserializedData.Count);
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }
        try
        {
            await _emailRepository.CreateAllowanceEmail(deserializedData.First(), _processParams.Notification.SenderId, _informationCommunicationType, ProcessId, d365WebApiService);
        }
        catch (Exception ex)
        {
            _logger.LogError(CustomLogEvent.Process, "Failed to generate email notification for supplementary {responseBody}", ex.InnerException.Message);

        }
        return ProcessResult.Completed(ProcessId).SimpleProcessResult;


        }

   
}


// Provider FA Approver
