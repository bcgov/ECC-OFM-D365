using ECC.Core.DataContext;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    public class P265SendFundingEnvelopeChangeApprovalNotification(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider, IEmailRepository emailRepository) : ID365ProcessProvider
    {
        private readonly IEmailRepository _emailRepository = emailRepository;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
        private ProcessParameter? _processParams;

        public short ProcessId => Setup.Process.Emails.SendFundingEnvelopeChangeApprovalNotificationsId;
        public string ProcessName => Setup.Process.Emails.SendFundingEnvelopeChangeApprovalNotificationsIdName;

        //To retrieve Expense application.
        private string RetrieveFunding
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
               var fetchXml = $"""
                <fetch>
                  <entity name="ofm_funding">
                    <attribute name="ofm_fundingid" />
                    <attribute name="ofm_envelope_administrative" />
                    <attribute name="ofm_envelope_administrative_pf" />
                    <attribute name="ofm_envelope_administrative_proj" />
                    <attribute name="ofm_envelope_facility" />
                    <attribute name="ofm_envelope_facility_pf" />
                    <attribute name="ofm_envelope_facility_proj" />
                    <attribute name="ofm_envelope_grand_total" />
                    <attribute name="ofm_envelope_grand_total_pf" />
                    <attribute name="ofm_envelope_grand_total_proj" />
                    <attribute name="ofm_envelope_hr_benefits" />
                    <attribute name="ofm_envelope_hr_benefits_pf" />
                    <attribute name="ofm_envelope_hr_benefits_proj" />
                    <attribute name="ofm_envelope_hr_employerhealthtax" />
                    <attribute name="ofm_envelope_hr_employerhealthtax_pf" />
                    <attribute name="ofm_envelope_hr_employerhealthtax_proj" />
                    <attribute name="ofm_envelope_hr_prodevexpenses" />
                    <attribute name="ofm_envelope_hr_prodevexpenses_pf" />
                    <attribute name="ofm_envelope_hr_prodevexpenses_proj" />
                    <attribute name="ofm_envelope_hr_prodevhours" />
                    <attribute name="ofm_envelope_hr_prodevhours_pf" />
                    <attribute name="ofm_envelope_hr_prodevhours_proj" />
                    <attribute name="ofm_envelope_hr_total" />
                    <attribute name="ofm_envelope_hr_total_pf" />
                    <attribute name="ofm_envelope_hr_total_proj" />
                    <attribute name="ofm_envelope_hr_wages_paidtimeoff" />
                    <attribute name="ofm_envelope_hr_wages_paidtimeoff_pf" />
                    <attribute name="ofm_envelope_hr_wages_paidtimeoff_proj" />
                    <attribute name="ofm_envelope_operational" />
                    <attribute name="ofm_envelope_operational_pf" />
                    <attribute name="ofm_envelope_operational_proj" />
                    <attribute name="ofm_envelope_programming" />
                    <attribute name="ofm_envelope_programming_pf" />
                    <attribute name="ofm_envelope_programming_proj" />
                    <attribute name="ofm_funding_envelope" />
                    <attribute name="ofm_funding_number" />
                    <attribute name="ofm_start_date" />
                    <attribute name="ofm_envelope_hr_total_reallo" />
                    <attribute name="ofm_envelope_hr_total_reallo_pf" />
                    <attribute name="ofm_envelope_hr_total_reallo_proj" />
                    <attribute name="ofm_envelope_hr_wages_paidtimeoff_reallo" />
                    <attribute name="ofm_envelope_hr_wages_paidtimeoff_reallo_pf" />
                    <attribute name="ofm_envelope_hr_wages_paidtimeoff_reallo_proj" />
                    <attribute name="ofm_envelope_hr_benefits_reallo" />
                    <attribute name="ofm_envelope_hr_benefits_reallo_pf" />
                    <attribute name="ofm_envelope_hr_benefits_reallo_proj" />
                    <attribute name="ofm_envelope_hr_employerhealthtax_reallo" />
                    <attribute name="ofm_envelope_hr_employerhealthtax_reallo_pf" />
                    <attribute name="ofm_envelope_hr_employerhealthtax_reallo_proj" />
                    <attribute name="ofm_envelope_hr_prodevhours_reallo" />
                    <attribute name="ofm_envelope_hr_prodevhours_reallo_pf" />
                    <attribute name="ofm_envelope_hr_prodevhours_reallo_proj" />
                    <attribute name="ofm_envelope_hr_prodevexpenses_reallo" />
                    <attribute name="ofm_envelope_hr_prodevexpenses_reallo_pf" />
                    <attribute name="ofm_envelope_hr_prodevexpenses_reallo_proj" />
                    <attribute name="ofm_envelope_programming_reallo" />
                    <attribute name="ofm_envelope_programming_reallo_pf" />
                    <attribute name="ofm_envelope_programming_reallo_proj" />
                    <attribute name="ofm_envelope_administrative_reallo" />
                    <attribute name="ofm_envelope_administrative_reallo_pf" />
                    <attribute name="ofm_envelope_administrative_reallo_proj" />
                    <attribute name="ofm_envelope_operational_reallo" />
                    <attribute name="ofm_envelope_operational_reallo_pf" />
                    <attribute name="ofm_envelope_operational_reallo_proj" />
                    <attribute name="ofm_envelope_facility_reallo" />
                    <attribute name="ofm_envelope_facility_reallo_pf" />
                    <attribute name="ofm_envelope_facility_reallo_proj" />
                    <attribute name="ofm_envelope_grand_total_reallo" />
                    <attribute name="ofm_envelope_grand_total_reallo_pf" />
                    <attribute name="ofm_envelope_grand_total_reallo_proj" />
                    <attribute name="ofm_monthly_province_base_funding_y1" />
                    <attribute name="ofm_projected_monthly_parent_fees_y1" />
                    <attribute name="ofm_projected_total_monthly_base_funding_y1" />
                    <filter>
                      <condition attribute="ofm_fundingid" operator="eq" value="{_processParams.FundingEnvelopeChange?.fundingId.ToString()?.Replace("{", "").Replace("}", "")}" />
                    </filter>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                         ofm_fundings?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;
                return requestUri.CleanCRLF();
            }
        }

        public string originalFunding
        {
            get
            {
                var originalFundingFetchXML = $"""
                                <fetch>
                                  <entity name="ofm_funding">
                                    <attribute name="ofm_fundingid" />
                                    <attribute name="ofm_start_date" />
                                    <attribute name="ofm_end_date" />
                                    <attribute name="ofm_version_number" />
                                    <attribute name="statecode" />
                                    <attribute name="statuscode" />
                                    <attribute name="ofm_funding_number" />
                                    <filter>
                                      <condition attribute="ofm_application" operator="eq" value="{_processParams.Application.applicationId.ToString()?.Replace("{", "").Replace("}", "")}" />
                                      <condition attribute="ofm_version_number" operator="eq" value="0" />
                                    </filter>
                                  </entity>
                                </fetch>
                                """;

                var originalFundingUri = $"""
                         ofm_fundings?fetchXml={WebUtility.UrlEncode(originalFundingFetchXML)}
                         """;

                return originalFundingUri;
            }
        }

        public string fundingEnvelope
        {
            get
            {
                var fundingEnvelopeFetchXML = $"""
                                <fetch>
                                  <entity name="ofm_funding_envelope_change">
                                    <attribute name="ofm_year_of_agreement" />
                                    <attribute name="ofm_pdf_funding_allocation_amount_text" />
                                    <filter>
                                      <condition attribute="ofm_funding_envelope_changeid" operator="eq" value="{_processParams.FundingEnvelopeChange.fundingEnvelopeChangeId.ToString()?.Replace("{", "").Replace("}", "")}" />
                                    </filter>
                                  </entity>
                                </fetch>
                                """;

                var fundingEnvelopeFundingUri = $"""
                         ofm_funding_envelope_changes?fetchXml={WebUtility.UrlEncode(fundingEnvelopeFetchXML)}
                         """;

                return fundingEnvelopeFundingUri;
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

            _logger.LogDebug(CustomLogEvent.Process, "Calling RetrieveFunding");

            response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveFunding, formatted: true, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to retrieve Funding data to send notification with the following server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding data found with query {requestUri}", RetrieveFunding.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<ProcessData> GetOriginalFundingRecord()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            _logger.LogDebug(CustomLogEvent.Process, "Calling GetOriginalFundingRecord");

            response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, originalFunding, formatted: true, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to retrieve Funding data with the following server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding data found with query {requestUri}", originalFunding.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<ProcessData> GetFundingEnvelope()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            _logger.LogDebug(CustomLogEvent.Process, "Calling GetFundingEnvelope");

            response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, fundingEnvelope, formatted: true, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to retrieve Funding data with the following server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding Envelope data found with query {requestUri}", fundingEnvelope.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            _processParams = processParams;

            var fundingData = await GetDataAsync();
            var originalFundingData = await GetOriginalFundingRecord();
            var fundingEnvelope = await GetFundingEnvelope();

            if (fundingData == null || fundingData.Data == null || originalFundingData == null || originalFundingData.Data == null
                || fundingEnvelope == null || fundingEnvelope.Data == null)
            {
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            var fundingRecord = JsonSerializer.Deserialize<List<D365Funding>>(fundingData.Data.ToString());
            var originalFundingRecord = JsonSerializer.Deserialize<List<D365Funding>>(originalFundingData.Data.ToString());
            var fundingEnvelopeRecord = JsonSerializer.Deserialize<List<D365FundingEnvelope>>(fundingEnvelope.Data.ToString());

            Guid primaryContact = _processParams.FundingEnvelopeChange.primaryContactId != null ? (Guid)_processParams.FundingEnvelopeChange.primaryContactId : Guid.Empty;
            Guid requestorContact = _processParams.FundingEnvelopeChange.requestorContactId != null ? (Guid)_processParams.FundingEnvelopeChange.requestorContactId : Guid.Empty;

            IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();

            var _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.Information)
                                                                         .Select(s => s.ofm_communication_typeid).FirstOrDefault();

            List<Guid> recipientsList = new List<Guid>();

            #region CreateEmailNotification


            // Get template details to create emails.                
            var fundingDataTemplate = await _emailRepository.GetTemplateDataAsync(Int32.Parse(_processParams.Notification.TemplateNumber));

            var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(fundingDataTemplate.Data.ToString());
            _logger.LogInformation("Retrieved Template", serializedDataTemplate.Count);

            var fundingDataContact = await GetContactDataAsync(primaryContact.ToString());
            var deserializedData = JsonSerializer.Deserialize<List<D365Contact>>(fundingDataContact.Data.ToString());
            var contactobj = deserializedData?.FirstOrDefault();
            var firstName = contactobj?.ofm_first_name;
            var lastName = contactobj?.ofm_last_name;

            var templateobj = serializedDataTemplate?.FirstOrDefault();
            string? subject = _emailRepository.StripHTML(templateobj?.subjectsafehtml);
            string? emailBody = templateobj?.safehtml;

            string? previousfundingNumber = fundingRecord.FirstOrDefault().ofm_funding_number.ToString();
            string currentfundingNumber = string.Empty;
           
            //Append Funding Modification Number (Can be 1 or 2 characters) to Funding
            if (_processParams.FundingEnvelopeChange.fundingVersionNumber != null) {
                currentfundingNumber = previousfundingNumber.Remove(previousfundingNumber.Length -
                    _processParams.FundingEnvelopeChange.fundingVersionNumber.Length) + _processParams.FundingEnvelopeChange.fundingVersionNumber;
            }
            else
            {
                currentfundingNumber = string.Empty;
            }
            
            string? baseFundingNumber = originalFundingRecord.FirstOrDefault()?.ofm_funding_number?.ToString();
            string? originalFundingStartDateString = originalFundingRecord.FirstOrDefault()?.ofm_start_date.ToString();
            DateTime originalFundingStartDate = DateTime.UtcNow;

            if (DateTime.TryParseExact(originalFundingStartDateString, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                originalFundingStartDate = result;
            }

            string? organizationName = _processParams.FundingEnvelopeChange.organizationName;
          
            DateTime startDate = originalFundingStartDate.AddYears(fundingEnvelopeRecord.FirstOrDefault().ofm_year_of_agreement-1);
            DateTime endDate = startDate.AddYears(1).AddDays(-1);

            subject = subject.Replace("[Funding Agreement #]", currentfundingNumber);
            emailBody = emailBody?.Replace("[Funding Agreement #]", currentfundingNumber);
            emailBody = emailBody?.Replace("[Organization]", organizationName);
            emailBody = emailBody?.Replace("[PreviousFundingNumber]", previousfundingNumber);
            emailBody = emailBody?.Replace("[BaseFundingNumber]", baseFundingNumber);
            emailBody = emailBody?.Replace("[OriginalFundingStartDate]", originalFundingStartDate.ToString("MMMM dd, yyyy"));
            emailBody = emailBody?.Replace("[StartDate]", startDate.ToString("MMMM dd, yyyy"));
            emailBody = emailBody?.Replace("[EndDate]", endDate.ToString("MMMM dd, yyyy"));
            emailBody = emailBody?.Replace("[FundingAllocation]", fundingEnvelopeRecord.FirstOrDefault().ofm_pdf_funding_allocation_amount_text);

            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_wages_paidtimeoff.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_wages_paidtimeoff_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_wages_paidtimeoff_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_benefits.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_benefits_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_benefits_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_employerhealthtax.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_employerhealthtax_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_employerhealthtax_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevhours.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevhours_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevhours_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevexpenses.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevexpenses_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevexpenses_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_programming}", fundingRecord.FirstOrDefault()?.ofm_envelope_programming.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_programming_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_programming_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_programming_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_programming_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_administrative}", fundingRecord.FirstOrDefault()?.ofm_envelope_administrative.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_administrative_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_administrative_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_administrative_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_administrative_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_operational}", fundingRecord.FirstOrDefault()?.ofm_envelope_operational.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_operational_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_operational_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_operational_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_operational_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_facility}", fundingRecord.FirstOrDefault()?.ofm_envelope_facility.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_facility_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_facility_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_facility_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_facility_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_total}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_total.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_total_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_total_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_total_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_total_proj.ToString("N2", CultureInfo.InvariantCulture));

            //Grand Total
            emailBody = emailBody?.Replace("ofm_envelope_grand_total}", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_grand_total_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_grand_total_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total_proj.ToString("N2", CultureInfo.InvariantCulture));

            //Monthly Amount
            emailBody = emailBody?.Replace("ofm_monthly_province_base_funding_y1", fundingRecord.FirstOrDefault()?.ofm_monthly_province_base_funding_y1.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_projected_monthly_parent_fees_y1", fundingRecord.FirstOrDefault()?.ofm_projected_monthly_parent_fees_y1.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_projected_total_monthly_base_funding_y1", fundingRecord.FirstOrDefault()?.ofm_projected_total_monthly_base_funding_y1.ToString("N2", CultureInfo.InvariantCulture));

            //Funding Envelope
            emailBody = emailBody?.Replace("ofm_envelope_hr_total_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_total_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_total_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_total_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_total_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_total_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_wages_paidtimeoff_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_wages_paidtimeoff_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_wages_paidtimeoff_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_benefits_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_benefits_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_benefits_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_employerhealthtax_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_employerhealthtax_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_employerhealthtax_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevhours_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevhours_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevhours_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevexpenses_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevexpenses_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_hr_prodevexpenses_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_programming_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_programming_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_programming_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_programming_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_programming_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_programming_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_administrative_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_administrative_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_administrative_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_administrative_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_administrative_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_administrative_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_operational_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_operational_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_operational_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_operational_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_operational_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_operational_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_facility_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_facility_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_facility_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_facility_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_facility_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_facility_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_grand_total_reallo}", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total_reallo.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_grand_total_reallo_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total_reallo_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_grand_total_reallo_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total_reallo_proj.ToString("N2", CultureInfo.InvariantCulture));
            
            string regardingData = string.Empty;

            if (primaryContact != Guid.Empty)
            {
                recipientsList.Add(primaryContact);
                regardingData = string.Format("{0}#ofm_funding_envelope_change", _processParams.FundingEnvelopeChange.fundingEnvelopeChangeId);

                await _emailRepository.CreateAndUpdateEmail(subject, emailBody, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 265, regardingData);

            }

            if (requestorContact != Guid.Empty && requestorContact != primaryContact)
            {
                recipientsList.Clear();
                recipientsList.Add(requestorContact);

                regardingData = string.Format("{0}#ofm_funding_envelope_change", _processParams.FundingEnvelopeChange.fundingEnvelopeChangeId);

                fundingDataContact = await GetContactDataAsync(requestorContact.ToString());
                deserializedData = JsonSerializer.Deserialize<List<D365Contact>>(fundingDataContact.Data.ToString());
                contactobj = deserializedData?.FirstOrDefault();
                firstName = contactobj?.ofm_first_name;
                lastName = contactobj?.ofm_last_name;

                await _emailRepository.CreateAndUpdateEmail(subject, emailBody, recipientsList, _processParams.Notification.SenderId, _informationCommunicationType, appUserService, d365WebApiService, 235, regardingData);
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
