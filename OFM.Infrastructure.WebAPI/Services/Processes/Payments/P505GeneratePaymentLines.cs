﻿using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
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
                          <condition attribute="ofm_application" operator="eq" uiname="APP-23000010" uitype="ofm_application" value="{E4B5E2F2-ED8E-EE11-8179-000D3A09D132}" />
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
                    application = fundingInfo._ofm_application_value.ToString();
                    Guid? organization = _processParams?.Organization.organizationId;

                    if (fundingStatus == (int)ofm_funding_StatusCode.Active)
                    {
                        var allPayments = await GetApplicationPaymentDataAsync();
                        var paymentDeserializedData = JsonSerializer.Deserialize<List<PaymentLine>>(allPayments.Data.ToString());
                        var supplementaryApplications = await GetSupplementaryApplicationDataAsync();
                        var supplementaryApplicationDeserializedData = JsonSerializer.Deserialize<List<SupplementaryApplication>>(supplementaryApplications.Data.ToString());
                        //if application payments does not exist create payment lines.
                        if (paymentDeserializedData.Count == 0)
                        {
                            createPaymentTasks.Add(CreatePaymentLines(facility, organization, startdate, enddate, fundingid, application, appUserService, d365WebApiService, _processParams));
                        }
                        //Check if supplementary application exists.
                        if (supplementaryApplicationDeserializedData.Count == 0)
                        {
                            // Filter entries with ofm_allowance_type as "supportprogramming" or "indigenous"
                            var saSupportProgrammingOrIndigenous = supplementaryApplicationDeserializedData
                                .Where(entry => entry.ofm_allowance_type == 1 || entry.ofm_allowance_type == 2)
                                .ToList();
                            var transportationApplications = supplementaryApplicationDeserializedData
                                .Where(entry => entry.ofm_allowance_type == 3)
                                .ToList();
                            if (saSupportProgrammingOrIndigenous.Any())
                            {
                                
                                foreach(var supplementaryApp in saSupportProgrammingOrIndigenous)
                                {
                                    decimal? fundingAmount = supplementaryApp.ofm_funding_amount;
                                    int allowanceType = supplementaryApp.ofm_allowance_type;
                                    createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facility, organization, startdate, enddate, fundingid, application, allowanceType == 1 ? (int)ecc_payment_type.SupportNeedsFunding : (int)ecc_payment_type.IndigenousProgramming, fundingAmount, appUserService, d365WebApiService, processParams));
                                }
                                

                            }
                           // Deserialize transportationApplications JSON array
                            var transportationApps = JsonSerializer.Deserialize<List<SupplementaryApplication>>(transportationApplications.ToString());
                            if (transportationApps.Any()) {
                                var firstTransportationApp = transportationApps[0];
                                //Sum the amounts of all transportation applications
                                decimal? totalAmount = transportationApps.Sum(app => app.ofm_funding_amount);

                                // Calculate number of months between start date and end date
                                int numberOfMonths = (enddate.Year - startdate.Year) * 12 + enddate.Month - startdate.Month + 1;

                                //Calculate the funding amount
                                decimal? fundingAmountPerPayment = totalAmount / numberOfMonths;


                                for (DateTime paymentdate = firstTransportationApp.ofm_start_date; paymentdate <= firstTransportationApp.ofm_end_date; paymentdate = paymentdate.AddMonths(1))
                                {
                                    createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facility, organization, paymentdate, enddate, fundingid, application, (int)ecc_payment_type.Transportation, fundingAmountPerPayment, appUserService, d365WebApiService, processParams));
                                }
                            }

                        }
                    }
                    else if (fundingStatus == (int)ofm_funding_StatusCode.Expired ||
                            fundingStatus == (int)ofm_funding_StatusCode.Terminated ||
                            fundingStatus == (int)ofm_funding_StatusCode.Cancelled)
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

        private async Task<JsonObject> CreatePaymentLines(string Facility, Guid? Organization, DateTime startdate, DateTime enddate, string fundingId, string application, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
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
                DateTime invoicedate = TimeExtensions.GetCFSInvoiceDate(paymentdate.AddDays(-1), holidaysList);
                DateTime invoiceReceivedDate = invoicedate.AddDays(-4);
                DateTime effectiveDate = TimeExtensions.GetCFSEffectiveDate(invoicedate, holidaysList);

                var payload = new JsonObject()
                {
                    { "ofm_invoice_line_number", lineNumber++ },
                    { "ofm_amount", decimal.Parse(_processParams?.Funding?.ofm_monthly_province_base_funding_y1)},
                    { "ofm_payment_type", (int) ecc_payment_type.CORE },
                    { "ofm_facility@odata.bind", $"/accounts({Facility})" },
                    { "ofm_organization@odata.bind", $"/accounts({_processParams?.Organization?.organizationId})" },
                    { "ofm_funding@odata.bind", $"/ofm_fundings({_processParams?.Funding?.FundingId})" },
                    { "ofm_description", "payment" },
                    { "ofm_application@odata.bind",$"/ofm_applications({application})" },
                    { "ofm_invoice_date", invoicedate },
                    { "ofm_invoice_received_date", invoiceReceivedDate },
                    { "ofm_effective_date", effectiveDate },
                    { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" }
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
        

        private async Task<JsonObject> CreateSupplementaryApplicationPayment(string Facility, Guid? Organization, DateTime startdate, DateTime enddate, string fundingId, string application, int paymentType, decimal? fundingAmount, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var entitySetName = "ofm_payments";
            var fiscalYearData = await GetFiscalYearDataAsync();
            List<FiscalYear> fiscalYears = JsonSerializer.Deserialize<List<FiscalYear>>(fiscalYearData.Data.ToString());
            var businessclosuresdata = await GetBusinessClosuresDataAsync();
            List<DateTime> holidaysList = GetStartTimes(businessclosuresdata.Data.ToString());

            Guid? fiscalYear = AssignFiscalYear(startdate, fiscalYears);
            DateTime invoicedate = TimeExtensions.GetCFSInvoiceDate(startdate.AddDays(-1), holidaysList);
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
        { "ofm_description", "payment" },
        { "ofm_application@odata.bind",$"/ofm_applications({application})" },
        { "ofm_invoice_date", invoicedate },
        { "ofm_invoice_received_date", invoiceReceivedDate },
        { "ofm_effective_date", effectiveDate },
        { "ofm_fiscal_year@odata.bind",$"/ofm_fiscal_years({fiscalYear})" }
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

    }
}

