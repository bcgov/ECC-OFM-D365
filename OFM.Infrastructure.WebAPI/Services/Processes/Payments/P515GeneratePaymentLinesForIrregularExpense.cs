using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments
{
    public class P515GeneratePaymentLinesForIrregularExpense(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
    {
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private ProcessParameter? _processParams;
        private string _expenseApplicationId = string.Empty;
        private string _applicationId = string.Empty; 

        public Int16 ProcessId => Setup.Process.Payments.GeneratePaymentLinesForIrregularExpenseId;
        public string ProcessName => Setup.Process.Payments.GeneratePaymentForIrregularExpenseResponseName;

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

        //Retrieve Business Closures.
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

        //Retrieve expesne Information.
        public string ExpenseApplicationRequestURI
        {
            get
            {
                var fetchXml = $$"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_expense">
                        <attribute name="ofm_expenseid" />
                        <attribute name="ofm_caption" />
                        <attribute name="createdon" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_start_date" />
                        <attribute name="ofm_request_summary" />
                        <attribute name="ofm_payment_frequency" />
                        <attribute name="ofm_amount" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_assistance_request" />
                        <attribute name="ofm_application" />
                        <order attribute="ofm_caption" descending="false" />
                        <filter type="and">
                      <condition attribute="ofm_expenseid" operator="eq"  value="{{_expenseApplicationId}}" />
                    </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                           ofm_expenses?fetchXml={WebUtility.UrlEncode(fetchXml)}
                           """;

                return requestUri;
            }
        }

       

        

        public async Task<ProcessData> GetBusinessClosuresDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetBusinessClosuresDataAsync));

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, BusinessClosuresRequestUri);

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

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, FiscalYearRequestUri);

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

        public async Task<ProcessData> GetDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "GetDataAsync");

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, ExpenseApplicationRequestURI);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding records found with query {requestUri}", ExpenseApplicationRequestURI.CleanLog());
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
            _expenseApplicationId = processParams?.ExpenseApplication.expenseId.ToString();
            // Get Expense Application
            var localData = await GetDataAsync();


            var deserializedData = JsonSerializer.Deserialize<List<ExpenseApplication>>(localData.Data.ToString());

            if (deserializedData != null)
            {
                var createPaymentTasks = new List<Task>();
                foreach (var expenseInfo in deserializedData)
                {
                    DateTime startdate = expenseInfo.ofm_start_date;
                    DateTime enddate = expenseInfo.ofm_end_date;
                    int expenseStatus = (int)expenseInfo.statuscode;
                    string applicationId = expenseInfo?._ofm_application_value ?? throw new InvalidDataException("application can't not be blank.");
                    int paymentFrequency = (int)expenseInfo.ofm_payment_frequency;
                    Decimal expenseAmount = expenseInfo.ofm_amount;
                   
                    if (paymentFrequency == 1) // **LUMPSUM** // check if payment frequency of expense application is lump sum or monthly.
                    {
                        createPaymentTasks.Add(CreateIrregularExpensePaymentLines(_expenseApplicationId, expenseAmount, startdate, startdate, false, applicationId, appUserService, d365WebApiService, _processParams));

                    }
                    else if (paymentFrequency == 2) // ** Monthly ** // Check if payment frequency of expense application is monthly.
                    {
                        int numberOfMonths = (enddate.Year - startdate.Year) * 12 + enddate.Month - startdate.Month + 1;
                        expenseAmount = expenseAmount / numberOfMonths;
                        for (DateTime date = startdate; date <= enddate; date = date.AddMonths(1))
                        {
                        createPaymentTasks.Add(CreateIrregularExpensePaymentLines(_expenseApplicationId, expenseAmount, date,startdate, false, applicationId, appUserService, d365WebApiService, _processParams));

                        }

                    }
                }
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

       
        private async Task<JsonObject> CreateIrregularExpensePaymentLines(string expenseId, decimal expenseAmount,DateTime startdate, DateTime firstpaymentDate, bool manualReview, string application, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var entitySetName = "ofm_payments";
            var fiscalYearData = await GetFiscalYearDataAsync();
            List<ofm_fiscal_year> fiscalYears = JsonSerializer.Deserialize<List<ofm_fiscal_year>>(fiscalYearData.Data.ToString());
            var businessclosuresdata = await GetBusinessClosuresDataAsync();
            List<DateTime> holidaysList = GetStartTimes(businessclosuresdata.Data.ToString());

            Int32 lineNumber = 1;

            Guid? fiscalYear = AssignFiscalYear(startdate, fiscalYears);


            DateTime invoiceReceivedDate = firstpaymentDate == startdate && firstpaymentDate != null ? startdate : startdate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
            DateTime invoicedate = TimeExtensions.GetCFSInvoiceDate(invoiceReceivedDate, holidaysList);
            DateTime effectiveDate = invoicedate;

            var payload = new JsonObject()
                {
                    { "ofm_invoice_line_number", lineNumber++ },
                    { "ofm_amount", expenseAmount},
                    { "ofm_payment_type", (int) ecc_payment_type.IrregularExpense },
                    { "ofm_description", " Irregular Expense payment" },
                    { "ofm_application@odata.bind",$"/ofm_applications({application})" },
                    { "ofm_invoice_date", invoicedate.ToString("yyyy-MM-dd") },
                    { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
                    { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
                    { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" },
                    { "ofm_payment_manual_review", manualReview },
                    { "statuscode", 4 }, // approved by default
                    { "ofm_regardingid_ofm_expense@odata.bind",$"/ofm_expenses({expenseId})"  },
                    { "ofm_facility@odata.bind", $"/accounts({_processParams?.Organization?.facilityId})" },
                    { "ofm_organization@odata.bind", $"/accounts({_processParams?.Organization?.organizationId})" }

                };

            var requestBody = JsonSerializer.Serialize(payload);
            var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to create payments for application with the server error {responseBody}", responseBody.CleanLog());

                return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }


        #region Cancel the unpaid payments when status changed to Inactive or Terminated.

        private async Task<JsonObject> CancelPaymentLines(Guid? paymentId, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var statement = $"ofm_payments({paymentId})";

            var payload = new JsonObject {
                { "statuscode", 7},
                { "statecode",(int) ofm_payment_statecode.Inactive }
            };

            var requestBody = JsonSerializer.Serialize(payload);

            var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

            if (!patchResponse.IsSuccessStatusCode)
            {
                var responseBody = await patchResponse.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to patch GoodStanding status on organization with the server error {responseBody}", responseBody.CleanLog());

                return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        #endregion

        private static List<DateTime> GetStartTimes(string jsonData)
        {
            var closures = JsonSerializer.Deserialize<List<BusinessClosure>>(jsonData);

            List<DateTime> startTimeList = closures.Select(closure => DateTime.Parse(closure.msdyn_starttime)).ToList();

            return startTimeList;
        }

        private static Guid? AssignFiscalYear(DateTime paymentDate, List<ofm_fiscal_year> fiscalYears)
        {
            var matchingFiscalYear = fiscalYears.FirstOrDefault(fiscalYear => paymentDate >= fiscalYear.ofm_start_date && paymentDate <= fiscalYear.ofm_end_date);

            if (matchingFiscalYear != null)
            {
                return matchingFiscalYear.ofm_fiscal_yearid;
            }
            return Guid.Empty;
        }

       

        private async Task<JsonObject> UpdatePaymentLines(Guid? paymentId, decimal? paymentAmount, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var statement = $"ofm_payments({paymentId})";

            var payload = new JsonObject {
                 { "ofm_amount", paymentAmount }

        };

            var requestBody = JsonSerializer.Serialize(payload);

            var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

            if (!patchResponse.IsSuccessStatusCode)
            {
                var responseBody = await patchResponse.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to patch GoodStanding status on organization with the server error {responseBody}", responseBody.CleanLog());

                return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }
    }
}

