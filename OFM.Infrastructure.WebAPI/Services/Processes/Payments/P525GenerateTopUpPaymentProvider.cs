using ECC.Core.DataContext;
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

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments
{
    public class P525GenerateTopUpPaymentProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, IFundingRepository fundingRepository, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
    {
        private readonly BCCASApi _BCCASApi = bccasApiSettings.Value.BCCASApi;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365WebApiService = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly IFundingRepository _fundingRepository = fundingRepository;
        private readonly TimeProvider _timeProvider = timeProvider;
        private ProcessParameter? _processParams;

        public Int16 ProcessId => Setup.Process.Payments.GenerateTopUpPaymentId;
        public string ProcessName => Setup.Process.Payments.GenerateTopUpPaymentName;

        #region Data Queries

        public string TopUpUri
        {
            get
            {
                var fetchXml = $$"""

                """;

                var requestUri = $"""
                         ofm_fiscal_years?$select=ofm_caption,createdon,ofm_agreement_number_seed,ofm_end_date,ofm_fiscal_year_number,_owningbusinessunit_value,ofm_start_date,statuscode,ofm_fiscal_yearid&$orderby=ofm_caption asc
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

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, AllFiscalYearsRequestUri, false, 0, true);

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

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, TopUpUri, pageSize: 0, isProcess: true);

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

        #endregion


        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            #region Validation & Setup

            ArgumentNullException.ThrowIfNull(processParams);
            ArgumentNullException.ThrowIfNull(processParams.Funding);
            ArgumentNullException.ThrowIfNull(processParams.Funding.FundingId);
            ArgumentNullException.ThrowIfNull(processParams.Topup);
            ArgumentNullException.ThrowIfNull(processParams.Topup.TopupId);

            _logger.LogTrace(CustomLogEvent.Process, "Start processing payments for the funding {FundingId}.", processParams.Funding.FundingId);

            _processParams = processParams;

            Funding? funding = await _fundingRepository!.GetFundingByIdAsync(new Guid(processParams.Funding!.FundingId!), isCalculator: false);

            if (funding is null)
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve Funding record with Id {FundingId}", processParams.Funding!.FundingId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            ProcessData topupData = await GetDataAsync();

            if (funding.statuscode != ofm_funding_StatusCode.Active)
            {
                _logger.LogError(CustomLogEvent.Process, "Funding is not active with Id {FundingId}", processParams.Funding!.FundingId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            var fiscalYearsData = await GetAllFiscalYearsDataAsync();
            List<D365FiscalYear> fiscalYears = [.. JsonSerializer.Deserialize<List<D365FiscalYear>>(fiscalYearsData.Data)];

            var businessClosuresData = await GetBusinessClosuresDataAsync();
            var closures = JsonSerializer.Deserialize<List<ofm_stat_holiday>>(businessClosuresData.Data.ToString());
            List<DateTime> holidaysList = closures!.Select(closure => (DateTime)closure.ofm_date_observed).ToList();
            #endregion


            await ProcessInitialOrModFundingPayments(funding, processParams, fiscalYears, holidaysList);

            _logger.LogTrace(CustomLogEvent.Process, "Finished processing payments for the funding {FundingId}.", processParams.Funding.FundingId);

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> ProcessInitialOrModFundingPayments(Funding funding, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Processing Initial or Mod payments for the funding {ofm_funding_number}", funding.ofm_funding_number);

            ArgumentNullException.ThrowIfNull(processParams.Funding!.IsMod);
            ArgumentNullException.ThrowIfNull(processParams.Funding!.MonthlyBaseFundingAmount);



            await CreatePaymentsInBatch(funding, processParams.Funding.MonthlyBaseFundingAmount.Value, funding!.ofm_start_date!.Value, funding!.ofm_end_date!.Value, false, processParams, fiscalYears, holidaysList, null, null);
            _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the Initial funding {ofm_funding_number}", funding.ofm_funding_number);


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