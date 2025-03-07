using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.Json;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using OFM.Infrastructure.WebAPI.Messages;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments
{
    public class P520GenerateAllowancePaymentProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, IFundingRepository fundingRepository, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
    {
        private readonly BCCASApi _BCCASApi = bccasApiSettings.Value.BCCASApi;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365WebApiService = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly IFundingRepository _fundingRepository = fundingRepository;
        private readonly TimeProvider _timeProvider = timeProvider;
        private ProcessParameter? _processParams;
        private Guid _baseApplicationId = Guid.Empty;
        private List<D365PaymentLine>? _allPayments;

        public Int16 ProcessId => Setup.Process.Payments.GeneratePaymentLinesForSupplementaryAllowanceId;
        public string ProcessName => Setup.Process.Payments.GeneratePaymentLinesForSupplementaryAllowanceName;

        #region Data Queries

        public string AllPaymentsByApplicationIdRequestUri
        {
            get
            {
                // For reference only
                var fetchXml = $$"""
                    <fetch>
                      <entity name="ofm_payment">
                        <attribute name="ofm_paymentid" />
                        <attribute name="ofm_name" />
                        <attribute name="createdon" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_funding" />
                        <attribute name="ofm_payment_type" />
                        <attribute name="ofm_effective_date" />
                        <attribute name="ofm_amount" />
                        <attribute name="ofm_application" />
                        <attribute name="ofm_invoice_line_number" />
                        <attribute name="ofm_regardingid" />
                        <order attribute="ofm_invoice_line_number" descending="true" />
                        <filter type="and">
                          <condition attribute="ofm_application" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_payments?$select=ofm_paymentid,ofm_name,_ofm_regardingid_value,createdon,statuscode,_ofm_funding_value,ofm_payment_type,ofm_effective_date,ofm_amount,_ofm_application_value,ofm_invoice_line_number&$filter=(_ofm_application_value eq {_baseApplicationId})&$orderby=ofm_invoice_line_number desc
                         """;

                return requestUri;
            }
        }

        public string AllFiscalYearsRequestUri
        {
            get
            {
                // For reference only
                var fetchXml = $$"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_fiscal_year">
                        <attribute name="ofm_caption" />
                        <attribute name="createdon" />
                        <attribute name="ofm_agreement_number_seed" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_fiscal_year_number" />
                        <attribute name="owningbusinessunit" />
                        <attribute name="ofm_start_date" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_fiscal_yearid" />
                        <order attribute="ofm_caption" descending="false" />
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_fiscal_years?$select=ofm_caption,createdon,ofm_agreement_number_seed,ofm_end_date,ofm_fiscal_year_number,_owningbusinessunit_value,ofm_start_date,statuscode,ofm_fiscal_yearid&$orderby=ofm_caption asc
                         """;

                return requestUri;
            }
        }

        public string BusinessClosuresRequestUri
        {
            get
            {
                var fetchXml = $$"""
                    <fetch>
                      <entity name="ofm_stat_holiday">
                        <attribute name="ofm_date_observed" />
                        <attribute name="ofm_holiday_type" />
                        <attribute name="ofm_stat_holidayid" />
                        <filter>
                          <condition attribute="ofm_holiday_type" operator="eq" value="2" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_stat_holidaies?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }
      
        public string ApplicationRequestUri
        {
            get
            {
                // For reference only
                var fetchXml = $$"""
                    <fetch distinct="true">
                      <entity name="ofm_application">
                        <attribute name="ofm_application" />
                        <attribute name="ofm_applicationid" />
                        <attribute name="ofm_funding_number_base" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <link-entity name="account" from="accountid" to="ofm_facility" link-type="inner" alias="Facility">
                          <attribute name="accountid" />
                          <attribute name="accountnumber" />
                          <attribute name="name" />
                        </link-entity>
                        <link-entity name="account" from="accountid" to="ofm_organization" link-type="inner" alias="Organization">
                          <attribute name="accountid" />
                          <attribute name="accountnumber" />
                          <attribute name="name" />
                        </link-entity>
                        <link-entity name="ofm_allowance" from="ofm_application" to="ofm_applicationid" alias="Allowance">
                          <attribute name="ofm_allowance_number" />
                          <attribute name="ofm_allowance_type" />
                          <attribute name="ofm_allowanceid" />
                          <attribute name="ofm_funding_amount" />
                          <attribute name="ofm_monthly_amount" />
                          <attribute name="ofm_start_date" />
                          <attribute name="ofm_end_date" />
                          <attribute name="ofm_renewal_term" />
                          <attribute name="ofm_retroactive_amount" />
                          <attribute name="ofm_retroactive_date" />
                          <attribute name="statecode" />
                          <attribute name="statuscode" />
                          <filter>
                            <condition attribute="ofm_allowanceid" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                          </filter>
                        </link-entity>
                        <link-entity name="ofm_funding" from="ofm_application" to="ofm_applicationid" alias="Funding">
                          <attribute name="ofm_fundingid" />
                          <attribute name="ofm_end_date" />
                          <attribute name="ofm_start_date" />
                          <attribute name="ofm_version_number" />
                          <attribute name="statecode" />
                          <attribute name="statuscode" />
                          <filter>
                            <condition attribute="ofm_version_number" operator="eq" value="0" />
                          </filter>
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                        ofm_applications?$select=ofm_application,ofm_applicationid,ofm_summary_ownership,ofm_application_type,ofm_funding_number_base,_ofm_contact_value,_ofm_expense_authority_value,statecode,statuscode&$expand=ofm_facility($select=accountid,accountnumber,name),ofm_organization($select=accountid,accountnumber,name),ofm_application_allowance($select=ofm_allowance_number,ofm_allowance_type,ofm_allowanceid,ofm_end_date,ofm_funding_amount,ofm_renewal_term,ofm_retroactive_amount,ofm_retroactive_date,ofm_submittedon,statecode,statuscode,ofm_monthly_amount,_ofm_application_value,ofm_start_date;$filter=(ofm_allowanceid eq '{_processParams!.SupplementaryApplication!.allowanceId}')),ofm_application_funding($select=ofm_end_date,ofm_fundingid,ofm_start_date,ofm_version_number,statecode,statuscode;$filter=(ofm_version_number eq 0))&$filter=(ofm_facility/accountid ne null) and (ofm_organization/accountid ne null) and (ofm_application_allowance/any(o1:(o1/ofm_allowanceid eq '{_processParams!.SupplementaryApplication!.allowanceId}'))) and (ofm_application_funding/any(o2:(o2/ofm_version_number eq 0)))
                        """;

                return requestUri;
            }
        }

        #endregion

        #region Data

        public async Task<ProcessData> GetBusinessClosuresDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetBusinessClosuresDataAsync));

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, BusinessClosuresRequestUri, false, 0, true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query Funding record information with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding records found with query {requestUri}", BusinessClosuresRequestUri.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<ProcessData> GetAllFiscalYearsDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetAllFiscalYearsDataAsync));

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, AllFiscalYearsRequestUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query fiscal year record information with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Fiscal Year records found with query {requestUri}", AllFiscalYearsRequestUri.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<ProcessData> GetDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetDataAsync));

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, ApplicationRequestUri, false, 0, true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query payment records with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No payment records found with query {requestUri}", ApplicationRequestUri.CleanLog());
                }

                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<ProcessData> GetAllPaymentsByApplicationIdDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetAllPaymentsByApplicationIdDataAsync));

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, AllPaymentsByApplicationIdRequestUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query all payment records by applicationId {applicationId} with the server error {responseBody}", _baseApplicationId, responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogWarning(CustomLogEvent.Process, "No payment records found with query {requestUri}", AllPaymentsByApplicationIdRequestUri.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        #endregion

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            #region Validation & Setup

            ArgumentNullException.ThrowIfNull(processParams);
            ArgumentNullException.ThrowIfNull(processParams.SupplementaryApplication);
            ArgumentNullException.ThrowIfNull(processParams.SupplementaryApplication.allowanceId);

            _processParams = processParams;

            var baseApplicationData = await GetDataAsync();
            var deserializedApplicationData = JsonSerializer.Deserialize<List<Application>>(baseApplicationData.Data);
            if (deserializedApplicationData is null || !deserializedApplicationData.Any())
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve Application record with allowanceId {allowanceId}", processParams.SupplementaryApplication!.allowanceId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            SupplementaryApplication? approvedSA = deserializedApplicationData.First()?.ofm_application_allowance?.FirstOrDefault();
            if (approvedSA is null)
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve the Supplementary Application with the Id: {allowanceId}", processParams!.SupplementaryApplication!.allowanceId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }
            _baseApplicationId = deserializedApplicationData.First().Id;
            Funding? funding = deserializedApplicationData.First()?.ofm_application_funding?.FirstOrDefault();
            if (funding is null)
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve Funding record with Id {FundingId}", processParams.Funding!.FundingId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }
            var fundingEndDate = funding.ofm_end_date;
            var fundingStartDate = funding.ofm_start_date;
            DateTime intermediateDate;
            DateTime firstAnniversary;
            DateTime secondAnniversary;
           
            var threeYear = new DateTime();
            threeYear = fundingStartDate.Value.AddYears(3).AddDays(-1);
            if (fundingEndDate < threeYear)
            {
                intermediateDate = fundingEndDate.Value.AddYears(-1);
                firstAnniversary = intermediateDate;
                secondAnniversary = fundingEndDate.Value;
               
            }
            else
            {
                intermediateDate = fundingEndDate.Value.AddYears(-2);
                firstAnniversary = intermediateDate;
                intermediateDate = fundingEndDate.Value.AddYears(-1);
                secondAnniversary = intermediateDate;
                
            }

            ProcessData allPaymentsData = await GetAllPaymentsByApplicationIdDataAsync();
            _allPayments = JsonSerializer.Deserialize<List<D365PaymentLine>>(allPaymentsData.Data.ToString());
            if (_allPayments is not null && _allPayments.Count > 0)
            {
                List<D365PaymentLine> approvedSAPayments = _allPayments.Where(payment => payment._ofm_regardingid_value != null &&
                                                                              payment._ofm_regardingid_value == approvedSA.ofm_allowanceid.ToString()).ToList();

                if (approvedSAPayments.Count > 0)
                {
                    _logger.LogWarning(CustomLogEvent.Process, "Payments have been previously generated for the Supplementary Application with the Id: {allowanceId}", processParams!.SupplementaryApplication!.allowanceId);
                    return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                }
            }

            var fiscalYearsData = await GetAllFiscalYearsDataAsync();
            List<D365FiscalYear> fiscalYears = [.. JsonSerializer.Deserialize<List<D365FiscalYear>>(fiscalYearsData.Data)];

            var businessClosuresData = await GetBusinessClosuresDataAsync();
            
            var closures = JsonSerializer.Deserialize<List<ofm_stat_holiday>>(businessClosuresData.Data.ToString());
            List<DateTime> holidaysList = closures!.Select(closure => (DateTime)closure.ofm_date_observed).ToList();
            #endregion

            await ProcessSupportNeedsOrIndigenousPayments(deserializedApplicationData.First(), approvedSA, processParams, fiscalYears, holidaysList, firstAnniversary, secondAnniversary, fundingEndDate);
            await ProcessTransportationPayments(deserializedApplicationData.First(), approvedSA, processParams, fiscalYears, holidaysList,firstAnniversary, secondAnniversary, fundingEndDate);

            _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the supplementary application {allowanceId}", processParams.SupplementaryApplication!.allowanceId);

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> ProcessSupportNeedsOrIndigenousPayments(Application baseApplication, SupplementaryApplication approvedSA, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList, DateTime firstAnniversaryDate, DateTime secondAnniversaryDate, DateTime? fundingEndDate)
        {
            if (approvedSA.ofm_allowance_type == ecc_allowance_type.SupportNeedsProgramming || approvedSA.ofm_allowance_type == ecc_allowance_type.IndigenousProgramming)
            {
                ecc_payment_type paymentType = (approvedSA.ofm_allowance_type.Value == ecc_allowance_type.SupportNeedsProgramming) ? ecc_payment_type.SupportNeedsFunding : ecc_payment_type.IndigenousProgramming;
                await CreateSinglePayment(approvedSA, approvedSA.ofm_start_date!.Value, approvedSA.ofm_funding_amount, false, paymentType, baseApplication!, processParams, fiscalYears, holidaysList, firstAnniversaryDate, secondAnniversaryDate, fundingEndDate);

                _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the {allowancetype} application with Id {allowanceId}", approvedSA.ofm_allowance_type, processParams.SupplementaryApplication!.allowanceId);
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> ProcessTransportationPayments(Application baseApplication, SupplementaryApplication approvedSA, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList, DateTime firstAnniversaryDate, DateTime secondAnniversaryDate, DateTime? fundingEndDate)
        {
            if (approvedSA.ofm_allowance_type == ecc_allowance_type.Transportation)
            {
                if ((CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value,firstAnniversaryDate) && approvedSA.ofm_renewal_term == 1) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, secondAnniversaryDate) && approvedSA.ofm_renewal_term == 2) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, fundingEndDate.Value) && approvedSA.ofm_renewal_term == 3))
            
                {
                    int retroActiveMonthsCount = approvedSA.ofm_retroactive_date!.HasValue ? (approvedSA.ofm_start_date.Value.Year - approvedSA.ofm_retroactive_date!.Value.Year) * 12 + approvedSA.ofm_start_date.Value.Month - approvedSA.ofm_retroactive_date.Value.Month : 0;
                    decimal retroActiveAmount = retroActiveMonthsCount > 0 ? approvedSA.ofm_monthly_amount!.Value * retroActiveMonthsCount : 0;
                    var endTermLumpSumPayment = approvedSA.ofm_monthly_amount!.Value + retroActiveAmount;
                    await CreateSinglePayment(approvedSA, approvedSA.ofm_start_date!.Value, endTermLumpSumPayment, false, ecc_payment_type.Transportation, baseApplication!, processParams, fiscalYears, holidaysList, firstAnniversaryDate, secondAnniversaryDate, fundingEndDate);

                    _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the {allowancetype} application with Id {allowanceId}", approvedSA.ofm_allowance_type, processParams.SupplementaryApplication!.allowanceId);
                }
                else
                {
                    // Process future payments
                    await CreatePaymentsInBatch(baseApplication!, approvedSA!, approvedSA.ofm_start_date.Value, approvedSA.ofm_end_date.Value, approvedSA.ofm_monthly_amount!.Value, false, ecc_payment_type.Transportation, processParams, fiscalYears, holidaysList, firstAnniversaryDate, secondAnniversaryDate, fundingEndDate);
                    // Process retroactive payment
                    int retroActiveMonthsCount = approvedSA.ofm_retroactive_date!.HasValue ? (approvedSA.ofm_start_date.Value.Year - approvedSA.ofm_retroactive_date!.Value.Year) * 12 + approvedSA.ofm_start_date.Value.Month - approvedSA.ofm_retroactive_date.Value.Month : 0;
                    await ProcessRetroActivePayment(baseApplication!, approvedSA, processParams, fiscalYears, holidaysList, approvedSA.ofm_monthly_amount!.Value, retroActiveMonthsCount, firstAnniversaryDate, secondAnniversaryDate, fundingEndDate);
                    _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the {allowancetype} application with Id {allowanceId}", approvedSA.ofm_allowance_type, processParams.SupplementaryApplication!.allowanceId);
                }

            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task ProcessRetroActivePayment(Application baseApplication, SupplementaryApplication approvedSA, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList, decimal monthlyPaymentAmount, int retroActiveMonthsCount, DateTime firstAnniversaryDate, DateTime secondAnniversaryDate, DateTime? fundingEndDate)
        {
            decimal retroActiveAmount = retroActiveMonthsCount > 0 ? monthlyPaymentAmount * retroActiveMonthsCount : 0;
            if (retroActiveAmount > 0)
            {
                await CreateSinglePayment(approvedSA, approvedSA.ofm_start_date!.Value, retroActiveAmount, false, ecc_payment_type.Transportation, baseApplication, processParams, fiscalYears, holidaysList, firstAnniversaryDate, secondAnniversaryDate, fundingEndDate);
            }

            await SaveRetroactiveAmount(approvedSA, retroActiveAmount);

            _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the {allowancetype} application with Id {allowanceId}", approvedSA.ofm_allowance_type, processParams.SupplementaryApplication!.allowanceId);
        }

        private async Task<JsonObject> SaveRetroactiveAmount(SupplementaryApplication approvedSA, decimal retroActiveAmount)
        {
            var payload = new JsonObject {
                { ofm_allowance.Fields.ofm_retroactive_amount, retroActiveAmount}
            };

            var requestBody = JsonSerializer.Serialize(payload);

            var patchResponse = await _d365WebApiService.SendPatchRequestAsync(_appUserService.AZSystemAppUser, $"{ofm_allowance.EntitySetName}({approvedSA.Id})", requestBody);

            if (!patchResponse.IsSuccessStatusCode)
            {
                var responseBody = await patchResponse.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to update record data with the server error {responseBody}", responseBody.CleanLog());

                return ProcessResult.Failure(ProcessId, [responseBody], 0, 0).SimpleProcessResult;
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> CreateSinglePayment(SupplementaryApplication approvedSA,
                                                                    DateTime paymentDate,
                                                                    decimal? paymentAmount,
                                                                    bool manualReview,
                                                                    ecc_payment_type paymentType,
                                                                    Application baseApplication,
                                                                    ProcessParameter processParams,
                                                                    List<D365FiscalYear> fiscalYears,
                                                                    List<DateTime> holidaysList,
                                                                    DateTime firstAnniversaryDate,
                                                                    DateTime secondAnniversaryDate,
                                                                    DateTime? fundingEndDate)
        {
            DateTime invoiceDate = paymentDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
            DateTime invoiceReceivedDate = invoiceDate.AddBusinessDays(_BCCASApi.PayableInDays, holidaysList);
            DateTime effectiveDate = invoiceDate;
            //this applies if supplementary application is submitted within last 45 days to last 30 days.

            if (((CheckSubmissionIsWithinLastMonth(approvedSA.ofm_submittedon.Value, firstAnniversaryDate) &&  approvedSA.ofm_renewal_term == 1) || (CheckSubmissionIsWithinLastMonth(approvedSA.ofm_submittedon.Value, secondAnniversaryDate) && approvedSA.ofm_renewal_term == 2) || (CheckSubmissionIsWithinLastMonth(approvedSA.ofm_submittedon.Value, fundingEndDate.Value) && approvedSA.ofm_renewal_term == 3)))
            {
                invoiceReceivedDate = paymentDate.GetFirstDayOfFollowingNextMonth(holidaysList);
                invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                effectiveDate = invoiceDate;
            }
            else if (((CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, firstAnniversaryDate) && approvedSA.ofm_renewal_term == 1) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, secondAnniversaryDate) && approvedSA.ofm_renewal_term == 2) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, fundingEndDate.Value) && approvedSA.ofm_renewal_term == 3)))
            {
                invoiceReceivedDate = paymentDate.GetFirstDayOfFollowingMonth(holidaysList);
                invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                effectiveDate = invoiceDate;
            }


                if (approvedSA.ofm_retroactive_date is not null && !((CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, firstAnniversaryDate) && approvedSA.ofm_renewal_term == 1) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, secondAnniversaryDate) && approvedSA.ofm_renewal_term == 2) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, fundingEndDate.Value) && approvedSA.ofm_renewal_term == 3)))
            {
                // Date calculation logic is different for mid-year supp application. Overriding regular date logic above
                // Invoice received date is always the last business date of previous month except for first payment of the First funding year, it is 5 business day after the last business day of the last month.
                invoiceReceivedDate = paymentDate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
                invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                effectiveDate = invoiceDate;
            }

            Guid fiscalYear = paymentDate.MatchFiscalYear(fiscalYears);

            var payload = new JsonObject()
            {
                { "ofm_invoice_line_number", await GetNextInvoiceLineNumber() },
                { "ofm_amount", paymentAmount },
                { "ofm_payment_type", (int) paymentType },
                { "ofm_facility@odata.bind", $"/accounts({baseApplication.ofm_facility?.accountid})" },
                { "ofm_organization@odata.bind", $"/accounts({baseApplication.ofm_organization?.accountid})" },
                { "ofm_funding@odata.bind", $"/ofm_fundings({baseApplication.ofm_application_funding?.First().Id})" },
                { "ofm_application@odata.bind",$"/ofm_applications({baseApplication.Id})" },
                { "ofm_invoice_date", invoiceDate.ToString("yyyy-MM-dd") },
                { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
                { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
                { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" },
                { "ofm_payment_manual_review", manualReview },
                { "ofm_regardingid_ofm_allowance@odata.bind",$"/ofm_allowances({approvedSA.Id})" },
               { "ofm_fayear", approvedSA.ofm_renewal_term.ToString() }
            };

            var requestBody = JsonSerializer.Serialize(payload);
            var response = await _d365WebApiService.SendCreateRequestAsync(_appUserService.AZSystemAppUser, ofm_payment.EntitySetName, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to create a payment with the server error {responseBody}. ProcessParam {param}", responseBody.CleanLog(), JsonValue.Create(processParams)?.ToString());

                return ProcessResult.Failure(ProcessId, [responseBody], 0, 0).SimpleProcessResult;
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> CreatePaymentsInBatch(Application baseApplication, SupplementaryApplication approvedSA,
                                                                    DateTime startDate,
                                                                    DateTime endDate,
                                                                    decimal? paymentAmount,
                                                                    bool manualReview,
                                                                    ecc_payment_type paymentType,
                                                                    ProcessParameter processParams,
                                                                    List<D365FiscalYear> fiscalYears,
                                                                    List<DateTime> holidaysList,
                                                                    DateTime firstAnniversaryDate,
                                                                    DateTime secondAnniversaryDate,
                                                                    DateTime? fundingEndDate)
        {
            List<HttpRequestMessage> createPaymentRequests = [];
            int nextLineNumber = await GetNextInvoiceLineNumber();

            for (DateTime paymentDate = startDate; paymentDate <= endDate; paymentDate = paymentDate.AddMonths(1))
            {

                DateTime invoiceDate = /*(paymentDate == startDate) ? startDate.GetLastBusinessDayOfThePreviousMonth(holidaysList) : */paymentDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                DateTime invoiceReceivedDate = invoiceDate.AddBusinessDays(_BCCASApi.PayableInDays, holidaysList);
                DateTime effectiveDate = invoiceDate;

                //this applies if supplementary application is submitted within 45 days.
                 if (((CheckSubmissionIsWithinLastMonth(approvedSA.ofm_submittedon.Value, firstAnniversaryDate) && approvedSA.ofm_renewal_term == 1) || (CheckSubmissionIsWithinLastMonth(approvedSA.ofm_submittedon.Value, secondAnniversaryDate) && approvedSA.ofm_renewal_term == 2) || (CheckSubmissionIsWithinLastMonth(approvedSA.ofm_submittedon.Value, fundingEndDate.Value) && approvedSA.ofm_renewal_term == 3)))
                {
                    invoiceReceivedDate = paymentDate.GetFirstDayOfFollowingNextMonth(holidaysList);
                    invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                    effectiveDate = invoiceDate;
                }else if (((CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, firstAnniversaryDate) && approvedSA.ofm_renewal_term == 1) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, secondAnniversaryDate) && approvedSA.ofm_renewal_term == 2) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, fundingEndDate.Value) && approvedSA.ofm_renewal_term == 3)))
                {
                    invoiceReceivedDate = paymentDate.GetFirstDayOfFollowingMonth(holidaysList);
                    invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                    effectiveDate = invoiceDate;
                }


                if (approvedSA.ofm_retroactive_date is not null && !((CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, firstAnniversaryDate) && approvedSA.ofm_renewal_term == 1) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, secondAnniversaryDate) && approvedSA.ofm_renewal_term == 2) || (CheckSubmissionIsWithinLast45days(approvedSA.ofm_submittedon.Value, fundingEndDate.Value) && approvedSA.ofm_renewal_term == 3)))
                {
                    // Date calculation logic is different for mid-year supp application. Overriding regular date logic above
                    // Invoice received date is always the last business date of previous month except for first payment of the First funding year, it is 5 business day after the last business day of the last month.
                    invoiceReceivedDate = paymentDate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
                    invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                    effectiveDate = invoiceDate;
                }

                Guid? fiscalYear = startDate.MatchFiscalYear(fiscalYears);
                var paymentToCreate = new JsonObject()
                    {
                        { "ofm_invoice_line_number", nextLineNumber++ },
                        { "ofm_amount", paymentAmount },
                        { "ofm_payment_type", (int) paymentType },
                        { "ofm_facility@odata.bind", $"/accounts({baseApplication?.ofm_facility?.accountid})" },
                        { "ofm_organization@odata.bind", $"/accounts({baseApplication?.ofm_organization?.accountid})" },
                        { "ofm_funding@odata.bind", $"/ofm_fundings({baseApplication?.ofm_application_funding?.First().Id})" },
                        { "ofm_application@odata.bind",$"/ofm_applications({baseApplication?.Id})" },
                        { "ofm_invoice_date", invoiceDate.ToString("yyyy-MM-dd") },
                        { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
                        { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
                        { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" },
                        { "ofm_payment_manual_review", manualReview },
                        { "ofm_regardingid_ofm_allowance@odata.bind",$"/ofm_allowances({approvedSA.Id})" },
                        { "ofm_fayear", approvedSA.ofm_renewal_term.ToString() }
                    };

                createPaymentRequests.Add(new CreateRequest(ofm_payment.EntitySetName, paymentToCreate));
            }

            var paymentsBatchResult = await _d365WebApiService.SendBatchMessageAsync(_appUserService.AZSystemAppUser, createPaymentRequests, null);
            if (paymentsBatchResult.Errors.Any())
            {
                var errors = ProcessResult.Failure(ProcessId, paymentsBatchResult.Errors, paymentsBatchResult.TotalProcessed, paymentsBatchResult.TotalRecords);
                _logger.LogError(CustomLogEvent.Process, "Failed to create payments in batch with an error: {error}", JsonValue.Create(errors)!.ToString());

                return await Task.FromResult(errors.SimpleProcessResult);
            }

            return await Task.FromResult(paymentsBatchResult.SimpleBatchResult);
        }

        private async Task<int> GetNextInvoiceLineNumber()
        {
            int nextLineNumber = 1;

            if (_allPayments is not null && _allPayments.Count != 0)
            {
                int? currentLineNumber = _allPayments
                                    .OrderByDescending(payment => payment.ofm_invoice_line_number)
                                    .First().ofm_invoice_line_number;
                if (currentLineNumber is not null) nextLineNumber = currentLineNumber!.Value + 1;
            }

            return await Task.FromResult(nextLineNumber);
        }

        private static bool CheckSubmissionIsWithinLast45days(DateTime submittedDate, DateTime anniversayDate)
        {
            DateTime temp = anniversayDate.AddMonths(-1);
            DateTime cutoffDate = new DateTime(temp.Year, temp.Month, 15, 0, 0, 0).ToUTC();

            return submittedDate >= cutoffDate;
        }

        private static bool CheckSubmissionIsWithinLastMonth(DateTime submittedDate, DateTime anniversayDate)
        {
           DateTime temp = submittedDate.ToUniversalTime();
  
            return temp.Year == anniversayDate.Year && temp.Month == anniversayDate.Month;
        }
    }
}