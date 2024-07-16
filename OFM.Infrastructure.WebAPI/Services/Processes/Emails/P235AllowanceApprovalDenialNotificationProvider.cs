﻿using ECC.Core.DataContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P235AllowanceApprovalDenialNotificationProvider : ID365ProcessProvider
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

    public P235AllowanceApprovalDenialNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IFundingRepository fundingRepository, IEmailRepository emailRepository)
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
                        <attribute name="ofm_funding_amount" />
                        <attribute name="statuscode" />
                    <attribute name="ofm_start_date" />
                    <attribute name="ofm_transport_vehicle_vin" />
                    <attribute name="ofm_retroactive_date" />
                    
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
        int TemplateNumber = _processParams.Notification.TemplateNumber != null ? int.Parse(_processParams.Notification.TemplateNumber) : -1;

        var localData = await GetDataAsync();
        var deserializedData = JsonSerializer.Deserialize<List<SupplementaryApplication>>(localData.Data.ToString());
        if (deserializedData == null || deserializedData.Count == 0 || TemplateNumber == -1)
        {
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }
        int appStatusCode = deserializedData.First().appstatuscode;
        var contactName = deserializedData.First().ofm_first_name + " " + deserializedData.First().ofm_last_name;
        var fundingAmount = deserializedData.First().ofm_funding_amount;
        var allowanceType = (ecc_allowance_type)deserializedData.First().ofm_allowance_type;
        var allowanceNumber = deserializedData.First().ofm_allowance_number;
        var statusReason = deserializedData.First().statuscode;
        Guid primaryContact = (Guid)deserializedData.First()._ofm_contact_value != null ? (Guid)deserializedData.First()._ofm_contact_value : Guid.Empty;
        Guid submittedBy = deserializedData.First()._ofm_summary_submittedby_value != null ? (Guid)deserializedData.First()._ofm_summary_submittedby_value : Guid.Empty;
        var fundingNumber = deserializedData.First().ofm_funding_number_base;
        var effectiveDate = deserializedData.First().ofm_start_date;
        var VIN = deserializedData.First().ofm_transport_vehicle_vin;
        // DateOnly retroActiveDate = deserializedData.First().ofm_ret;
        IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
        _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                                   .Select(s => s.ofm_communication_typeid).FirstOrDefault();
        // funding status
        _logger.LogInformation("Got the Status", statusReason);

        var startTime = _timeProvider.GetTimestamp();

        #region Create the funding email notifications 


        if (appStatusCode == (int)ofm_application_StatusCode.Approved && statusReason == (int)ofm_allowance_StatusCode.Approved)
        {
            ProcessData localDataTemplate = null;


            _logger.LogInformation("Entered if Approved", statusReason);
            if (allowanceType == ecc_allowance_type.SupportNeedsProgramming)
                // Get template details to create emails.
                localDataTemplate = await _emailRepository.GetTemplateDataAsync(TemplateNumber); //240
            else if (allowanceType == ecc_allowance_type.IndigenousProgramming)
                localDataTemplate = await _emailRepository.GetTemplateDataAsync(TemplateNumber);//255
            else if (allowanceType == ecc_allowance_type.Transportation)
                localDataTemplate = await _emailRepository.GetTemplateDataAsync(TemplateNumber);//260

            var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
            _logger.LogInformation("Got the Template", serializedDataTemplate.Count);

            var templateobj = serializedDataTemplate?.FirstOrDefault();

            string? subject = (string)templateobj?.subjectsafehtml;
            subject = subject.Replace("#FANumber#", fundingNumber);
            string? emaildescription = templateobj?.safehtml;
            emaildescription = emaildescription?.Replace("[PrimaryContactName]", contactName);
            emaildescription = emaildescription?.Replace("{Amount}", fundingAmount.ToString());
            //emaildescription = emaildescription?.Replace("{AllowanceType}", allowanceType.ToString());
            if (allowanceType == ecc_allowance_type.Transportation)
            {
                emaildescription = emaildescription?.Replace("[AllowanceStartDate]", effectiveDate.Date.ToString());
                emaildescription = emaildescription?.Replace("{VINnumber}", VIN.ToString());

                emaildescription = emaildescription?.Replace("[retroactiveAmount]", contactName);
                emaildescription = emaildescription?.Replace("{paymentlineEnddate}", fundingAmount.ToString());
            }

            List<Guid> recipientsList = new List<Guid>();
            if (submittedBy != Guid.Empty)
            {
                _logger.LogInformation("Got the recipientsList submittedBy", submittedBy);
                recipientsList.Add(submittedBy);
                if (submittedBy != primaryContact)
                {
                    recipientsList.Add(primaryContact);
                }
                await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 235);


            }
            #endregion Create the email notifications 

        }

        if (appStatusCode == (int)ofm_application_StatusCode.Approved && statusReason == (int)ofm_allowance_StatusCode.DeniedIneligible)
        {
            ProcessData localDataTemplate = null;
            if (allowanceType == ecc_allowance_type.SupportNeedsProgramming)
                // Get template details to create emails.
                localDataTemplate = await _emailRepository.GetTemplateDataAsync(TemplateNumber);//275
            else if (allowanceType == ecc_allowance_type.IndigenousProgramming)
                localDataTemplate = await _emailRepository.GetTemplateDataAsync(TemplateNumber);//280
            else if (allowanceType == ecc_allowance_type.Transportation)
                localDataTemplate = await _emailRepository.GetTemplateDataAsync(TemplateNumber);//285

            var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
            _logger.LogInformation("Got the Template", serializedDataTemplate.Count);

            var templateobj = serializedDataTemplate?.FirstOrDefault();

            string? subject = (string)templateobj?.subjectsafehtml;
            string? emaildescription = templateobj?.safehtml;

            List<Guid> recipientsList = new List<Guid>();
            if (submittedBy != Guid.Empty)
            {
                _logger.LogInformation("Got the recipientsList submittedBy", submittedBy);
                recipientsList.Add(submittedBy);
                if (submittedBy != primaryContact)
                {
                    recipientsList.Add(primaryContact);
                }
                await _emailRepository.CreateAndUpdateEmail(subject, emaildescription, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 235);
            }

        }

        var updateNotificationFlagRequests = new List<HttpRequestMessage>() { };

        var approvalEmailSentflag = new JsonObject
            {
                { "ofm_approval_notification_sent", true }
            };

        updateNotificationFlagRequests.Add(new D365UpdateRequest(new EntityReference(ofm_allowance.EntitySetName, _allowanceId), approvalEmailSentflag));


        // Deactive reminders
        var updateNotificationResults = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updateNotificationFlagRequests, null);
        if (updateNotificationResults.Errors.Any())
        {
            var sendNotificationError = ProcessResult.Failure(ProcessId, updateNotificationResults.Errors, updateNotificationResults.TotalProcessed, updateNotificationResults.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to send notifications with an error: {error}", JsonValue.Create(sendNotificationError)!.ToString());

            return await Task.FromResult(sendNotificationError.SimpleProcessResult);
        }

        var result = ProcessResult.Success(ProcessId, updateNotificationFlagRequests.Count);

        var endTime = _timeProvider.GetTimestamp();

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string json = JsonSerializer.Serialize(result, serializeOptions);
        _logger.LogInformation(CustomLogEvent.Process, "Send Notification process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, json);



        return ProcessResult.Completed(ProcessId).SimpleProcessResult;


    }
}

    // Provider FA Approver







