using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Emails;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class P300FundingCalculatorProvider : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private string _requestUri = string.Empty;
    private Dictionary<string, FundingRate[]> _parameters;


    public P300FundingCalculatorProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {

        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
        _parameters = new Dictionary<string, FundingRate[]>();
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
            var applicationId = "41733dc8-d292-ee11-be37-000d3a09d499";

            if (string.IsNullOrEmpty(_requestUri))
            {
                //for reference only
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
                                  </entity>
                                </fetch>
                                """;

                var requestUri = $"""                                
                                  ofm_applications?$expand=ofm_licence_application($select=_ofm_application_value,ofm_licenceid,createdon,ofm_accb_providerid,ofm_ccof_facilityid,ofm_ccof_organizationid,_ofm_facility_value,ofm_health_authority,ofm_licence,ofm_tdad_funding_agreement_number,_ownerid_value,statuscode;
                                  $expand=ofm_licence_licencedetail($select=createdon,ofm_care_type,ofm_enrolled_spaces,_ofm_licence_value,ofm_licence_detail,ofm_licence_spaces,ofm_licence_type,ofm_operation_hours_from,ofm_operation_hours_to,ofm_operational_spaces,ofm_overnight_care,ofm_week_days,ofm_weeks_in_operation,_ownerid_value,statuscode)),
                                  ofm_application_funding($select=ofm_fundingid;$filter=(statecode eq 0))&$filter=(ofm_applicationid eq '{applicationId}') and (ofm_licence_application/any(o1:(o1/ofm_licenceid ne null) and (o1/ofm_licence_licencedetail/any(o2:(o2/ofm_licence_detailid ne null))))) and (ofm_application_funding/any(o3:(o3/statecode eq 0)))
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

        if (_data is null)
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

        #region Step 0: Get Fixed Parameters
        await SetupNonHRParameters();
        int MAX_OPERATION_HOURS = 2510;

        #endregion

        #region Step 1: Pre-Calculation

        //fetch the application and licences data
        //Get application and license data -> this will be moved to Calculator Object later so ignore
        var localData = await GetData();
        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<Ofm_Application>>(localData.Data, Setup.s_writeOptionsForLogs);

        var application = serializedData?.FirstOrDefault();

        var fundingId = application?.ofm_application_funding[0].ofm_fundingid;


        //Calculate the total spaces and operation hours

        //Total spaces: Sum the ***ofm_operational_spaces*** for each category of each licence
        //Operation Hours: Select the max operation hours for each category of each licence

        var totalSpaces = 0;
        var operationHours = 0.00;
        var licences = application?.ofm_licence_application;

        foreach (Ofm_Licence_Application licence in licences){
            //One licence could have mulitple categories
            var licenceDetail = licence.ofm_licence_licencedetail;
            totalSpaces += licenceDetail.Sum(item => item.ofm_operational_spaces);
            var maxOperationHoursPerCategory = 0.00;
            foreach(Ofm_Licence_Licencedetail category in licenceDetail)
            {
                var operationHoursPerCategory = category.ofm_weeks_in_operation * (category.ofm_week_days.Split(",").Length) * (category.ofm_operation_hours_to - category.ofm_operation_hours_from).TotalHours;
                maxOperationHoursPerCategory = Math.Max(maxOperationHoursPerCategory, operationHoursPerCategory);
            }
            operationHours = Math.Max(maxOperationHoursPerCategory, operationHours);
        }
        

        _logger.LogDebug(CustomLogEvent.Process, "Total Spaces {totalSpaces}", totalSpaces);


        //Calculate the total operating hours
       
        var adjustment = MAX_OPERATION_HOURS / operationHours;

        var operationalCurrentCost = 0;

        //Facility Type and Cost -> Application

/*        Rent / Lease  1
        Owned With Mortgage 2
        Owned Without Mortgage  3
        Provided Free of Charge 4*/

        var facilityType = 1;
        var facilityCurrentCost = 0.00;

        var ownership = 1;
        #endregion

        #region  Step 2: Non-HR Calculation


        //1. Schedule Funding


        //Not-for-profit = 1, Home-based = 2, Private = 3

        var programmingScheduleFunding = stepScheduleFundingCalculation(ownership, "programming", totalSpaces);
        var adminScheduleFunding = stepScheduleFundingCalculation(ownership, "administration", totalSpaces);
        var operationalScheduleFunding = stepScheduleFundingCalculation(ownership, "operational", totalSpaces);
        var facilityScheduleFunding = stepScheduleFundingCalculation(ownership, "facility", totalSpaces);



        //2. Adjusted Funding
        var programmingAdjustedFunding = programmingScheduleFunding;
        var adminAdjustedFunding = adminScheduleFunding / adjustment;
        var operationalAdjustedFunding = ownership == 2 ? Math.Min(operationalScheduleFunding / adjustment, operationalCurrentCost) : operationalScheduleFunding / adjustment;
        var facilityAdjustedFunding = 0.00;
        if(facilityCurrentCost.Equals(0.00))
        {
            facilityAdjustedFunding = 0.00;
        }
        else if(ownership == 3 && (facilityType == 2 || facilityType == 3))
        {
            facilityAdjustedFunding = 0.00;
        }
        else
        {
            facilityAdjustedFunding = Math.Min(facilityScheduleFunding, facilityCurrentCost);
        }

        #endregion

        #region  Step 3: HR Calculation

        #endregion

        #region  Step 4: Post-Calculation

        #endregion

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    #region Local Validation & Setup Code

    private async Task SetupNonHRParameters()
    {
              //For reference
/*            var fetchXml = """
                        <fetch>
                          <entity name="ofm_funding_rate">
                            <attribute name="ofm_ownership" />
                            <attribute name="ofm_rate" />
                            <attribute name="ofm_spaces_max" />
                            <attribute name="ofm_spaces_min" />
                            <attribute name="ofm_step" />
                            <attribute name="statecode" />
                            <link-entity name="ofm_rate_schedule" from="ofm_rate_scheduleid" to="ofm_rate_schedule">
                              <attribute name="ofm_fiscal_year" />
                              <attribute name="ofm_fundinng_envelope" />
                              <link-entity name="ofm_fiscal_year" from="ofm_fiscal_yearid" to="ofm_fiscal_year">
                                <attribute name="ofm_caption" />
                                <attribute name="statecode" />
                                <filter>
                                  <condition attribute="statuscode" operator="eq" value="1" />
                                </filter>
                              </link-entity>
                            </link-entity>
                          </entity>
                        </fetch>
                """;*/

        var requestUri = $"""
                            ofm_funding_rates?$select=ofm_ownership,ofm_rate,ofm_spaces_max,ofm_spaces_min,ofm_step,statecode&$expand=ofm_rate_schedule($select=_ofm_fiscal_year_value,ofm_fundinng_envelope;$expand=ofm_fiscal_year($select=ofm_caption,statecode))
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

            var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<FundingRate>>(d365Result, Setup.s_writeOptionsForLogs);

        //Programming = 6, Administration = 7, Operational = 8, Facility = 9
        FundingRate[]? progammingScheduleRate = serializedData?.Where(rate => rate?.ofm_rate_schedule?.ofm_fundinng_envelope == 6).ToArray();
        FundingRate[]? administrationScheduleRate = serializedData?.Where(rate => rate?.ofm_rate_schedule?.ofm_fundinng_envelope == 7).ToArray();
        FundingRate[]? operationalScheduleRate = serializedData?.Where(rate => rate?.ofm_rate_schedule?.ofm_fundinng_envelope == 8).ToArray();
        FundingRate[]? facilityScheduleRate = serializedData?.Where(rate => rate?.ofm_rate_schedule?.ofm_fundinng_envelope == 9).ToArray();

        _parameters.Add("programming", progammingScheduleRate);
        _parameters.Add("administration", administrationScheduleRate);
        _parameters.Add("operational", operationalScheduleRate);
        _parameters.Add("facility", facilityScheduleRate);
    }

    #endregion

    private double stepScheduleFundingCalculation(int ownership, string envolope, int totalSpaces)
    {
        var funding = 0.00;

        FundingRate[] scheduleRate = _parameters[envolope].Where(rate => rate.ofm_ownership == ownership).OrderBy(rate => rate.ofm_step).ToArray();

        for (int i = 0; i < scheduleRate.Length; i++)
        {
            if (totalSpaces - scheduleRate[i].ofm_spaces_max >= 0)
            {
                funding += scheduleRate[i].ofm_rate * (scheduleRate[i].ofm_spaces_max - scheduleRate[i].ofm_spaces_min + 1);
            }
            else if (totalSpaces - scheduleRate[i].ofm_spaces_min > 0)
            {
                funding += scheduleRate[i].ofm_rate * (totalSpaces - scheduleRate[i].ofm_spaces_min + 1);
            }
        }

        return funding; 
    }
}