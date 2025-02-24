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
        private List<D365PaymentLine>? _allPayments;
        private Guid _applicationId;

        public Int16 ProcessId => Setup.Process.Payments.GenerateTopUpPaymentId;
        public string ProcessName => Setup.Process.Payments.GenerateTopUpPaymentName;

        #region Data Queries

        public string TopUpUri
        {
            get
            {
                var fetchXml = $$"""
                <fetch>
                  <entity name="ofm_top_up_fund">
                    <attribute name="ofm_approved_by" />
                    <attribute name="ofm_approved_date" />
                    <attribute name="ofm_end_date" />
                    <attribute name="ofm_facility" />
                    <attribute name="ofm_funding" />
                    <attribute name="ofm_pcm_validated" />
                    <attribute name="ofm_programming_amount" />
                    <attribute name="ofm_start_date" />
                    <attribute name="statuscode" />
                    <filter>
                      <condition attribute="ofm_funding" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                    </filter>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                         ofm_top_up_funds?$select=_ofm_approved_by_value,ofm_approved_date,ofm_end_date,_ofm_facility_value,_ofm_funding_value,ofm_pcm_validated,ofm_programming_amount,ofm_start_date,statuscode&$filter=(ofm_top_up_fundid eq {_processParams?.Topup?.TopupId})
                         """;

                return requestUri;
            }
        }

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
                          <condition attribute="ofm_application" operator="eq" value="{_applicationId}" />
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
                         ofm_payments?$select=ofm_paymentid,ofm_name,createdon,statuscode,_ofm_funding_value,ofm_payment_type,ofm_effective_date,ofm_amount,_ofm_application_value,ofm_invoice_line_number,ofm_invoice_received_date,ofm_payment_manual_review,_ofm_regardingid_value,ofm_remittance_message,ofm_revised_effective_date,ofm_revised_invoice_date,ofm_revised_invoice_received_date,_ofm_facility_value,_ofm_fiscal_year_value,ofm_invoice_date,ofm_invoice_number,statecode&$expand=ofm_funding($select=ofm_end_date,ofm_fundingid,ofm_monthly_province_base_funding_y1,ofm_start_date,ofm_version_number),ofm_application($select=ofm_application,ofm_applicationid,statuscode)&$filter=(_ofm_application_value eq '{_applicationId}') and (ofm_funding/ofm_fundingid ne null) and (ofm_application/ofm_applicationid ne null)&$orderby=ofm_invoice_line_number desc
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
            ArgumentNullException.ThrowIfNull(processParams.Organization);
            ArgumentNullException.ThrowIfNull(processParams.Organization.organizationId);

            _logger.LogTrace(CustomLogEvent.Process, "Start processing payments for the funding {FundingId}.", processParams.Funding.FundingId);

            _processParams = processParams;

            Funding? funding = await _fundingRepository!.GetFundingByIdAsync(new Guid(processParams.Funding!.FundingId!), isCalculator: false);

            if (funding is null)
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve Funding record with Id {FundingId}", processParams.Funding!.FundingId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

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

            if (funding.ofm_application is null)
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve application record with Funding Id {FundingId}", processParams.Funding!.FundingId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            _applicationId = funding.ofm_application.Id;

            ProcessData allExistingPaymentsByApplicationIdData = await GetAllPaymentsByApplicationIdDataAsync();
            _allPayments = JsonSerializer.Deserialize<List<D365PaymentLine>>(allExistingPaymentsByApplicationIdData.Data.ToString());

            List<D365PaymentLine> topUpPayments = [];


            if (funding.statuscode == ofm_funding_StatusCode.Active && _allPayments is not null && _allPayments.Count > 0)
            {
                topUpPayments = _allPayments.Where(payment => payment?._ofm_regardingid_value != null && payment?._ofm_regardingid_value == _processParams.Topup.TopupId && payment.statecode != ofm_payment_statecode.Inactive).ToList();
            }

            var topupData = await GetDataAsync();
            var topUp = JsonSerializer.Deserialize<List<TopUp>>(topupData.Data).FirstOrDefault();

            #endregion

            if(topUp?.statuscode == ofm_top_up_fund_StatusCode.Approved)
            {
                if (topUpPayments.Count > 0)
                {
                    _logger.LogWarning(CustomLogEvent.Process, "Payments have been previously generated for Topup with the Id: {TopupId}", _processParams.Topup.TopupId);
                    return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                }

                await ProcessTopUpFundingPayments(topUp, funding, _processParams, fiscalYears, holidaysList);
            }else if(topUp.statuscode == ofm_top_up_fund_StatusCode.Cancelled)
            {
                if(topUpPayments.Count > 0)
                {
                    await CancelUnpaidPayments(topUpPayments);
                }
            }
 

            _logger.LogTrace(CustomLogEvent.Process, "Finished processing payments for the Topup {TopupId}.", _processParams.Topup.TopupId);

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> ProcessTopUpFundingPayments(TopUp topUp, Funding funding, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Processing payments for the top up {ofm_top_up}", topUp.ofm_name);

            await CreatePaymentsInBatch(topUp, funding, topUp!.ofm_start_date!.Value, topUp!.ofm_end_date!.Value, false, processParams, fiscalYears, holidaysList);
            _logger.LogInformation(CustomLogEvent.Process, "Finished payments generation for the Initial funding {ofm_funding_number}", funding.ofm_funding_number);


            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }


        private async Task<JsonObject> CreatePaymentsInBatch(TopUp topUp, Funding funding, DateTime startDate, DateTime endDate, bool manualReview, ProcessParameter processParams, List<D365FiscalYear> fiscalYears, List<DateTime> holidaysList)
        {
            if (startDate > endDate)
            {
                _logger.LogError(CustomLogEvent.Process, "Failed to create payments in batch. Start Date {startDate} is later than End Date {endDate}", startDate, endDate);
                return await Task.FromResult(new JsonObject());
            }


            //set the payment start date
            var paymentStartDate = new DateTime();

            if(startDate.Date == funding.ofm_start_date.Value.Date)
            {
                paymentStartDate = startDate;
            }
            else
            {
                var temp = new DateTime();

                if (startDate.Day <= 15)
                {
                    temp = startDate.AddMonths(1);
                    endDate = endDate.AddMonths(1);
                }
                else
                {
                    temp = startDate.AddMonths(2);
                    endDate = endDate.AddMonths(2);
                }

                paymentStartDate = new DateTime(temp.Year, temp.Month, 1);
            }


            var totalFunding = topUp.ofm_programming_amount.Value;

            int numberOfMonths = (endDate.Year - paymentStartDate.Year) * 12 + endDate.Month - paymentStartDate.Month + 1;

            decimal monthlyAmount = totalFunding / numberOfMonths;


            List<HttpRequestMessage> createPaymentRequests = [];

            Int32 lineNumber = await GetNextInvoiceLineNumber();

            for (DateTime paymentDate = paymentStartDate; paymentDate <= endDate || numberOfMonths > 0; paymentDate = paymentDate.AddMonths(1), numberOfMonths--)
            {
                DateTime invoiceDate = (paymentDate == paymentStartDate && paymentStartDate.Date == funding.ofm_start_date.Value.Date) ? paymentStartDate.GetLastBusinessDayOfThePreviousMonth(holidaysList) : paymentDate.GetLastBusinessDayOfThePreviousMonth(holidaysList).GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                DateTime invoiceReceivedDate = invoiceDate.AddBusinessDays(_BCCASApi.PayableInDays, holidaysList);
                DateTime effectiveDate = invoiceDate;

/*                if (processParams.Funding!.IsMod!.Value)
                {
                    // Date calculation logic is different for mid-year supp application. Overriding regular date logic above
                    // Invoice received date is always the last business date of previous month except for first payment of the First funding year, it is 5 business day after the last business day of the last month.
                    invoiceReceivedDate = paymentDate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
                    invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                    effectiveDate = invoiceDate;
                }
*/
                Guid fiscalYearId = invoiceDate.MatchFiscalYear(fiscalYears);

                var FAYear = (invoiceReceivedDate < funding.ofm_start_date.Value.AddYears(1)) ? 1 :
                (invoiceReceivedDate < funding.ofm_start_date.Value.AddYears(2)) ? 2 :
                (invoiceReceivedDate < funding.ofm_start_date.Value.AddYears(3)) ? 3 : 0;

                var paymentToCreate = new JsonObject()
                {
                    { "ofm_invoice_line_number", lineNumber++ },
                    { "ofm_amount", monthlyAmount},
                    { "ofm_payment_type", (int) ecc_payment_type.TopUp },
                    { "ofm_facility@odata.bind", $"/accounts({funding.ofm_facility?.Id})" },
                    { "ofm_organization@odata.bind", $"/accounts({_processParams?.Organization?.organizationId})" },
                    { "ofm_funding@odata.bind", $"/ofm_fundings({funding?.Id})" },
                    { "ofm_application@odata.bind",$"/ofm_applications({funding?.ofm_application?.Id})" },
                    { "ofm_invoice_date", invoiceDate.ToString("yyyy-MM-dd") },
                    { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
                    { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
                    { "ofm_payment_manual_review", manualReview },
                    { "ofm_fayear", FAYear.ToString() },
                    { "ofm_regardingid_ofm_top_up_fund@odata.bind",$"/ofm_top_up_funds({topUp.Id})" }
                };

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