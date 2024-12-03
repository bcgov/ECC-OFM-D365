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

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments
{
    public class P505GeneratePaymentLinesProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, IFundingRepository fundingRepository, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
    {
        private readonly BCCASApi _BCCASApi = bccasApiSettings.Value.BCCASApi;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365WebApiService = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly IFundingRepository _fundingRepository = fundingRepository;
        private readonly TimeProvider _timeProvider = timeProvider;
        private ProcessParameter? _processParams;
        private List<D365PaymentLine>? _allPayments;

        public Int16 ProcessId => Setup.Process.Payments.GeneratePaymentLinesId;
        public string ProcessName => Setup.Process.Payments.GeneratePaymentLinesName;

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
                        <attribute name="ofm_invoice_received_date" />
                        <attribute name="ofm_payment_manual_review" />
                        <attribute name="ofm_regardingid" />
                        <attribute name="ofm_remittance_message" />
                        <attribute name="ofm_revised_effective_date" />
                        <attribute name="ofm_revised_invoice_date" />
                        <attribute name="ofm_revised_invoice_received_date" />
                        <attribute name="ofm_facility" />
                        <attribute name="ofm_fiscal_year" />
                        <attribute name="ofm_invoice_date" />
                        <attribute name="ofm_invoice_number" />
                        <attribute name="statecode" />
                        <order attribute="ofm_invoice_line_number" descending="true" />
                        <filter type="and">
                          <condition attribute="ofm_application" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                        </filter>
                        <link-entity name="ofm_funding" from="ofm_fundingid" to="ofm_funding" link-type="inner" alias="Funding">
                          <attribute name="ofm_end_date" />
                          <attribute name="ofm_fundingid" />
                          <attribute name="ofm_monthly_province_base_funding_y1" />
                          <attribute name="ofm_start_date" />
                          <attribute name="ofm_version_number" />
                        </link-entity>
                        <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" link-type="inner" alias="App">
                          <attribute name="ofm_application" />
                          <attribute name="ofm_applicationid" />
                          <attribute name="statuscode" />
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_payments?$select=ofm_paymentid,ofm_name,createdon,statuscode,_ofm_funding_value,ofm_payment_type,ofm_effective_date,ofm_amount,_ofm_application_value,ofm_invoice_line_number,ofm_invoice_received_date,ofm_payment_manual_review,_ofm_regardingid_value,ofm_remittance_message,ofm_revised_effective_date,ofm_revised_invoice_date,ofm_revised_invoice_received_date,_ofm_facility_value,_ofm_fiscal_year_value,ofm_invoice_date,ofm_invoice_number,statecode&$expand=ofm_funding($select=ofm_end_date,ofm_fundingid,ofm_monthly_province_base_funding_y1,ofm_start_date,ofm_version_number),ofm_application($select=ofm_application,ofm_applicationid,statuscode)&$filter=(_ofm_application_value eq '{_processParams!.Application!.applicationId}') and (ofm_funding/ofm_fundingid ne null) and (ofm_application/ofm_applicationid ne null)&$orderby=ofm_invoice_line_number desc
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

        public string ApprovedSupplementariesByApplicationIdRequestURI
        {
            get
            {
                var fetchXml = $$"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_allowance">
                        <attribute name="ofm_allowanceid" />
                        <attribute name="ofm_allowance_number" />
                        <attribute name="createdon" />
                        <attribute name="ofm_allowance_type" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_start_date" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_funding_amount" />
                        <attribute name="ofm_renewal_term" />
                        <order attribute="ofm_allowance_number" descending="false" />
                        <filter type="and">
                          <condition attribute="ofm_application" operator="eq"  value="{{_processParams!.Application!.applicationId}}" />
                          <condition attribute="statuscode" operator="eq" value="{{(int)ofm_allowance_StatusCode.Approved}}" /> 
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                        ofm_allowances?$select=ofm_allowanceid,ofm_allowance_number,createdon,ofm_allowance_type,ofm_end_date,ofm_start_date,statuscode,ofm_funding_amount,ofm_renewal_term&$filter=(_ofm_application_value eq {_processParams!.Application!.applicationId} and statuscode eq {(int)ofm_allowance_StatusCode.Approved})&$orderby=ofm_allowance_number asc
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

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, AllPaymentsByApplicationIdRequestUri, pageSize: 0, isProcess: true);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No payment records found with query {requestUri}", AllPaymentsByApplicationIdRequestUri.CleanLog());
                }

                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<ProcessData> GetApprovedSupplementaryApplicationsByApplicationIdDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetApprovedSupplementaryApplicationsByApplicationIdDataAsync));

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, ApprovedSupplementariesByApplicationIdRequestURI);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query Supplementary Allowance records with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogWarning(CustomLogEvent.Process, "No Supplementary Allowance records found with query {requestUri}", ApprovedSupplementariesByApplicationIdRequestURI.CleanLog());
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
                _logger.LogError(CustomLogEvent.Process, "Failed to query all payment records by applicaitonId {applicaitonId} with the server error {responseBody}", _processParams!.Application!.applicationId, responseBody.CleanLog());

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
            ArgumentNullException.ThrowIfNull(processParams.Funding);
            ArgumentNullException.ThrowIfNull(processParams.Funding.FundingId);
            ArgumentNullException.ThrowIfNull(processParams.Funding.MonthlyBaseFundingAmount);
            ArgumentNullException.ThrowIfNull(processParams.Application);
            ArgumentNullException.ThrowIfNull(processParams.Application.applicationId);

            _logger.LogTrace(CustomLogEvent.Process, "Start processing payments for the funding {FundingId}.", processParams.Funding.FundingId);

            _processParams = processParams;

            Funding? funding = await _fundingRepository!.GetFundingByIdAsync(new Guid(processParams.Funding!.FundingId!), isCalculator: false);

            if (funding is null)
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve Funding record with Id {FundingId}", processParams.Funding!.FundingId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            ProcessData allExistingPaymentsByApplicationIdData = await GetDataAsync();
            _allPayments = JsonSerializer.Deserialize<List<D365PaymentLine>>(allExistingPaymentsByApplicationIdData.Data.ToString());
            if (funding.statuscode == ofm_funding_StatusCode.Active && _allPayments is not null && _allPayments.Count > 0)
            {
                List<D365PaymentLine> fundingPayments = _allPayments.Where(payment => payment?.ofm_funding?.Id == funding.Id).ToList();
                if (fundingPayments.Count > 0)
                {
                    _logger.LogWarning(CustomLogEvent.Process, "Payments have been previously generated for Funding with the Id: {FundingId}", processParams!.Funding!.FundingId);
                    return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                }
            }

            var fiscalYearsData = await GetAllFiscalYearsDataAsync();
            List<D365FiscalYear> fiscalYears = [.. JsonSerializer.Deserialize<List<D365FiscalYear>>(fiscalYearsData.Data)];

            var businessClosuresData = await GetBusinessClosuresDataAsync();
            var closures = JsonSerializer.Deserialize<List<ofm_stat_holiday>>(businessClosuresData.Data.ToString());
            List<DateTime> holidaysList = closures!.Select(closure => (DateTime)closure.ofm_date_observed).ToList();

            #endregion

            switch (funding.statuscode)
            {
                case ofm_funding_StatusCode.Active:
                    await ProcessInitialOrModFundingPayments(funding, processParams, fiscalYears, holidaysList);

                    _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the Active Initial or Mod funding {ofm_funding_number}", funding.ofm_funding_number);
                    break;
                case ofm_funding_StatusCode.Terminated:
                case ofm_funding_StatusCode.Cancelled:
                case ofm_funding_StatusCode.Expired:
                    await CancelUnpaidPayments(_allPayments);

                    _logger.LogInformation(CustomLogEvent.Process, "Finished cancelling unpaid payments for the Inactive Funding {ofm_funding_number}", funding.ofm_funding_number);
                    break;
                default:
                    _logger.LogWarning(CustomLogEvent.Process, "Unable to process payments with Invalid Funding status {statuscode} for the funding {ofm_funding_number}. Process Params: {param}", funding.statuscode, funding.ofm_funding_number, JsonValue.Create(processParams)!.ToString());
                    break;
            }

            _logger.LogTrace(CustomLogEvent.Process, "Finished processing payments for the funding {FundingId}.", processParams.Funding.FundingId);

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> ProcessInitialOrModFundingPayments(Funding funding, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Processing Initial or Mod payments for the funding {ofm_funding_number}", funding.ofm_funding_number);

            ArgumentNullException.ThrowIfNull(processParams.Funding!.IsMod);
            ArgumentNullException.ThrowIfNull(processParams.Funding!.MonthlyBaseFundingAmount);

            if (processParams!.Funding!.IsMod!.Value)
            {
                await ProcessModPayments(processParams, funding, processParams.Funding.MonthlyBaseFundingAmount.Value, fiscalYears, holidaysList);
                _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the Mod funding {ofm_funding_number}", funding.ofm_funding_number);
            }
            else
            {
                await CreatePaymentsInBatch(funding, processParams.Funding.MonthlyBaseFundingAmount.Value, funding!.ofm_start_date!.Value, funding!.ofm_end_date!.Value, false, processParams, fiscalYears, holidaysList, null, null);
                _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the Initial funding {ofm_funding_number}", funding.ofm_funding_number);
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> ProcessModPayments(ProcessParameter processParams, Funding funding, decimal monthlyFundingAmount, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList)
        {
            #region Validation   

            ArgumentNullException.ThrowIfNull(processParams.Funding!.PreviousMonthlyBaseFundingAmount);
            if (funding.ofm_retroactive_payment_frequency is not null && funding.ofm_retroactive_payment_date >= funding.ofm_start_date)
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to process Mod Payments generation for the funding {ofm_funding_number}. Invalid retroactive date.", funding.ofm_funding_number);

                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            #endregion

            decimal previousMonthlyFundingAmount = processParams!.Funding!.PreviousMonthlyBaseFundingAmount.Value;
            int retroActiveMonthsCount = 0;
            if (funding.ofm_retroactive_payment_date.HasValue)
                retroActiveMonthsCount = (funding!.ofm_start_date!.Value.Year - funding.ofm_retroactive_payment_date.Value.Year) * 12 + funding.ofm_start_date.Value.Month - funding.ofm_retroactive_payment_date.Value.Month;
            bool retroActivePaymentYesOrNo = (retroActiveMonthsCount > 0);
            int remainingModMonthsCount = (funding!.ofm_end_date!.Value.Year - funding!.ofm_start_date!.Value.Year) * 12 + (funding.ofm_end_date.Value.Month - funding.ofm_start_date.Value.Month) + 1;
            decimal adjustedMonthlyCreditOrDebitOnly = monthlyFundingAmount - previousMonthlyFundingAmount; // Positive or Negative (e.g.: facility cost has reduced or less operational space changes)

            // Processing future payments
            await CreatePaymentsInBatch(funding, adjustedMonthlyCreditOrDebitOnly, funding!.ofm_start_date!.Value, funding!.ofm_end_date!.Value, false, processParams, fiscalYears, holidaysList, null, null);

            if (retroActivePaymentYesOrNo)
            {
                decimal retroActiveCreditOrDebitLumpSumAmount = adjustedMonthlyCreditOrDebitOnly * retroActiveMonthsCount;
                decimal retroActiveCreditOrDebitMonthlyAmount = retroActiveCreditOrDebitLumpSumAmount / remainingModMonthsCount;
                if (retroActiveCreditOrDebitLumpSumAmount != 0)
                {
                    await ProcessRetroActivePaymentsForMod(funding, retroActiveCreditOrDebitLumpSumAmount, retroActiveCreditOrDebitMonthlyAmount, processParams, fiscalYears, holidaysList);
                }
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> ProcessRetroActivePaymentsForMod(Funding funding, decimal retroActiveCreditOrDebitLumpSumAmount, decimal retroActiveCreditOrDebitMonthlyAmount, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList)
        {
            if (funding.ofm_retroactive_payment_frequency == ecc_payment_frequency.Monthly)
            {
                await CreatePaymentsInBatch(funding, retroActiveCreditOrDebitMonthlyAmount, funding!.ofm_start_date!.Value, funding!.ofm_end_date!.Value, false, processParams, fiscalYears, holidaysList, null, null);
                _logger.LogInformation(CustomLogEvent.Process, "Finished Mod payments generation for the funding {ofm_funding_number}", funding.ofm_funding_number);
            }
            else
            {
                await CreatePaymentsInBatch(funding, retroActiveCreditOrDebitLumpSumAmount, funding!.ofm_start_date!.Value, funding!.ofm_start_date!.Value, true, processParams, fiscalYears, holidaysList, null, null);
                _logger.LogInformation(CustomLogEvent.Process, "Finished Mod payments generation for the funding {ofm_funding_number}", funding.ofm_funding_number);
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> CreatePaymentsInBatch(Funding funding, decimal fundingAmount, DateTime startDate, DateTime endDate, bool manualReview, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList, Guid? regardingid, string? regardingTableSet)
        {
            if (startDate > endDate)
            {
                _logger.LogError(CustomLogEvent.Process, "Failed to create payments in batch. Start Date {startDate} is later than End Date {endDate}", startDate, endDate);
                return await Task.FromResult(new JsonObject());
            }

            List<HttpRequestMessage> createPaymentRequests = [];

            Int32 lineNumber = await GetNextInvoiceLineNumber();

            for (DateTime paymentDate = startDate; paymentDate <= endDate; paymentDate = paymentDate.AddMonths(1))
            {       
                DateTime invoiceDate = (paymentDate == startDate) ? startDate.GetLastBusinessDayOfThePreviousMonth(holidaysList) : paymentDate.GetLastBusinessDayOfThePreviousMonth(holidaysList).GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                DateTime invoiceReceivedDate = invoiceDate.AddBusinessDays(_BCCASApi.PayableInDays, holidaysList);
                DateTime effectiveDate = invoiceDate;
                
                if (processParams.Funding!.IsMod!.Value)
                {
                    // Date calculation logic is different for mid-year supp application. Overriding regular date logic above
                    // Invoice received date is always the last business date of previous month except for first payment of the First funding year, it is 5 business day after the last business day of the last month.
                    invoiceReceivedDate = paymentDate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
                    invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                    effectiveDate = invoiceDate;
                }
                Guid fiscalYearId = invoiceDate.MatchFiscalYear(fiscalYears);

                var FAYear = (invoiceReceivedDate < startDate.AddYears(1)) ? 1 :
                (invoiceReceivedDate < startDate.AddYears(2)) ? 2 :
                (invoiceReceivedDate < startDate.AddYears(3)) ? 3 : 0;
                var paymentToCreate = new JsonObject()
                {
                    { "ofm_invoice_line_number", lineNumber++ },
                    { "ofm_amount", fundingAmount},
                    { "ofm_payment_type", (int) ecc_payment_type.Base },
                    { "ofm_facility@odata.bind", $"/accounts({funding.ofm_facility!.Id})" },
                    { "ofm_organization@odata.bind", $"/accounts({processParams!.Organization!.organizationId})" },
                    { "ofm_funding@odata.bind", $"/ofm_fundings({processParams!.Funding!.FundingId})" },
                    { "ofm_application@odata.bind",$"/ofm_applications({processParams!.Application!.applicationId})" },
                    { "ofm_invoice_date", invoiceDate.ToString("yyyy-MM-dd") },
                    { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
                    { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
                    { "ofm_payment_manual_review", manualReview },
                    { "ofm_fayear", FAYear.ToString() }

                };
                if (regardingid is not null && !string.IsNullOrEmpty(regardingTableSet))
                {
                    paymentToCreate.Add($"ofm_regardingid_{(regardingTableSet!).TrimEnd('s')}@odata.bind", $"/{regardingTableSet}({regardingid})");
                }
                if (fiscalYearId != Guid.Empty)
                {
                    paymentToCreate.Add("ofm_fiscal_year@odata.bind", $"/ofm_fiscal_years({fiscalYearId})");
                }

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

        private async Task<JsonObject> CancelUnpaidPayments(List<D365PaymentLine> deserializedPaymentsData)
        {
            List<HttpRequestMessage> updatePaymentRequests = [];
            List<D365PaymentLine> unpaidPayments = deserializedPaymentsData.Where(r => r.statuscode != ofm_payment_StatusCode.Paid && r.statuscode != ofm_payment_StatusCode.ProcessingPayment).ToList();
            if (unpaidPayments != null)
            {
                foreach (var payment in unpaidPayments)
                {
                    var statement = $"ofm_payments({payment.Id})";

                    var payload = new JsonObject {
                                { "statuscode", (int) ofm_payment_StatusCode.Cancelled},
                                { "statecode", (int) ofm_payment_statecode.Inactive }
                    };

                    updatePaymentRequests.Add(new D365UpdateRequest(new D365EntityReference(ofm_payment.EntitySetName, payment.Id), payload));
                }

                var paymentsBatchResult = await _d365WebApiService.SendBatchMessageAsync(_appUserService.AZSystemAppUser, updatePaymentRequests, null);
                if (paymentsBatchResult.Errors.Any())
                {
                    var errors = ProcessResult.Failure(ProcessId, paymentsBatchResult.Errors, paymentsBatchResult.TotalProcessed, paymentsBatchResult.TotalRecords);
                    _logger.LogError(CustomLogEvent.Process, "Failed to cancel payments in batch with an error: {error}", JsonValue.Create(errors)!.ToString());

                    return await Task.FromResult(errors.SimpleProcessResult);
                }
            }

            return await Task.FromResult(new JsonObject());
        }

        private async Task<int> GetNextInvoiceLineNumber()
        {
            int nextLineNumber = 1;

            if (_allPayments is not null && _allPayments.Count > 0)
            {
                int? currentLineNumber = _allPayments
                                    .OrderByDescending(payment => payment.ofm_invoice_line_number)
                                    .First().ofm_invoice_line_number;
                if (currentLineNumber is not null) nextLineNumber = currentLineNumber!.Value + 1;
            }

            return await Task.FromResult(nextLineNumber);
        }
    }
}