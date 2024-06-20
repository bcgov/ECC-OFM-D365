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
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments
{
    public class P505GeneratePaymentLinesProvider : ID365ProcessProvider
    {
        private string application;
        private string FYYear;
        private string saApplication;
        private readonly ID365AppUserService _appUserService;
        private readonly ID365WebApiService _d365webapiservice;
        private readonly ILogger _logger;
        private readonly TimeProvider _timeProvider;
        private ProcessParameter? _processParams;


        public P505GeneratePaymentLinesProvider(IOptionsSnapshot<ExternalServices> ApiKeyBCRegistry, IOptionsSnapshot<D365AuthSettings> d365AuthSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
        {

            _appUserService = appUserService;
            _d365webapiservice = d365WebApiService;
            _logger = loggerFactory.CreateLogger(LogCategory.Process);
            _timeProvider = timeProvider;
        }

        public Int16 ProcessId => Setup.Process.Payments.GeneratePaymentLinesId;
        public string ProcessName => Setup.Process.Payments.GeneratePaymentLinesName;

        public string ApplicationPaymentsRequestUri
        {
            get
            {
                var fetchXml = $$"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                                              <entity name="ofm_payment">
                                                <attribute name="ofm_paymentid" />
                                                <attribute name="ofm_name" />
                                                <attribute name="createdon" />
                                                <attribute name="statuscode" />
                                                <attribute name="ofm_funding" />
                                                <attribute name="ofm_payment_type" />
                                                <attribute name="ofm_effective_date" />
                                                <attribute name="ofm_amount" />
                                                <attribute name="ofm_supplementary" />
                                                <order attribute="ofm_name" descending="false" />
                                                <filter type="and">
                                                  <condition attribute="ofm_application" operator="eq"  value="{{application}}" />
                                                 
                                                </filter>
                                              </entity>
                                            </fetch>
                    """;

                var requestUri = $"""
                         ofm_payments?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }

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
        //Retrieve funding Information.
        public string FundingRequestURI
        {
            get
            {
                var fetchXml = $$"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_funding">
                        <attribute name="ofm_fundingid" />
                        <attribute name="ofm_funding_number" />
                        <attribute name="createdon" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_start_date" />
                        <attribute name="ofm_facility" />
                        <attribute name="ofm_monthly_province_base_funding_y1" />
                        <attribute name="ofm_application" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_retroactive_payment_date" />
                        <attribute name="ofm_retroactive_payment" />
                        <order attribute="ofm_funding_number" descending="false" />
                        <filter type="and">
                          <condition attribute="ofm_fundingid" operator="eq"  value="{{_processParams?.Funding?.FundingId}}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_fundings?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }
        //Retrieve Supplementary Applications that are approved.

        public string SupplementaryApplicationsRequestURI
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
                          <condition attribute="ofm_application" operator="eq"  value="{{application}}" />
                          <condition attribute="statuscode" operator="eq" value="6" />
                          <condition attribute="ofm_renewal_term" operator="eq" value="{{FYYear}}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_allowances?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }
        public async Task<ProcessData> GetApplicationPaymentDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, nameof(GetApplicationPaymentDataAsync));

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, ApplicationPaymentsRequestUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query payment record information with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No payment records found with query {requestUri}", ApplicationPaymentsRequestUri.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
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

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, FundingRequestURI);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding records found with query {requestUri}", FundingRequestURI.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }
        public async Task<ProcessData> GetSupplementaryApplicationDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "GetSupplementaryApplicationDataAsync");

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, SupplementaryApplicationsRequestURI);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding records found with query {requestUri}", SupplementaryApplicationsRequestURI.CleanLog());
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
            //Get funding Info
            var localData = await GetDataAsync();

            var deserializedData = JsonSerializer.Deserialize<List<Funding>>(localData.Data.ToString());

            if (deserializedData != null)
            {
                var createPaymentTasks = new List<Task>();
                foreach (var fundingInfo in deserializedData)
                {
                    string fundingid = fundingInfo.ofm_fundingid.Value.ToString();
                    DateTime startdate = fundingInfo.ofm_start_date.Value;
                    DateTime enddate = fundingInfo.ofm_end_date.Value;
                    int fundingStatus = fundingInfo.statuscode.Value;
                    string facility = fundingInfo._ofm_facility_value.ToString();
                    application = _processParams?.Application?.applicationId.ToString();
                    FYYear = _processParams?.SupplementaryApplication?.fyYear != null ? _processParams?.SupplementaryApplication?.fyYear.ToString() : null;
                    decimal monthlyFundingAmount = decimal.Parse(_processParams?.Funding?.ofm_monthly_province_base_funding_y1);
                    DateTime retroActivePaymentDate;
                    int retroActiveCreditOrDebitMonths = 0;



                    if (fundingStatus == (int)ofm_funding_StatusCode.Active)
                    {
                        var allPayments = await GetApplicationPaymentDataAsync();
                        var paymentDeserializedData = JsonSerializer.Deserialize<List<PaymentLine>>(allPayments.Data.ToString());

                        //if application payments does not exist create payment lines for initial funding.
                        if (paymentDeserializedData.Count == 0)
                        {
                            createPaymentTasks.Add(CreatePaymentLines(facility, monthlyFundingAmount, startdate, enddate, false, application, appUserService, d365WebApiService, _processParams));
                        }
                        //Check if it is MOD
                        else if (processParams?.Funding?.isMod == true)
                        {
                            int paymentFrequency = fundingInfo.ofm_retroactive_payment.Value;
                            decimal previousMonthlyFundingAmount = decimal.Parse(processParams?.Funding?.previous_monthly_province_base_funding_y1);
                            if (fundingInfo.ofm_retroactive_payment_date.HasValue)
                            {
                                retroActivePaymentDate = fundingInfo.ofm_retroactive_payment_date.Value;
                                retroActiveCreditOrDebitMonths = (startdate.Year - retroActivePaymentDate.Year) * 12 + startdate.Month - retroActivePaymentDate.Month;
                            }
                            
                            int differenceInMonths = (enddate.Year - startdate.Year) * 12 + (enddate.Month - startdate.Month);
                            bool retroActiveCreditOrDebitYesOrNo = false;
                            // Check if the difference is greater than 0 months
                            if (retroActiveCreditOrDebitMonths > 0)
                            {
                                retroActiveCreditOrDebitYesOrNo = true;
                            }
                            //To find adjusted amount.
                            decimal modIncreaseMonthlyAmount = monthlyFundingAmount - previousMonthlyFundingAmount;//15000
                            if (retroActiveCreditOrDebitYesOrNo == true)
                            {
                                decimal retroActiveCreditOrDebitLumpSumAmount = modIncreaseMonthlyAmount * retroActiveCreditOrDebitMonths;
                                decimal retroActiveCreditOrDebitMonthlyAmount = retroActiveCreditOrDebitLumpSumAmount / differenceInMonths;
                                // if it is positive or negative.
                                if (retroActiveCreditOrDebitLumpSumAmount != 0)
                                {
                                    //lumpsum
                                    if (paymentFrequency == 2)
                                    {
                                        //create lumpsum payment for starting month only.
                                        createPaymentTasks.Add(CreatePaymentLines(facility, retroActiveCreditOrDebitLumpSumAmount, startdate, startdate, true, application, appUserService, d365WebApiService, _processParams));

                                    }
                                    //monthly
                                    else if (paymentFrequency == 3)
                                    {
                                        //create monthly retroactive credit.
                                        createPaymentTasks.Add(CreatePaymentLines(facility, retroActiveCreditOrDebitMonthlyAmount, startdate, enddate, true,application, appUserService, d365WebApiService, _processParams));
                                    }

                                    //create payment lines for the increase or decrease from mod start date to end date.
                                    createPaymentTasks.Add(CreatePaymentLines(facility, modIncreaseMonthlyAmount, startdate, enddate, false, application, appUserService, d365WebApiService, _processParams));
                                 
                                }

                            }
                            else
                            {
                                //create payment lines for the increase or decrease from mod start date to end date.
                                createPaymentTasks.Add(CreatePaymentLines(facility, modIncreaseMonthlyAmount, startdate, enddate, false, application, appUserService, d365WebApiService, _processParams));
                                
                            }
                        }
                        // PAYMENT CREATION FOR SUPPORT or INDIGENIOUS PROGRAMMING APPS
                        // Checking if the trigger is for supplementary application approval based on FY year value.
                        if (FYYear != null)
                        {

                            //Check if supplementary application exists.
                            var supplementaryApplications = await GetSupplementaryApplicationDataAsync();
                            var supplementaryApplicationDeserializedData = JsonSerializer.Deserialize<List<SupplementaryApplication>>(supplementaryApplications.Data.ToString());
                            if (supplementaryApplicationDeserializedData.Count > 0)
                            {
                                // Filter entries with ofm_allowance_type as "supportneedservice" or "indigenous"
                                var saSupportOrIndigenous = supplementaryApplicationDeserializedData
                                    .Where(entry => entry.ofm_allowance_type == 1 || entry.ofm_allowance_type == 2)
                                    .ToList();

                                if (saSupportOrIndigenous.Any())
                                {

                                    foreach (var supplementaryApp in saSupportOrIndigenous)
                                    {
                                        var saStartDate = supplementaryApp.ofm_start_date;
                                        var saEndDate = supplementaryApp.ofm_end_date;
                                        //Check if payments of this allowance type created.
                                        List<PaymentLine> saSupportOrIndigenousPayments = paymentDeserializedData
                                        .Where(r => r._ofm_supplementary_value == supplementaryApp.ofm_allowanceid).ToList();
                                        //Check if payment record already exist for this supplementary app.
                                        if (saSupportOrIndigenousPayments.Count == 0)
                                        {
                                            decimal? fundingAmount = supplementaryApp.ofm_funding_amount;
                                            int allowanceType = supplementaryApp.ofm_allowance_type;
                                            createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facility, saStartDate, saStartDate, false, fundingid, application, supplementaryApp.ofm_allowanceid.ToString(), allowanceType == 1 ? (int)ecc_payment_type.SupportNeedsFunding : (int)ecc_payment_type.IndigenousProgramming, fundingAmount, appUserService, d365WebApiService, processParams));
                                        }

                                    }


                                }
                                //payment creation for TRANSPORTATION APPS
                                var transportationApplications = supplementaryApplicationDeserializedData
                                    .Where(entry => entry.ofm_allowance_type == 3)
                                    .ToList();

                                //Check if transportation application exists.
                                if (transportationApplications.Any())
                                {
                                    var minStartDate = transportationApplications.Min(app => app.ofm_start_date);
                                    var maxStartDate = transportationApplications.Max(app => app.ofm_start_date);
                                    var maxEndDate = transportationApplications.Max(app => app.ofm_end_date);
                                    // Calculate number of months between start date and end date
                                    int numberOfMonths = (maxEndDate.Year - minStartDate.Year) * 12 + maxEndDate.Month - minStartDate.Month + 1;
                                    int midYearAppMonths = (maxEndDate.Year - maxStartDate.Year) * 12 + maxEndDate.Month - maxStartDate.Month + 1;
                                    int retroActiveMonths = numberOfMonths - midYearAppMonths;
                                    // Calculate total monthly payment
                                    decimal? totalMonthlyPayment = 0;
                                    foreach (var transportationApp in transportationApplications)
                                    {
                                        //Check if payments exists for this app.
                                        List<PaymentLine> saTransportationPayments = paymentDeserializedData
                                            .Where(r =>
                                              (int)r.ofm_payment_type == (int)ecc_payment_type.Transportation && r._ofm_supplementary_value == transportationApp.ofm_allowanceid).ToList();
                                        //if no payments exists for any transportationa application, then create monthly payment lines.
                                        if (saTransportationPayments.Count == 0)
                                        {
                                            //create payment lines for mid year transportation applications.
                                            if( transportationApp.ofm_start_date > minStartDate ) 
                                            {
                                                totalMonthlyPayment = transportationApp.ofm_funding_amount / midYearAppMonths;
                                                decimal PaymentAmountPerMonth = Math.Round(totalMonthlyPayment.Value, 2);
                                                for (DateTime date = maxStartDate; date <= maxEndDate; date = date.AddMonths(1))
                                                {
                                                    //create payment for transportation.
                                                    createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facility, date, maxEndDate, false, fundingid, application, transportationApp.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, PaymentAmountPerMonth, appUserService, d365WebApiService, processParams));

                                                }
                                                if (retroActiveMonths > 0)
                                                {
                                                    decimal retroActiveCreditAmount = PaymentAmountPerMonth * retroActiveMonths;
                                                    //create payment with type transportation and payment amount as retrospective amount.
                                                    createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facility, maxStartDate, maxStartDate, true, fundingid, application,transportationApp.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, retroActiveCreditAmount, appUserService, d365WebApiService, processParams));
                                                }
                                            }
                                            // create payment lines for standard submission transportation applications.
                                            else
                                            {
                                                totalMonthlyPayment = transportationApp.ofm_funding_amount / numberOfMonths;
                                                decimal PaymentAmountPerMonth = Math.Round(totalMonthlyPayment.Value, 2);
                                                for (DateTime date = minStartDate; date <= maxEndDate; date = date.AddMonths(1))
                                                {
                                                    //create payment for transportation.
                                                    createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facility, date, maxEndDate, false, fundingid, application, transportationApp.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, PaymentAmountPerMonth, appUserService, d365WebApiService, processParams));

                                                }
                                            }
                                            
                                        }
                                    }

                                }

                            }
                        }

                    }
                    else if (fundingStatus == (int)ofm_funding_StatusCode.Terminated || fundingStatus == (int)ofm_funding_StatusCode.Cancelled || fundingStatus == (int)ofm_funding_StatusCode.Expired)
                    {
                        var allPayments = await GetApplicationPaymentDataAsync();

                        if (allPayments != null)
                        {
                            List<PaymentLine> paymentDeserializedData = JsonSerializer.Deserialize<List<PaymentLine>>(allPayments.Data.ToString());

                            // Filter records where StatusCode is not equal to 2 or 7
                            List<PaymentLine> notPaidPayments = paymentDeserializedData.Where(r => r.statuscode != (int)ofm_payment_StatusCode.Paid || r.statuscode != (int)ofm_payment_StatusCode.Cancelled).ToList();
                            if (notPaidPayments != null)
                            {
                                foreach (var payment in notPaidPayments)
                                {
                                    createPaymentTasks.Add(CancelPaymentLines(payment.Id, appUserService, d365WebApiService, _processParams));
                                }
                            }
                        }
                    }
                }

                await Task.WhenAll(createPaymentTasks);
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> CreatePaymentLines(string Facility, decimal fundingAmount, DateTime startdate, DateTime enddate, bool manualReview,  string application, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var entitySetName = "ofm_payments";
            var fiscalYearData = await GetFiscalYearDataAsync();
            List<FiscalYear> fiscalYears = JsonSerializer.Deserialize<List<FiscalYear>>(fiscalYearData.Data.ToString());
            var businessclosuresdata = await GetBusinessClosuresDataAsync();
            List<DateTime> holidaysList = GetStartTimes(businessclosuresdata.Data.ToString());

            Int32 lineNumber = 1;
            //From start date to end date create payments
            for (DateTime paymentdate = startdate; paymentdate <= enddate; paymentdate = paymentdate.AddMonths(1))
            {
                Guid? fiscalYear = AssignFiscalYear(paymentdate, fiscalYears);
                DateTime invoicedate = TimeExtensions.GetCFSInvoiceDate(paymentdate, holidaysList);
                DateTime invoiceReceivedDate = invoicedate.AddDays(-4);
                DateTime effectiveDate = TimeExtensions.GetCFSEffectiveDate(invoicedate, holidaysList);

                var payload = new JsonObject()
                {
                    { "ofm_invoice_line_number", lineNumber++ },
                    { "ofm_amount", fundingAmount},
                    { "ofm_payment_type", (int) ecc_payment_type.CORE },
                    { "ofm_facility@odata.bind", $"/accounts({Facility})" },
                    { "ofm_organization@odata.bind", $"/accounts({_processParams?.Organization?.organizationId})" },
                    { "ofm_funding@odata.bind", $"/ofm_fundings({_processParams?.Funding?.FundingId})" },
                    { "ofm_description", "payment" },
                    { "ofm_application@odata.bind",$"/ofm_applications({application})" },
                    { "ofm_invoice_date", invoicedate.ToString("yyyy-MM-dd") },
                    { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
                    { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
                    { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" },
                    { "ofm_payment_manual_review", manualReview }
                };

                var requestBody = JsonSerializer.Serialize(payload);
                var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody);
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to create payments for application with the server error {responseBody}", responseBody.CleanLog());

                    return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
                }
            }
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        #region Cancell the not paid payments when status changed to Inactive or terminated.
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

        private static Guid? AssignFiscalYear(DateTime paymentDate, List<FiscalYear> fiscalYears)
        {
            var matchingFiscalYear = fiscalYears.FirstOrDefault(fiscalYear => paymentDate >= fiscalYear.ofm_start_date && paymentDate <= fiscalYear.ofm_end_date);

            if (matchingFiscalYear != null)
            {
                return matchingFiscalYear.ofm_fiscal_yearid;
            }
            return Guid.Empty;
        }


        private async Task<JsonObject> CreateSupplementaryApplicationPayment(string Facility, DateTime startdate, DateTime enddate, bool manualReview, string fundingId, string application,string saApplication, int paymentType, decimal? fundingAmount, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var entitySetName = "ofm_payments";
            var fiscalYearData = await GetFiscalYearDataAsync();
            List<FiscalYear> fiscalYears = JsonSerializer.Deserialize<List<FiscalYear>>(fiscalYearData.Data.ToString());
            var businessclosuresdata = await GetBusinessClosuresDataAsync();
            List<DateTime> holidaysList = GetStartTimes(businessclosuresdata.Data.ToString());

            Guid? fiscalYear = AssignFiscalYear(startdate, fiscalYears);
            DateTime invoicedate = TimeExtensions.GetCFSInvoiceDate(startdate, holidaysList);
            DateTime invoiceReceivedDate = invoicedate.AddDays(-4);
            DateTime effectiveDate = TimeExtensions.GetCFSEffectiveDate(invoicedate, holidaysList);

            var payload = new JsonObject()
    {
        { "ofm_invoice_line_number", 1 },
        { "ofm_amount", fundingAmount },
        { "ofm_payment_type", paymentType },
        { "ofm_facility@odata.bind", $"/accounts({Facility})" },
        { "ofm_organization@odata.bind", $"/accounts({_processParams?.Organization?.organizationId})" },
        { "ofm_funding@odata.bind", $"/ofm_fundings({_processParams?.Funding?.FundingId})" },
        { "ofm_description", "supplementary application payment" },
        { "ofm_application@odata.bind",$"/ofm_applications({application})" },
        { "ofm_invoice_date", invoicedate.ToString("yyyy-MM-dd") },
        { "ofm_invoice_received_date", invoiceReceivedDate.ToString("yyyy-MM-dd")},
        { "ofm_effective_date", effectiveDate.ToString("yyyy-MM-dd")},
        { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" },
        { "ofm_payment_manual_review", manualReview },
        { "ofm_supplementary@odata.bind", $"/ofm_allowances({saApplication})" },
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

