using ECC.Core.DataContext;
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
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;

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
        private string RetrieveFundingAllocation
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $"""
                <fetch>
                  <entity name="ofm_funding_allocation">
                    <attribute name="statecode" />
                    <attribute name="ofm_funding_envelope_from" />
                    <attribute name="ofm_funding_envelope_to" />
                    <attribute name="ofm_amount" />
                    <filter>
                      <condition attribute="ofm_funding_envelop" operator="eq" value="{_processParams.FundingEnvelopeChange?.fundingEnvelopeChangeId.ToString()?.Replace("{", "").Replace("}", "")}" />
                      <condition attribute="statecode" operator="eq" value="0" />
                    </filter>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                         ofm_funding_allocations?fetchXml={WebUtility.UrlEncode(fetchXml)}
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

        public async Task<ProcessData> GetFundingAllocationDataAsync()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            _logger.LogDebug(CustomLogEvent.Process, "Calling RetrieveFundingAllocation");

            response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveFundingAllocation, formatted: true, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to retrieve Funding Allocation data to send notification with the following server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding Allocation data found with query {requestUri}", RetrieveFundingAllocation.CleanLog());
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

            if (fundingData == null || fundingData.Data == null)
            {
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            var fundingRecord = JsonSerializer.Deserialize<List<D365Funding>>(fundingData.Data.ToString());

            var fundingAllocationData = await GetFundingAllocationDataAsync();

            if (fundingAllocationData == null || fundingAllocationData.Data == null)
            {
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            decimal instuctionHumanResources = 0;
            decimal wages = 0;
            decimal benefits = 0;
            decimal employerHealthTax = 0;
            decimal professionalDevelopmentHours = 0;
            decimal professionalDevelopmentExpenses = 0;
            decimal programming = 0;
            decimal administrative = 0;
            decimal operational = 0;
            decimal facility = 0;

            var deserializedFundingAllocationData = JsonSerializer.Deserialize<List<D365FundingEnvelope>>(fundingAllocationData.Data.ToString());

            foreach (var fundingAllocation in deserializedFundingAllocationData)
            {
                if (fundingAllocation.ofm_funding_envelope_to != null) 
                {
                    switch ((int)fundingAllocation.ofm_funding_envelope_to)
                    {
                        //Adding 'TO' envelope
                        case 1:
                            wages += fundingAllocation.ofm_amount;
                            break;
                        case 2:
                            benefits += fundingAllocation.ofm_amount;
                            break;
                        case 3:
                            employerHealthTax += fundingAllocation.ofm_amount;
                            break;
                        case 4:
                            professionalDevelopmentHours += fundingAllocation.ofm_amount;
                            break;
                        case 5:
                            professionalDevelopmentExpenses += fundingAllocation.ofm_amount;
                            break;
                        case 6:
                            programming += fundingAllocation.ofm_amount;
                            break;
                        case 7:
                            administrative += fundingAllocation.ofm_amount;
                            break;
                        case 8:
                            operational += fundingAllocation.ofm_amount;
                            break;
                        case 9:
                            facility += fundingAllocation.ofm_amount;
                            break;
                    }
                }
                if (fundingAllocation.ofm_funding_envelope_from != null)
                {
                    switch ((int)fundingAllocation.ofm_funding_envelope_from)
                    {
                        //Subtracting 'FROM' envelope
                        case 1:
                            wages -= fundingAllocation.ofm_amount;
                            break;
                        case 2:
                            benefits -= fundingAllocation.ofm_amount;
                            break;
                        case 3:
                            employerHealthTax -= fundingAllocation.ofm_amount;
                            break;
                        case 4:
                            professionalDevelopmentHours -= fundingAllocation.ofm_amount;
                            break;
                        case 5:
                            professionalDevelopmentExpenses -= fundingAllocation.ofm_amount;
                            break;
                        case 6:
                            programming -= fundingAllocation.ofm_amount;
                            break;
                        case 7:
                            administrative -= fundingAllocation.ofm_amount;
                            break;
                        case 8:
                            operational -= fundingAllocation.ofm_amount;
                            break;
                        case 9:
                            facility -= fundingAllocation.ofm_amount;
                            break;
                    }
                }
            }

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
            
            string? baseFundingNumber = fundingRecord.FirstOrDefault()?.ofm_funding_number?.ToString();
            string? currentDate = DateTime.UtcNow.ToLocalPST().ToString("MM/dd/yyyy");

            string? organizationName = _processParams.FundingEnvelopeChange.organizationName;
            string? oneYearAnniversary = DateTime.UtcNow.AddYears(1).ToLocalPST().ToString("MM/dd/yyyy");
            string? startDate = fundingRecord.FirstOrDefault()?.ofm_start_date?.ToString();

            subject = subject.Replace("[Funding Agreement #]", currentfundingNumber);
            emailBody = emailBody?.Replace("[Funding Agreement #] ", currentfundingNumber);
            emailBody = emailBody?.Replace("[Organization]", organizationName);
            emailBody = emailBody?.Replace("[BaseFundingNumber]", baseFundingNumber);
            emailBody = emailBody?.Replace("[Approval Date]", currentDate);
            emailBody = emailBody?.Replace("[Approval Date + 1 year (the anniversary date of the approval)]", oneYearAnniversary);
            emailBody = emailBody?.Replace("[Start Date]", startDate);


            instuctionHumanResources = wages + benefits + employerHealthTax + professionalDevelopmentHours + professionalDevelopmentExpenses;

            //Envelopment Change only impacts 'Annual Base Funding' and 'Annual Total Funding for Core Services' column

            decimal wages_base = wages + fundingRecord.FirstOrDefault().ofm_envelope_hr_wages_paidtimeoff;
            decimal wages_pf = fundingRecord.FirstOrDefault().ofm_envelope_hr_wages_paidtimeoff_pf;
            decimal wages_proj = wages + fundingRecord.FirstOrDefault().ofm_envelope_hr_wages_paidtimeoff_proj;

            decimal benefits_base = benefits + fundingRecord.FirstOrDefault().ofm_envelope_hr_benefits;
            decimal benefits_pf = fundingRecord.FirstOrDefault().ofm_envelope_hr_benefits_pf;
            decimal benefits_proj = benefits + fundingRecord.FirstOrDefault().ofm_envelope_hr_benefits_proj;

            decimal employerHealthTax_base = employerHealthTax + fundingRecord.FirstOrDefault().ofm_envelope_hr_employerhealthtax;
            decimal employerHealthTax_pf = fundingRecord.FirstOrDefault().ofm_envelope_hr_employerhealthtax_pf;
            decimal employerHealthTax_proj = employerHealthTax + fundingRecord.FirstOrDefault().ofm_envelope_hr_employerhealthtax_proj;

            decimal professionalDevelopmentHours_base = professionalDevelopmentHours + fundingRecord.FirstOrDefault().ofm_envelope_hr_prodevhours;
            decimal professionalDevelopmentHours_pf = fundingRecord.FirstOrDefault().ofm_envelope_hr_prodevhours_pf;
            decimal professionalDevelopmentHours_proj = professionalDevelopmentHours + fundingRecord.FirstOrDefault().ofm_envelope_hr_prodevhours_proj;

            decimal professionalDevelopmentExpenses_base = professionalDevelopmentExpenses + fundingRecord.FirstOrDefault().ofm_envelope_hr_prodevexpenses;
            decimal professionalDevelopmentExpenses_pf = fundingRecord.FirstOrDefault().ofm_envelope_hr_prodevexpenses_pf;
            decimal professionalDevelopmentExpenses_proj = professionalDevelopmentExpenses + fundingRecord.FirstOrDefault().ofm_envelope_hr_prodevexpenses_proj;

            decimal programming_base = programming + fundingRecord.FirstOrDefault().ofm_envelope_programming;
            decimal programming_pf = fundingRecord.FirstOrDefault().ofm_envelope_programming_pf;
            decimal programming_proj = programming + fundingRecord.FirstOrDefault().ofm_envelope_programming_proj;

            decimal administrative_base = administrative + fundingRecord.FirstOrDefault().ofm_envelope_administrative;
            decimal administrative_pf = fundingRecord.FirstOrDefault().ofm_envelope_administrative_pf;
            decimal administrative_proj = administrative + fundingRecord.FirstOrDefault().ofm_envelope_administrative_proj;

            decimal operational_base = operational + fundingRecord.FirstOrDefault().ofm_envelope_operational;
            decimal operational_pf = fundingRecord.FirstOrDefault().ofm_envelope_operational_pf;
            decimal operational_proj = operational + fundingRecord.FirstOrDefault().ofm_envelope_operational_proj;

            decimal facility_base = facility + fundingRecord.FirstOrDefault().ofm_envelope_facility;
            decimal facility_pf = fundingRecord.FirstOrDefault().ofm_envelope_facility_pf;
            decimal facility_proj = facility + fundingRecord.FirstOrDefault().ofm_envelope_facility_proj;

            decimal instuctionHumanResources_base = instuctionHumanResources + fundingRecord.FirstOrDefault().ofm_envelope_hr_total;
            decimal instuctionHumanResources_pf = fundingRecord.FirstOrDefault().ofm_envelope_hr_total_pf;
            decimal instuctionHumanResources_proj = instuctionHumanResources + fundingRecord.FirstOrDefault().ofm_envelope_hr_total_proj;

            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff}", wages_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff_pf", wages_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_wages_paidtimeoff_proj", wages_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits}", benefits_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits_pf", benefits_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_benefits_proj", benefits_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax}", employerHealthTax_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax_pf", employerHealthTax_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_employerhealthtax_proj", employerHealthTax_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours}", professionalDevelopmentHours_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours_pf", professionalDevelopmentHours_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevhours_proj", professionalDevelopmentHours_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses}", professionalDevelopmentExpenses_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses_pf", professionalDevelopmentExpenses_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_prodevexpenses_proj", professionalDevelopmentExpenses_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_programming}", programming_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_programming_pf", programming_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_programming_proj", programming_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_administrative}", administrative_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_administrative_pf", administrative_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_administrative_proj", administrative_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_operational}", operational_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_operational_pf", operational_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_operational_proj", operational_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_facility}", facility_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_facility_pf", facility_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_facility_proj", facility_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_hr_total}", instuctionHumanResources_base.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_total_pf", instuctionHumanResources_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_hr_total_proj", instuctionHumanResources_proj.ToString("N2", CultureInfo.InvariantCulture));

            emailBody = emailBody?.Replace("ofm_envelope_grand_total}", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_grand_total_pf", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total_pf.ToString("N2", CultureInfo.InvariantCulture));
            emailBody = emailBody?.Replace("ofm_envelope_grand_total_proj", fundingRecord.FirstOrDefault()?.ofm_envelope_grand_total_proj.ToString("N2", CultureInfo.InvariantCulture));

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
