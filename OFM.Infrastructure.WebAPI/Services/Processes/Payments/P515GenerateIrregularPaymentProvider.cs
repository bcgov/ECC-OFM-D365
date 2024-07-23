using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Messages;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments
{
    public class P515GenerateIrregularPaymentProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
    {
        private readonly BCCASApi _BCCASApi = bccasApiSettings.Value.BCCASApi;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365WebApiService = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private ProcessParameter? _processParams;
        private List<D365PaymentLine>? _allPayments;
        private Guid _baseApplicationId = Guid.Empty;

        public Int16 ProcessId => Setup.Process.Payments.GeneratePaymentLinesForIrregularExpenseId;
        public string ProcessName => Setup.Process.Payments.GeneratePaymentForIrregularExpenseName;

        #region Data Queries

        public string FiscalYearRequestUri
        {
            get
            {
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
                         ofm_fiscal_years?fetchXml={WebUtility.UrlEncode(fetchXml)}
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
                      <entity name="msdyn_businessclosure">
                        <attribute name="msdyn_starttime" />
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         msdyn_businessclosures?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }

        public string BaseApplicationRequestURI
        {
            get
            {
                // For reference Only
                var fetchXml = $$"""
                                <fetch distinct="true">
                                <entity name="ofm_application">
                                  <attribute name="ofm_application" />
                                  <attribute name="ofm_applicationid" />
                                  <attribute name="ofm_summary_ownership" />
                                  <attribute name="ofm_application_type" />
                                  <attribute name="ofm_funding_number_base" />
                                  <attribute name="ofm_contact" />
                                  <attribute name="ofm_expense_authority" />
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
                                  <link-entity name="ofm_funding" from="ofm_application" to="ofm_applicationid" alias="Funding">
                                    <attribute name="ofm_end_date" />
                                    <attribute name="ofm_fundingid" />
                                    <attribute name="ofm_start_date" />
                                    <attribute name="ofm_version_number" />
                                    <attribute name="statecode" />
                                    <attribute name="statuscode" />
                                    <filter>
                                      <condition attribute="ofm_version_number" operator="eq" value="0" />
                                    </filter>
                                  </link-entity>
                                  <link-entity name="ofm_expense" from="ofm_application" to="ofm_applicationid" alias="Expense">
                                    <attribute name="ofm_amount" />
                                    <attribute name="ofm_application" />
                                    <attribute name="ofm_approvedon_date" />
                                    <attribute name="ofm_assistance_request" />
                                    <attribute name="ofm_caption" />
                                    <attribute name="ofm_end_date" />
                                    <attribute name="ofm_expenseid" />
                                    <attribute name="ofm_payment_frequency" />
                                    <attribute name="ofm_start_date" />
                                    <attribute name="statecode" />
                                    <attribute name="statuscode" />
                                    <filter>
                                      <condition attribute="ofm_expenseid" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                                    </filter>
                                  </link-entity>
                                </entity>
                              </fetch>
                              """;

                var requestUri = $"""
                           ofm_applications?$select=ofm_application,ofm_applicationid,ofm_summary_ownership,ofm_application_type,ofm_funding_number_base,_ofm_contact_value,_ofm_expense_authority_value,statecode,statuscode&$expand=ofm_facility($select=accountid,accountnumber,name),ofm_organization($select=accountid,accountnumber,name),ofm_application_funding($select=ofm_end_date,ofm_fundingid,ofm_start_date,ofm_version_number,statecode,statuscode;$filter=(ofm_version_number eq 0)),ofm_application_expense($select=ofm_amount,_ofm_application_value,ofm_approvedon_date,_ofm_assistance_request_value,ofm_caption,ofm_end_date,ofm_expenseid,ofm_payment_frequency,ofm_start_date,statecode,statuscode;$filter=(ofm_expenseid eq '{_processParams!.ExpenseApplication!.expenseId}'))&$filter=(ofm_facility/accountid ne null) and (ofm_organization/accountid ne null) and (ofm_application_funding/any(o1:(o1/ofm_version_number eq 0))) and (ofm_application_expense/any(o2:(o2/ofm_expenseid eq '{_processParams!.ExpenseApplication!.expenseId}')))
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
                        <attribute name="ofm_cas_response" />
                        <attribute name="ofm_invoice_received_date" />
                        <attribute name="ofm_payment_manual_review" />
                        <attribute name="ofm_regardingid" />
                        <attribute name="ofm_remittance_message" />
                        <attribute name="ofm_revised_effective_date" />
                        <attribute name="ofm_revised_invoice_date" />
                        <attribute name="ofm_revised_invoice_received_date" />
                        <order attribute="ofm_invoice_line_number" descending="true" />
                        <filter type="and">
                          <condition attribute="ofm_application" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_payments?$select=ofm_paymentid,ofm_name,createdon,statuscode,_ofm_funding_value,ofm_payment_type,ofm_effective_date,ofm_amount,_ofm_application_value,ofm_invoice_line_number,ofm_cas_response,ofm_invoice_received_date,ofm_payment_manual_review,_ofm_regardingid_value,ofm_remittance_message,ofm_revised_effective_date,ofm_revised_invoice_date,ofm_revised_invoice_received_date&$filter=(_ofm_application_value eq '{_baseApplicationId}')&$orderby=ofm_invoice_line_number desc
                         """;

                return requestUri;
            }
        }

        #endregion

        #region Data

        public async Task<ProcessData> GetBusinessClosuresDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetBusinessClosuresDataAsync));

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, BusinessClosuresRequestUri);

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

        public async Task<ProcessData> GetFiscalYearDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetFiscalYearDataAsync));

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, FiscalYearRequestUri);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No Fiscal Year records found with query {requestUri}", FiscalYearRequestUri.CleanLog());
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
                _logger.LogError(CustomLogEvent.Process, "Failed to query all payment records by applicaitonId {applicaitonId} with the server error {responseBody}", _baseApplicationId, responseBody.CleanLog());

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

        public async Task<ProcessData> GetDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "GetDataAsync");

            var response = await _d365WebApiService.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, BaseApplicationRequestURI);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query Expense record information with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Expense records found with query {requestUri}", BaseApplicationRequestURI.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        #endregion

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            #region Validation & setup

            ArgumentNullException.ThrowIfNull(processParams);
            ArgumentNullException.ThrowIfNull(processParams.ExpenseApplication);
            ArgumentNullException.ThrowIfNull(processParams.ExpenseApplication.expenseId);

            _processParams = processParams;
            var startTime = _timeProvider.GetTimestamp();
            var baseApplicationData = await GetDataAsync();
            List<Application>? deserializedApplicationData = JsonSerializer.Deserialize<List<Application>>(baseApplicationData.Data);
            if (deserializedApplicationData is null || !deserializedApplicationData.Any())
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve the Base Application record by expenseId {expenseId}", processParams!.ExpenseApplication!.expenseId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }
            _baseApplicationId = deserializedApplicationData!.First().Id;

            ExpenseApplication? expenseInfo = deserializedApplicationData?.First()?.ofm_application_expense?.FirstOrDefault();
            if (expenseInfo is null)
            {
                _logger.LogError(CustomLogEvent.Process, "Unable to retrieve the Expense record by expenseId {expenseId}", processParams!.ExpenseApplication!.expenseId);
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            ProcessData allPaymentsData = await GetAllPaymentsByApplicationIdDataAsync();
            _allPayments = JsonSerializer.Deserialize<List<D365PaymentLine>>(allPaymentsData.Data.ToString());
            if (_allPayments is not null && _allPayments.Count > 0)
            {
                List<D365PaymentLine> expensePayments = _allPayments.Where(payment => payment?._ofm_regardingid_value != null &&
                                                                                        payment._ofm_regardingid_value == expenseInfo.Id.ToString()).ToList();

                if (expensePayments.Count > 0)
                {
                    _logger.LogWarning(CustomLogEvent.Process, "Payments have been previously generated for the Expense Application by the expenseId: {expenseId}", processParams!.ExpenseApplication!.expenseId);
                    return ProcessResult.Completed(ProcessId).SimpleProcessResult;
                }
            }

            var fiscalYearsData = await GetFiscalYearDataAsync();
            List<ofm_fiscal_year> fiscalYears = [.. JsonSerializer.Deserialize<List<ofm_fiscal_year>>(fiscalYearsData.Data)];

            var businessClosuresData = await GetBusinessClosuresDataAsync();
            var closures = JsonSerializer.Deserialize<List<BusinessClosure>>(businessClosuresData.Data.ToString());
            List<DateTime> holidaysList = closures!.Select(closure => DateTime.Parse(closure.msdyn_starttime)).ToList();

            #endregion

            switch (expenseInfo.ofm_payment_frequency)
            {
                case ecc_payment_frequency.LumpSum:
                    await CreateSinglePayment(expenseInfo, expenseInfo.ofm_start_date!.Value, expenseInfo.ofm_amount, deserializedApplicationData!.First(), processParams!, fiscalYears, holidaysList);
                    break;
                case ecc_payment_frequency.Monthly:
                    int numberOfMonthsCount = (expenseInfo.ofm_end_date.Value.Year - expenseInfo.ofm_start_date.Value.Year) * 12 + expenseInfo.ofm_end_date.Value.Month - expenseInfo.ofm_start_date.Value.Month + 1;
                    var expenseAmount = expenseInfo.ofm_amount / numberOfMonthsCount;

                    await CreatePaymentsInBatch(expenseInfo, expenseInfo.ofm_start_date!.Value, expenseInfo.ofm_end_date.Value, expenseInfo.ofm_amount, deserializedApplicationData!.First(), processParams!, fiscalYears, holidaysList);
                    break;
                default:
                    _logger.LogError(CustomLogEvent.Process, "Unable to generate payments for Expense record with Id {expenseId}. Invalid Payment Frequency {frequency}", processParams?.ExpenseApplication.expenseId, expenseInfo.ofm_payment_frequency);
                    break;
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> CreateSinglePayment(ExpenseApplication expenseInfo,
                                                                           DateTime paymentDate,
                                                                           decimal? expenseAmount,
                                                                           Application baseApplication,
                                                                           ProcessParameter processParams,
                                                                           List<ofm_fiscal_year> fiscalYears,
                                                                           List<DateTime> holidaysList)
        {
            DateTime invoiceReceivedDate = paymentDate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
            DateTime invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
            DateTime effectiveDate = invoiceDate;

            Guid fiscalYear = paymentDate.MatchFiscalYear(fiscalYears);

            var payload = new JsonObject()
                        {
                            { "ofm_invoice_line_number", await GetNextInvoiceLineNumber(baseApplication!.Id) },
                            { "ofm_amount", expenseAmount},
                            { "ofm_payment_type", (int) ecc_payment_type.IrregularExpense },
                            { "ofm_application@odata.bind",$"/ofm_applications({baseApplication!.Id})" },
                            { "ofm_invoice_date", invoiceDate.ToString("yyyy-MM-dd") },
                            { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
                            { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
                            { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" },
                            { "statuscode", (int) ofm_payment_StatusCode.ApprovedforPayment },
                            { "ofm_regardingid_ofm_expense@odata.bind",$"/ofm_expenses({expenseInfo.Id})"  },
                            { "ofm_facility@odata.bind", $"/accounts({baseApplication!.ofm_facility!.accountid})" },
                            { "ofm_organization@odata.bind", $"/accounts({baseApplication!.ofm_organization!.accountid})" }
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

        private async Task<JsonObject> CreatePaymentsInBatch(ExpenseApplication expenseInfo,
                                                                    DateTime startDate,
                                                                    DateTime endDate,
                                                                    decimal? monthlyExpenseAmount,
                                                                    Application baseApplication,
                                                                    ProcessParameter processParams,
                                                                    List<ofm_fiscal_year> fiscalYears,
                                                                    List<DateTime> holidaysList)
        {
            List<HttpRequestMessage> createPaymentRequests = [];
            int nextLineNumber = await GetNextInvoiceLineNumber(baseApplication.Id);

            for (DateTime paymentDate = startDate; paymentDate <= endDate; paymentDate = paymentDate.AddMonths(1))
            {
                Guid? fiscalYear = paymentDate.MatchFiscalYear(fiscalYears);

                DateTime invoiceReceivedDate = paymentDate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
                DateTime invoiceDate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList, _BCCASApi.PayableInDays);
                DateTime effectiveDate = invoiceDate;

                var paymentToCreate = new JsonObject()
                        {
                            { "ofm_invoice_line_number",nextLineNumber ++ },
                            { "ofm_amount", monthlyExpenseAmount},
                            { "ofm_payment_type", (int) ecc_payment_type.IrregularExpense },
                            { "ofm_application@odata.bind",$"/ofm_applications({baseApplication!.Id})" },
                            { "ofm_invoice_date", invoiceDate.ToString("yyyy-MM-dd") },
                            { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
                            { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
                            { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" },
                            { "statuscode", (int) ofm_payment_StatusCode.ApprovedforPayment },
                            { "ofm_regardingid_ofm_expense@odata.bind",$"/ofm_expenses({expenseInfo.Id})"  },
                            { "ofm_facility@odata.bind", $"/accounts({baseApplication!.ofm_facility!.accountid})" },
                            { "ofm_organization@odata.bind", $"/accounts({baseApplication!.ofm_organization!.accountid})" }
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

        private async Task<int> GetNextInvoiceLineNumber(Guid baseApplicationId)
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