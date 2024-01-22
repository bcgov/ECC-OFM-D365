using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Emails;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ECC.Core.DataContext;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;
using System.Text;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class P300FundingCalculatorProvider : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _requestUri = string.Empty;
    private RateSchedule _config;


    public P300FundingCalculatorProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {

        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Funding.FundingCalculatorId;
    public string ProcessName => Setup.Process.Funding.FundingCalculatorName;

    public string RequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            // Use Paging Cookie for large datasets
            // Use exact filters to reduce the matching data and enhance performance
            //var applicationId = "41733dc8-d292-ee11-be37-000d3a09d499";
            var applicationId = _processParams?.ApplicationId;

            if (string.IsNullOrEmpty(_requestUri))
            {
                //for reference only
                /*
                var fetchXml = $"""
                                <fetch>
                                  <entity name="ofm_application">
                                    <filter>
                                      <condition attribute="ofm_applicationid" operator="eq" value="41733dc8-d292-ee11-be37-000d3a09d499" />
                                    </filter>
                                    <link-entity name="ofm_licence" from="ofm_application" to="ofm_applicationid" alias="ApplicationLicense">
                                      <attribute name="ofm_application" />
                                      <attribute name="ofm_licenceid" />
                                      <attribute name="createdon" />
                                      <attribute name="ofm_accb_providerid" />
                                      <attribute name="ofm_ccof_facilityid" />
                                      <attribute name="ofm_ccof_organizationid" />
                                      <attribute name="ofm_facility" />
                                      <attribute name="ofm_health_authority" />
                                      <attribute name="ofm_licence" />
                                      <attribute name="ofm_tdad_funding_agreement_number" />
                                      <attribute name="ownerid" />
                                      <attribute name="statuscode" />
                                      <filter>
                                        <condition attribute="statecode" operator="eq" value="0" />
                                      </filter>
                                      <link-entity name="ofm_licence_detail" from="ofm_licence" to="ofm_licenceid" alias="Licence">
                                        <attribute name="createdon" />
                                        <attribute name="ofm_care_type" />
                                        <attribute name="ofm_enrolled_spaces" />
                                        <attribute name="ofm_licence" />
                                        <attribute name="ofm_licence_detail" />
                                        <attribute name="ofm_licence_spaces" />
                                        <attribute name="ofm_licence_type" />
                                        <attribute name="ofm_operation_hours_from" />
                                        <attribute name="ofm_operation_hours_to" />
                                        <attribute name="ofm_operational_spaces" />
                                        <attribute name="ofm_overnight_care" />
                                        <attribute name="ofm_week_days" />
                                        <attribute name="ofm_weeks_in_operation" />
                                        <attribute name="ownerid" />
                                        <attribute name="statuscode" />
                                      </link-entity>
                                    </link-entity>
                                    <link-entity name="ofm_funding" from="ofm_application" to="ofm_applicationid" alias="Funding">
                                      <attribute name="ofm_fundingid" />
                                      <filter>
                                        <condition attribute="statecode" operator="eq" value="0" />
                                      </filter>
                                    </link-entity>
                                  </entity>
                                </fetch>
                                """;
                */

                var requestUri = $"""                                
                                ofm_applications?$expand=ofm_licence_application($select=_ofm_application_value,ofm_licenceid,createdon,ofm_accb_providerid,ofm_ccof_facilityid,ofm_ccof_organizationid,_ofm_facility_value,ofm_health_authority,ofm_licence,ofm_tdad_funding_agreement_number,_ownerid_value,statuscode;
                                $expand=ofm_licence_licencedetail($select=createdon,ofm_care_type,ofm_enrolled_spaces,_ofm_licence_value,ofm_licence_detail,ofm_licence_spaces,ofm_licence_type,ofm_operation_hours_from,ofm_operation_hours_to,ofm_operational_spaces,ofm_overnight_care,ofm_week_days,ofm_weeks_in_operation,_ownerid_value,statuscode);
                                $filter=(statecode eq 0)),ofm_application_funding($select=ofm_fundingid;$filter=(statecode eq 0))
                                &$filter=(ofm_applicationid eq '{applicationId}') and (ofm_licence_application/any(o1:(o1/statecode eq 0) and (o1/ofm_licence_licencedetail/any(o2:(o2/ofm_licence_detailid ne null))))) and (ofm_application_funding/any(o3:(o3/statecode eq 0)))
                                """;

                _requestUri = requestUri.CleanCRLF();
            }

            return _requestUri;
        }
    }

    //For reference
    public async Task<ProcessData> GetData()
    {

        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P300FundingCalculatorProvider));

        if (_data is null && _processParams is not null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting application data with query {requestUri}", RequestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query application data with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No application found with query {requestUri}", RequestUri);
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString(Setup.s_writeOptionsForLogs));
        }

        return await Task.FromResult(_data);
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;

        #region Step 0: Get Fixed Parameters
        await SetupConfiguration();

        //Fix Parameters
        int MAX_OPERATION_HOURS = 2510; //50.2 * 50
        var parentFeePerDayTable = new Dictionary<int, decimal> {
                { 1, _config.ofm_parent_fee_per_day_ft },
                { 2, _config.ofm_parent_fee_per_day_pt }
            };

        var parentFeePerMonthTable = new Dictionary<int, decimal> {
                { 1, _config.ofm_parent_fee_per_month_ft },
                { 2, _config.ofm_parent_fee_per_month_pt }
            };

        #endregion

        #region Step 1: Pre-Calculation

        //fetch the application and licences data
        //Get application and license data -> this will be moved to Calculator Object later so ignore
        var localData = await GetData();
        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<OFM.Infrastructure.WebAPI.Models.Application>>(localData.Data, Setup.s_writeOptionsForLogs);

        var application = serializedData?.FirstOrDefault();
        var fundingId = application?.ofm_application_funding?.FirstOrDefault().ofm_fundingid;


        //Calculate the total spaces, operation hours and Parent Fee
        //Total spaces: Sum the ***ofm_operational_spaces*** for each category of each licence
        //Operation Hours: Select the max operation hours for each category of each licence

        double operationHours = 0;
        var licences = application?.ofm_licence_application;
        
        var categories = licences?.SelectMany(licence => licence.ofm_licence_licencedetail);

        var totalSpaces = categories?.Sum(category => category.ofm_operational_spaces)?? 0;

        int? preSchoolCount = 0;  int? preSchoolWeeksinOperation = 0; var preSchoolHoursPerDay = 0.00; int? preSchoolWorkDay = 0;
        int? schoolAgeCount = 0; int? schoolAgeWeeksinOperation = 0; var schoolAgeHoursPerDay = 0.00; int? schoolAgeWorkDay = 0;

        decimal totoalParentFee = 0;

        foreach(OFM.Infrastructure.WebAPI.Models.LicenceDetail category in categories)
        {
           
            //if the category is preschool (4,5,6) or schoolAge (7,8,9) -> need to calculate the average operation hours
            if ((int)category.ofm_licence_type == 4 || (int)category.ofm_licence_type== 5 || (int)category.ofm_licence_type == 6)
            {
                preSchoolCount++;
                preSchoolHoursPerDay += (category.ofm_operation_hours_to - category.ofm_operation_hours_from).TotalHours;
                preSchoolWorkDay += (category.ofm_week_days.Count());
                preSchoolWeeksinOperation += category.ofm_weeks_in_operation;
            }
            else if ((int)category.ofm_licence_type == 7 || (int)category.ofm_licence_type == 8 || (int)category.ofm_licence_type == 9)
            {
                schoolAgeCount++;
                schoolAgeHoursPerDay += (category.ofm_operation_hours_to - category.ofm_operation_hours_from).TotalHours;
                schoolAgeWorkDay += (category.ofm_week_days.Count());
                schoolAgeWeeksinOperation += category.ofm_weeks_in_operation;
            }
            else
            {
                var operationHoursPerCategory = category.ofm_weeks_in_operation * (category.ofm_week_days.Count()) * (category.ofm_operation_hours_to - category.ofm_operation_hours_from).TotalHours;
                operationHours = Math.Max(operationHours, operationHoursPerCategory);
            }

            var parentFeePerDay = parentFeePerDayTable[(int)category.ofm_care_type] * category.ofm_weeks_in_operation * (category.ofm_week_days.Count());
            var parentFeePerMonth = parentFeePerMonthTable[(int)category.ofm_care_type] * 12;

            var parentFeePerCategory = Math.Min(parentFeePerDay, parentFeePerMonth) * category.ofm_operational_spaces;
            totoalParentFee += parentFeePerCategory;
        }
        if(preSchoolCount > 0)
        {
            var avgPreSchoolHours = (preSchoolHoursPerDay / preSchoolCount) * (preSchoolWorkDay / preSchoolCount) * (preSchoolWeeksinOperation / preSchoolCount);
            operationHours = Math.Max(operationHours, avgPreSchoolHours?? 0);
        }

        if (schoolAgeCount > 0)
        {
            var avgSchoolAgeHours = (schoolAgeHoursPerDay / schoolAgeCount) * (schoolAgeWorkDay / schoolAgeCount) * (schoolAgeWeeksinOperation / schoolAgeCount);
            operationHours = Math.Max(operationHours, avgSchoolAgeHours?? 0);
        }

        _logger.LogDebug(CustomLogEvent.Process, "Total Spaces {totalSpaces}", totalSpaces);
        _logger.LogDebug(CustomLogEvent.Process, "Annual Operation Hours {totalSpaces}", operationHours);

        var operationalCurrentCost = application?.ofm_costs_yearly_operating_costs?? 0;
        var facilityType = (int)application?.ofm_costs_facility_type;
        var facilityCurrentCost = application?.ofm_costs_year_facility_costs?? 0;
        var ownership = (int)application?.ofm_summary_ownership;
        #endregion

        #region  Step 2: Non-HR Calculation


        //1. Schedule Funding
        //Ownership: Not-for-profit = 1, Home-based = 2, Private = 3
        //Programming = 1, Administration = 2, Operational = 3, Facility = 4
        var programmingScheduleFunding = stepScheduleFundingCalculation(ownership, 1, totalSpaces);
        var adminScheduleFunding = stepScheduleFundingCalculation(ownership, 2, totalSpaces);
        var operationalScheduleFunding = stepScheduleFundingCalculation(ownership, 3, totalSpaces);
        var facilityScheduleFunding = stepScheduleFundingCalculation(ownership, 4, totalSpaces);

        //2. Adjusted Funding
        //Calculate the adjustment
        decimal adjustment = MAX_OPERATION_HOURS / (decimal) operationHours;

        var programmingAdjustedFunding = programmingScheduleFunding;
        var adminAdjustedFunding = adminScheduleFunding / adjustment;
        var operationalAdjustedFunding = ownership == 2 ? Math.Min(operationalScheduleFunding / adjustment, operationalCurrentCost) : operationalScheduleFunding / adjustment;
        decimal facilityAdjustedFunding = 0;

        if(facilityCurrentCost.Equals(0))
        {
            facilityAdjustedFunding = 0;
        }
        else if(ownership == 3 && (facilityType == 2 || facilityType == 3))
        {
            facilityAdjustedFunding = 0;
        }
        else
        {
            facilityAdjustedFunding = Math.Min(facilityScheduleFunding, facilityCurrentCost);
        }

        _logger.LogDebug(CustomLogEvent.Process, "Finish Non HR calculation");

        #endregion

        #region  Step 3: HR Calculation

        #endregion

        #region  Step 4: Post-Calculation

        //Update the funding record

        var updateFundingUrl = @$"ofm_fundings({fundingId})";
        var updateContent = new
        {
            ofm_envelope_programming_proj = programmingAdjustedFunding,
            ofm_envelope_administrative_proj = adminAdjustedFunding,
            ofm_envelope_operational_proj = operationalAdjustedFunding,
            ofm_envelope_facility_proj = facilityAdjustedFunding
        };
        var requestBody = System.Text.Json.JsonSerializer.Serialize(updateContent);
        var response = await _d365webapiservice.SendPatchRequestAsync(_appUserService.AZSystemAppUser, updateFundingUrl, requestBody);

        _logger.LogDebug(CustomLogEvent.Process, "Update Funding Record {fundingId}", fundingId);
        #endregion

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    #region Local Validation & Setup Code

    private async Task SetupConfiguration()
    {
        /* For Reference
         * <fetch>
            <entity name="ofm_rate_schedule">
             <attribute name="ofm_caption" />
             <attribute name="ofm_end_date" />
             <attribute name="ofm_parent_fee_per_day_ft" />
             <attribute name="ofm_parent_fee_per_day_pt" />
             <attribute name="ofm_parent_fee_per_month_ft" />
             <attribute name="ofm_parent_fee_per_month_pt" />
             <attribute name="ofm_start_date" />
             <attribute name="statecode" />
             <link-entity name="ofm_funding_rate" from="ofm_rate_schedule" to="ofm_rate_scheduleid">
               <attribute name="ofm_caption" />
               <attribute name="ofm_nonhr_funding_envelope" />
               <attribute name="ofm_rate" />
               <attribute name="ofm_spaces_max" />
               <attribute name="ofm_spaces_min" />
               <attribute name="ofm_step" />
               <attribute name="statecode" />
               <attribute name="ofm_ownership" />
             </link-entity>
            </entity>
           </fetch>
        */

        var requestUri = $"""
                            ofm_rate_schedules?$select=ofm_caption,ofm_end_date,ofm_parent_fee_per_day_ft,ofm_parent_fee_per_day_pt,ofm_parent_fee_per_month_ft,ofm_parent_fee_per_month_pt,ofm_start_date,statecode
                            &$expand=ofm_rateschedule_fundingrate($select=ofm_caption,ofm_nonhr_funding_envelope,ofm_rate,ofm_spaces_max,ofm_spaces_min,ofm_step,statecode,ofm_ownership)
                            &$filter=(ofm_rateschedule_fundingrate/any(o1:(o1/ofm_funding_rateid ne null)))
                            """;

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query the funding rate with a server error {responseBody}", responseBody.CleanLog());
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No funding rate found with query {requestUri}", requestUri);
                }
                d365Result = currentValue!;
            }

        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<RateSchedule>>(d365Result, Setup.s_writeOptionsForLogs);

        _config = serializedData?.FirstOrDefault();
        _logger.LogInformation(CustomLogEvent.Process, "No funding rate found with query {requestUri}", requestUri);
    }

    #endregion

    private decimal stepScheduleFundingCalculation(int? ownership, int envolope, int totalSpaces)
    {
        decimal? funding = 0;

        //Programming = 1, Administration = 2, Operational = 3, Facility = 4
        ECC.Core.DataContext.ofm_funding_rate[] scheduleRate = _config.ofm_rateschedule_fundingrate.Where(rate => (int) rate.ofm_ownership == ownership && (int) rate.ofm_nonhr_funding_envelope == envolope).OrderBy(rate => rate.ofm_step).ToArray();

        for (int i = 0; i < scheduleRate.Length; i++)
        {
            if (totalSpaces - scheduleRate[i].ofm_spaces_max >= 0)
            {
                funding += scheduleRate[i].ofm_rate?.Value * (scheduleRate[i].ofm_spaces_max - scheduleRate[i].ofm_spaces_min + 1);
            }
            else if (totalSpaces - scheduleRate[i].ofm_spaces_min > 0)
            {
                funding += scheduleRate[i].ofm_rate.Value * (totalSpaces - scheduleRate[i].ofm_spaces_min + 1);
            }
        }

        return funding?? 0; 
    }
}