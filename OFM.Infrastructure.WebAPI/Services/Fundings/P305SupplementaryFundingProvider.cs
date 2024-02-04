using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Fundings;

public class P305SupplementaryFundingProvider : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _requestUri = string.Empty;

    public P305SupplementaryFundingProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Funding.CalculateSupplementaryFundingId;
    public string ProcessName => Setup.Process.Funding.CalculateSupplementaryFundingName;

    public string RequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            // Use Paging Cookie for large datasets
            // Use exact filters to reduce the matching data and enhance performance
            var supplementaryId = _processParams?.Funding?.SupplementaryId;

            if (string.IsNullOrEmpty(_requestUri))
            {
                //for reference only
                /*
                var fetchXml = $"""
                                <fetch>
                                  <entity name="ofm_allowance">
                                    <attribute name="ofm_allowance_number" />
                                    <attribute name="ofm_allowance_type" />
                                    <attribute name="ofm_application" />
                                    <attribute name="ofm_funding_amount" />
                                    <attribute name="ofm_needs_number_of_children_scd" />
                                    <attribute name="ofm_transport_estimated_monthly_km" />
                                    <attribute name="ofm_transport_lease" />
                                    <attribute name="ofm_transport_monthly_lease" />
                                    <attribute name="ofm_transport_odometer" />
                                    <attribute name="statuscode" />
                                    <filter>
                                      <condition attribute="ofm_allowanceid" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                                    </filter>
                                    <link-entity name="ofm_supplementary_schedule" from="ofm_supplementary_scheduleid" to="ofm_supplementary_schedule">
                                      <attribute name="ofm_end_date" />
                                      <attribute name="ofm_indigenous_10_to_19_spaces" />
                                      <attribute name="ofm_indigenous_ge_20_spaces" />
                                      <attribute name="ofm_indigenous_le_9_spaces" />
                                      <attribute name="ofm_needs_10_to_19_spaces" />
                                      <attribute name="ofm_needs_ge_20_spaces" />
                                      <attribute name="ofm_needs_le_9_spaces" />
                                      <attribute name="ofm_sqw_caps_for_centers" />
                                      <attribute name="ofm_sqw_caps_for_homebased" />
                                      <attribute name="ofm_start_date" />
                                      <attribute name="ofm_transport_ge_20_spaces_lease_cap_month" />
                                      <attribute name="ofm_transport_less_20_spaces_lease_cap_month" />
                                      <attribute name="ofm_transport_reimbursement_rate_per_km" />
                                    </link-entity>
                                  </entity>
                                </fetch>
                                """;
                */

                var requestUri = $"""                                
                                ofm_allowances?$select=ofm_allowance_number,ofm_allowance_type,_ofm_application_value,ofm_funding_amount,ofm_needs_number_of_children_scd,ofm_transport_estimated_monthly_km,ofm_transport_lease,ofm_transport_monthly_lease,ofm_transport_odometer,statuscode
                                &$expand=ofm_supplementary_schedule($select=ofm_end_date,ofm_indigenous_10_to_19_spaces,ofm_indigenous_ge_20_spaces,ofm_indigenous_le_9_spaces,ofm_needs_10_to_19_spaces,ofm_needs_ge_20_spaces,ofm_needs_le_9_spaces,ofm_sqw_caps_for_centers,ofm_sqw_caps_for_homebased,ofm_start_date,ofm_transport_ge_20_spaces_lease_cap_month,ofm_transport_less_20_spaces_lease_cap_month,ofm_transport_reimbursement_rate_per_km)
                                &$filter=(ofm_allowanceid eq '{supplementaryId}') and (ofm_supplementary_schedule/ofm_supplementary_scheduleid ne null)
                                """;
                _requestUri = requestUri.CleanCRLF();
            }

            return _requestUri;
        }
    }

    //For reference
    public async Task<ProcessData> GetApplicationData(string applicationId)
    {

        if (applicationId is not null)
        {
            var requestUri = $"""                                
                                ofm_applications?$expand=ofm_licence_application($select=_ofm_application_value,ofm_licenceid,createdon,ofm_accb_providerid,ofm_ccof_facilityid,ofm_ccof_organizationid,_ofm_facility_value,ofm_health_authority,ofm_licence,ofm_tdad_funding_agreement_number,_ownerid_value,statuscode;
                                $expand=ofm_licence_licencedetail($select=createdon,ofm_care_type,ofm_enrolled_spaces,_ofm_licence_value,ofm_licence_detail,ofm_licence_spaces,ofm_licence_type,ofm_operation_hours_from,ofm_operation_hours_to,ofm_operational_spaces,ofm_overnight_care,ofm_week_days,ofm_weeks_in_operation,_ownerid_value,statuscode);
                                $filter=(statecode eq 0)),ofm_application_funding($select=ofm_fundingid;$filter=(statecode eq 0))
                                &$filter=(ofm_applicationid eq '{applicationId}') and (ofm_licence_application/any(o1:(o1/statecode eq 0) and (o1/ofm_licence_licencedetail/any(o2:(o2/ofm_licence_detailid ne null))))) and (ofm_application_funding/any(o3:(o3/statecode eq 0)))
                                """;

            _logger.LogDebug(CustomLogEvent.Process, "Getting application data with query {requestUri}", requestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUri.CleanCRLF(), isProcess: true);

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

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P305SupplementaryFundingProvider));

        if (_data is null && _processParams is not null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting Supplemental data with query {requestUri}", RequestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query Supplemental data with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No Supplemental found with query {requestUri}", RequestUri);
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

        #region Step 0: Pre-Calculation

        //fetch the supplemental data
        var localData = await GetData();
        var deserializedData = JsonSerializer.Deserialize<List<Supplementary>>(localData.Data, Setup.s_writeOptionsForLogs);

        var supplementary = deserializedData?.FirstOrDefault();

        //fetch the application and licences data
        var applicationId = supplementary._ofm_application_value;
        var applicationData = await GetApplicationData(applicationId);
        var deserializedApplicationData = JsonSerializer.Deserialize<List<Models.Application>>(applicationData.Data, Setup.s_writeOptionsForLogs);

        var application = deserializedApplicationData?.FirstOrDefault();

        //Calculate the total operational spaces
        //Total spaces: Sum the ***ofm_operational_spaces*** for each category of each licence

        var licences = application?.ofm_licence_application;

        var categories = licences?.SelectMany(licence => licence.ofm_licence_licencedetail);

        var totalSpaces = categories?.Sum(category => category.ofm_operational_spaces) ?? 0;

        _logger.LogDebug(CustomLogEvent.Process, "Total Spaces {totalSpaces}", totalSpaces);

        #endregion

        #region Step 2: Calculate Allowance

        decimal calculatedFundingAmount = 0.0M;
        /*
            Support Needs Programming = 1
            Indigenous Programming = 2
            Transportation = 3
        */
        switch (supplementary.ofm_allowance_type)
        {
            case 1:
                if (totalSpaces <= 9)
                {
                    calculatedFundingAmount = supplementary.ofm_supplementary_schedule.ofm_needs_le_9_spaces;
                }
                else if (totalSpaces <= 19)
                {
                    calculatedFundingAmount = supplementary.ofm_supplementary_schedule.ofm_needs_10_to_19_spaces;
                }
                else
                {
                    calculatedFundingAmount = supplementary.ofm_supplementary_schedule.ofm_needs_ge_20_spaces;
                }
                break;
            case 2:
                if (totalSpaces <= 9)
                {
                    calculatedFundingAmount = supplementary.ofm_supplementary_schedule.ofm_indigenous_le_9_spaces;
                }
                else if (totalSpaces <= 19)
                {
                    calculatedFundingAmount = supplementary.ofm_supplementary_schedule.ofm_indigenous_10_to_19_spaces;
                }
                else
                {
                    calculatedFundingAmount = supplementary.ofm_supplementary_schedule.ofm_indigenous_ge_20_spaces;
                }
                break;
            case 3:
                //if lease = Yes
                if (supplementary.ofm_transport_lease == 1)
                {
                    if (totalSpaces < 20)
                    {
                        calculatedFundingAmount += Math.Min(supplementary.ofm_transport_monthly_lease, supplementary.ofm_supplementary_schedule.ofm_transport_less_20_spaces_lease_cap_month) * 12;

                    }
                    else if (totalSpaces >= 20)
                    {
                        calculatedFundingAmount += Math.Min(supplementary.ofm_transport_monthly_lease, supplementary.ofm_supplementary_schedule.ofm_transport_ge_20_spaces_lease_cap_month) * 12;
                    }
                }

                calculatedFundingAmount += supplementary.ofm_transport_estimated_monthly_km * supplementary.ofm_supplementary_schedule.ofm_transport_reimbursement_rate_per_km * 12;

                break;
        }

        #endregion

        #region  Step 4: Post-Calculation

        //Update the supplemental record

        var updateSupplementalUrl = @$"ofm_allowances({supplementary.ofm_allowanceid})";
        var updateContent = new
        {
            ofm_funding_amount = calculatedFundingAmount
        };
        var requestBody = System.Text.Json.JsonSerializer.Serialize(updateContent);
        var response = await _d365webapiservice.SendPatchRequestAsync(_appUserService.AZSystemAppUser, updateSupplementalUrl, requestBody);

        _logger.LogDebug(CustomLogEvent.Process, "Update Supplemental Record {supplemental.ofm_allowanceid}", supplementary.ofm_allowanceid);

        #endregion

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}