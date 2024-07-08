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

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments
{
    public class P505GeneratePaymentLinesProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
    {
        //private string saApplication;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private ProcessParameter? _processParams;
        private string _applicationId = string.Empty;

        public Int16 ProcessId => Setup.Process.Payments.GeneratePaymentLinesId;
        public string ProcessName => Setup.Process.Payments.GeneratePaymentLinesName;

        public string ApplicationPaymentsRequestUri
        {
            get
            {
                // For reference only
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
                                                  <condition attribute="ofm_application" operator="eq"  value="{{_applicationId}}" />                                                 
                                                </filter>
                                              </entity>
                                            </fetch>
                    """;

                var requestUri = $"""
                         ofm_payments?$select=ofm_paymentid,ofm_name,createdon,statuscode,_ofm_funding_value,ofm_payment_type,ofm_effective_date,ofm_amount,_ofm_supplementary_value&$filter=(_ofm_application_value eq {_applicationId})&$orderby=ofm_name asc
                         """;

                return requestUri;
            }
        }

        public string FiscalYearRequestUri
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
                        <attribute name="ofm_retroactive_payment_frequency" />
                        <order attribute="ofm_funding_number" descending="false" />
                        <filter type="and">
                          <condition attribute="ofm_fundingid" operator="eq"  value="{{_processParams?.Funding?.FundingId}}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_fundings?$select=ofm_fundingid,ofm_funding_number,createdon,statecode,statuscode,ofm_start_date,_ofm_facility_value,ofm_monthly_province_base_funding_y1,_ofm_application_value,ofm_end_date,ofm_retroactive_payment_date,ofm_retroactive_payment_frequency&$expand=ofm_facility($select=accountnumber,name,accountid),ofm_application($select=ofm_application,ofm_applicationid)&$filter=(ofm_fundingid eq {_processParams?.Funding?.FundingId}) and (ofm_facility/accountid ne null) and (ofm_application/ofm_applicationid ne null)&$orderby=ofm_funding_number asc
                         """;

                return requestUri;
            }
        }

        public string ApprovedSupplementaryApplicationsRequestURI
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
                          <condition attribute="ofm_application" operator="eq"  value="{{_applicationId}}" />
                          <condition attribute="statuscode" operator="eq" value="{{(int)ofm_allowance_StatusCode.Approved}}" /> 
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                        ofm_allowances?$select=ofm_allowanceid,ofm_allowance_number,createdon,ofm_allowance_type,ofm_end_date,ofm_start_date,statuscode,ofm_funding_amount,ofm_renewal_term&$filter=(_ofm_application_value eq {_applicationId} and statuscode eq {(int)ofm_allowance_StatusCode.Approved})&$orderby=ofm_allowance_number asc
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

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, ApprovedSupplementaryApplicationsRequestURI);

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
                    _logger.LogInformation(CustomLogEvent.Process, "No Funding records found with query {requestUri}", ApprovedSupplementaryApplicationsRequestURI.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            _processParams = processParams;
            _applicationId = _processParams?.Application?.applicationId.ToString();
            var startTime = _timeProvider.GetTimestamp();

            var fundingData = await GetDataAsync();
            var deserializedFundingData = JsonSerializer.Deserialize<List<Funding>>(fundingData.Data.ToString());

            if (deserializedFundingData != null)
            {
                var createPaymentTasks = new List<Task>();
                foreach (var fundingInfo in deserializedFundingData)
                {
                    string fundingId = fundingInfo.ofm_fundingid.Value.ToString();
                    DateTime startdate = fundingInfo.ofm_start_date.Value;
                    DateTime enddate = fundingInfo.ofm_end_date.Value;
                    int fundingStatus = (int)(fundingInfo.statuscode ?? throw new InvalidDataException("Funding Status can't not be blank."));
                    string facilityId = fundingInfo?.ofm_facility?.accountid.ToString() ?? throw new InvalidDataException("Funding Status can't not be blank.");
                    decimal monthlyFundingAmount = decimal.Parse(_processParams?.Funding?.MonthlyBaseFundingAmount);
                    DateTime retroActivePaymentDate;
                    int retroActiveCreditOrDebitMonths = 0;
                    var allPayments = await GetApplicationPaymentDataAsync();
                    var paymentDeserializedData = JsonSerializer.Deserialize<List<D365PaymentLine>>(allPayments.Data.ToString());
                    var supplementaryApplications = await GetSupplementaryApplicationDataAsync();
                    var supplementaryApplicationDeserializedData = JsonSerializer.Deserialize<List<SupplementaryApplication>>(supplementaryApplications.Data.ToString());

                    if (fundingStatus == (int)ofm_funding_StatusCode.Active && processParams.SupplementaryApplication.allowanceId == null)
                    {
                        //if application payments does not exist create payment lines for initial funding.
                        if (paymentDeserializedData.Count == 0)
                        {
                            createPaymentTasks.Add(CreatePaymentLines(facilityId, monthlyFundingAmount, startdate, enddate, false, _applicationId, appUserService, d365WebApiService, _processParams));
                        }
                        //Check if it is MOD
                        else if (processParams?.Funding?.IsMod == true)
                        {
                            int paymentFrequency = (int)(fundingInfo.ofm_retroactive_payment_frequency ?? 0); //  fundingInfo.ofm_retroactive_payment_frequency.HasValue ? fundingInfo.ofm_retroactive_payment_frequency.Value : 0;
                            decimal previousMonthlyFundingAmount = decimal.Parse(processParams?.Funding?.PreviousMonthlyBaseFundingAmount);
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
                                        createPaymentTasks.Add(CreatePaymentLines(facilityId, retroActiveCreditOrDebitLumpSumAmount, startdate, startdate, true, _applicationId, appUserService, d365WebApiService, _processParams));

                                    }
                                    //monthly
                                    else if (paymentFrequency == 3)
                                    {
                                        //create monthly retroactive credit.
                                        createPaymentTasks.Add(CreatePaymentLines(facilityId, retroActiveCreditOrDebitMonthlyAmount, startdate, enddate, true, _applicationId, appUserService, d365WebApiService, _processParams));
                                    }

                                    //create payment lines for the increase or decrease from mod start date to end date.
                                    createPaymentTasks.Add(CreatePaymentLines(facilityId, modIncreaseMonthlyAmount, startdate, enddate, false, _applicationId, appUserService, d365WebApiService, _processParams));

                                }
                            }
                            else
                            {
                                //create payment lines for the increase or decrease from mod start date to end date.
                                createPaymentTasks.Add(CreatePaymentLines(facilityId, modIncreaseMonthlyAmount, startdate, enddate, false, _applicationId, appUserService, d365WebApiService, _processParams));

                            }
                        }
                        // PAYMENT CREATION FOR SUPPORT or INDIGENIOUS PROGRAMMING APPS
                        // Checking if the trigger is for supplementary application approval based on FY year value.

                        //Check if supplementary application exists.

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
                                    
                                    List<D365PaymentLine> saSupplementaryPayments = paymentDeserializedData
                                    .Where(payment => payment.ofm_regardingid?.Id != null)
                                    .ToList();

                                    List<D365PaymentLine> saSupportOrIndigenousPayments;

                                    if (saSupplementaryPayments.Count != 0)
                                    {
                                        saSupportOrIndigenousPayments = saSupplementaryPayments
                                            .Where(payment => payment.ofm_regardingid.Id == supplementaryApp.ofm_allowanceid)
                                            .ToList();
                                    }
                                    else
                                    {
                                        saSupportOrIndigenousPayments = new List<D365PaymentLine>(); // Assign an empty list or handle it accordingly
                                    }

                                    //Check if payment record already exist for this supplementary app.
                                    if (saSupportOrIndigenousPayments.Count == 0)
                                    {
                                        decimal? fundingAmount = supplementaryApp.ofm_funding_amount;
                                        int allowanceType = supplementaryApp.ofm_allowance_type;

                                        createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facilityId, saStartDate, saStartDate, false, fundingId, _applicationId, supplementaryApp.ofm_allowanceid.ToString(), allowanceType == 1 ? (int)ecc_payment_type.SupportNeedsFunding : (int)ecc_payment_type.IndigenousProgramming, fundingAmount, appUserService, d365WebApiService, processParams));
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
                                    List<D365PaymentLine> saTransportationPayments = paymentDeserializedData
                                        .Where(payment =>
                                                payment.ofm_payment_type == ecc_payment_type.Transportation && payment.ofm_regardingid.Id == transportationApp.ofm_allowanceid).ToList();
                                    //if no payments exists for any transportationa application, then create monthly payment lines.
                                    if (saTransportationPayments.Count == 0)
                                    {
                                        //create payment lines for mid year transportation applications.
                                        if (transportationApp.ofm_start_date > minStartDate)
                                        {
                                            totalMonthlyPayment = transportationApp.ofm_funding_amount / midYearAppMonths;
                                            decimal PaymentAmountPerMonth = Math.Round(totalMonthlyPayment.Value, 2);
                                            for (DateTime date = maxStartDate; date <= maxEndDate; date = date.AddMonths(1))
                                            {
                                                //create payment for transportation.
                                                createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facilityId, date, maxStartDate, false, fundingId, _applicationId, transportationApp.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, PaymentAmountPerMonth, appUserService, d365WebApiService, processParams));

                                            }
                                            if (retroActiveMonths > 0)
                                            {
                                                decimal retroActiveCreditAmount = PaymentAmountPerMonth * retroActiveMonths;
                                                //create payment with type transportation and payment amount as retrospective amount.
                                                createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facilityId, maxStartDate, maxStartDate, true, fundingId, _applicationId, transportationApp.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, retroActiveCreditAmount, appUserService, d365WebApiService, processParams));
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
                                                createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facilityId, date, minStartDate, false, fundingId, _applicationId, transportationApp.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, PaymentAmountPerMonth, appUserService, d365WebApiService, processParams));

                                            }
                                        }

                                    }
                                }

                            }

                        }
                    }

                    // When a supplementary application is approved.
                    if (processParams.SupplementaryApplication.allowanceId != null && fundingStatus == (int)ofm_funding_StatusCode.Active)
                    {
                        var saAppApproved = supplementaryApplicationDeserializedData
                                            .FirstOrDefault(entry => entry.ofm_allowanceid == processParams.SupplementaryApplication.allowanceId);

                        var saStartDate = saAppApproved.ofm_start_date;
                        var saEndDate = saAppApproved.ofm_end_date;
                        //Check if payments of this allowance type created.
                      

                        List<D365PaymentLine> saSupplementaryPayments = paymentDeserializedData
                        .Where(payment => payment.ofm_regardingid?.Id != null)
                        .ToList();

                        List<D365PaymentLine> saSupportOrIndigenousPayments;

                        if (saSupplementaryPayments.Count != 0)
                        {
                            saSupportOrIndigenousPayments = saSupplementaryPayments
                                .Where(payment => payment.ofm_regardingid.Id == saAppApproved.ofm_allowanceid)
                                .ToList();
                        }
                        else
                        {
                            saSupportOrIndigenousPayments = new List<D365PaymentLine>(); // Assign an empty list or handle it accordingly
                        }
                        //Check if payment record already exist for this supplementary app.
                        if (saSupportOrIndigenousPayments.Count == 0)
                        {
                            if (saAppApproved.ofm_allowance_type == (int)ecc_allowance_type.SupportNeedsProgramming || saAppApproved.ofm_allowance_type == (int)ecc_allowance_type.IndigenousProgramming)
                            {
                                decimal? fundingAmount = saAppApproved.ofm_funding_amount;
                                int allowanceType = saAppApproved.ofm_allowance_type;
                                createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facilityId, saStartDate, saStartDate, false, fundingId, _applicationId, saAppApproved.ofm_allowanceid.ToString(), allowanceType == 1 ? (int)ecc_payment_type.SupportNeedsFunding : (int)ecc_payment_type.IndigenousProgramming, fundingAmount, appUserService, d365WebApiService, processParams));
                            }
                            else if (saAppApproved.ofm_allowance_type == (int)ecc_allowance_type.Transportation)
                            {
                                //payment creation for TRANSPORTATION APPS
                                var transportationApplications = supplementaryApplicationDeserializedData
                                                                    .Where(entry => entry.ofm_allowance_type == 3)
                                                                    .ToList();
                                var minStartDate = transportationApplications.Min(app => app.ofm_start_date);
                                var maxStartDate = transportationApplications.Max(app => app.ofm_start_date);
                                var maxEndDate = transportationApplications.Max(app => app.ofm_end_date);
                                // Calculate number of months between start date and end date
                                int numberOfMonths = (maxEndDate.Year - minStartDate.Year) * 12 + maxEndDate.Month - minStartDate.Month + 1;
                                int midYearAppMonths = (maxEndDate.Year - maxStartDate.Year) * 12 + maxEndDate.Month - maxStartDate.Month + 1;
                                int retroActiveMonths = numberOfMonths - midYearAppMonths;
                                decimal? totalMonthlyPayment = 0;

                                //create payment lines for mid year transportation applications.
                                if (saAppApproved.ofm_start_date > minStartDate)
                                {

                                    totalMonthlyPayment = saAppApproved.ofm_funding_amount / midYearAppMonths;
                                    decimal PaymentAmountPerMonth = Math.Round(totalMonthlyPayment.Value, 2);
                                    for (DateTime date = maxStartDate; date <= maxEndDate; date = date.AddMonths(1))
                                    {
                                        //create payment for transportation.
                                        createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facilityId, date, maxStartDate, false, fundingId, _applicationId, saAppApproved.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, PaymentAmountPerMonth, appUserService, d365WebApiService, processParams));

                                    }
                                    if (retroActiveMonths > 0)
                                    {
                                        decimal retroActiveCreditAmount = PaymentAmountPerMonth * retroActiveMonths;
                                        //create payment with type transportation and payment amount as retrospective amount.
                                        createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facilityId, maxStartDate, maxStartDate, true, fundingId, _applicationId, saAppApproved.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, retroActiveCreditAmount, appUserService, d365WebApiService, processParams));
                                    }
                                }
                                // create payment lines for standard submission transportation applications.
                                else
                                {
                                    totalMonthlyPayment = saAppApproved.ofm_funding_amount / numberOfMonths;
                                    decimal PaymentAmountPerMonth = Math.Round(totalMonthlyPayment.Value, 2);
                                    for (DateTime date = minStartDate; date <= maxEndDate; date = date.AddMonths(1))
                                    {
                                        //create payment for transportation.
                                        createPaymentTasks.Add(CreateSupplementaryApplicationPayment(facilityId, date, minStartDate, false, fundingId, _applicationId, saAppApproved.ofm_allowanceid.ToString(), (int)ecc_payment_type.Transportation, PaymentAmountPerMonth, appUserService, d365WebApiService, processParams));

                                    }
                                }

                            }

                        }
                    }
                    //For cancellation or termination of funding.
                    else if (fundingStatus == (int)ofm_funding_StatusCode.Terminated || fundingStatus == (int)ofm_funding_StatusCode.Cancelled || fundingStatus == (int)ofm_funding_StatusCode.Expired || fundingStatus == (int)ofm_funding_StatusCode.Cancelled)
                    {
                        List<D365PaymentLine> notPaidPayments = paymentDeserializedData.Where(r => r.statuscode != ofm_payment_StatusCode.Paid || r.statuscode != ofm_payment_StatusCode.Cancelled).ToList();
                        if (notPaidPayments != null)
                        {
                            foreach (var payment in notPaidPayments)
                            {
                                createPaymentTasks.Add(CancelPaymentLines(payment.Id, appUserService, d365WebApiService, _processParams));
                            }
                        }

                    }
                }

                await Task.WhenAll(createPaymentTasks);
            }

            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        private async Task<JsonObject> CreatePaymentLines(string Facility, decimal fundingAmount, DateTime startdate, DateTime enddate, bool manualReview, string application, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var fiscalYearData = await GetFiscalYearDataAsync();
            List<ofm_fiscal_year> fiscalYears = JsonSerializer.Deserialize<List<ofm_fiscal_year>>(fiscalYearData.Data);
            var businessclosuresdata = await GetBusinessClosuresDataAsync();
            List<DateTime> holidaysList = GetStartTimes(businessclosuresdata.Data.ToString());

            Int32 lineNumber = 1;
            //From start date to end date create payments
            for (DateTime paymentdate = startdate; paymentdate <= enddate; paymentdate = paymentdate.AddMonths(1))
            {
                Guid? fiscalYear = AssignFiscalYear(paymentdate, fiscalYears);

                DateTime invoiceReceivedDate = paymentdate == startdate ? startdate : paymentdate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
                DateTime invoicedate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList);
                DateTime effectiveDate = invoicedate;

                var payload = new JsonObject()
                {
                    { "ofm_invoice_line_number", lineNumber++ },
                    { "ofm_amount", fundingAmount},
                    { "ofm_payment_type", (int) ecc_payment_type.Base },
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
                var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, ofm_payment.EntitySetName, requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to create payments for application with the server error {responseBody}", responseBody.CleanLog());

                    return ProcessResult.Failure(ProcessId, [responseBody], 0, 0).SimpleProcessResult;
                }
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

        private async Task<JsonObject> CreateSupplementaryApplicationPayment(string Facility, DateTime startdate, DateTime firstpaymentDate, bool manualReview, string fundingId, string application, string saApplication, int paymentType, decimal? fundingAmount, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var fiscalYearData = await GetFiscalYearDataAsync();
            List<ofm_fiscal_year> fiscalYears = JsonSerializer.Deserialize<List<ofm_fiscal_year>>(fiscalYearData.Data.ToString());
            var businessclosuresdata = await GetBusinessClosuresDataAsync();
            List<DateTime> holidaysList = GetStartTimes(businessclosuresdata.Data.ToString());

            Guid? fiscalYear = AssignFiscalYear(startdate, fiscalYears);

            //invoice received date is always last day of previous month. But for first payment it is start date of supplementary application
            DateTime invoiceReceivedDate = firstpaymentDate == startdate && firstpaymentDate != null ? startdate : startdate.GetLastBusinessDayOfThePreviousMonth(holidaysList);
            DateTime invoicedate = invoiceReceivedDate.GetCFSInvoiceDate(holidaysList);
            DateTime effectiveDate = invoicedate;

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
                { "ofm_regardingid_ofm_allowance@odata.bind",$"/ofm_allowances({saApplication})"  }
            };

            var requestBody = JsonSerializer.Serialize(payload);
            var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, ofm_payment.EntitySetName, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to create payments for application with the server error {responseBody}", responseBody.CleanLog());

                return ProcessResult.Failure(ProcessId, [responseBody], 0, 0).SimpleProcessResult;
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