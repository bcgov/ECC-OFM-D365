using ECC.Core.DataContext;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings.Validators;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class FundingCalculator
{
    private readonly IFundingRepository _fundingRepository;
    private readonly IProviderProfileRepository _providerRepository;
    private readonly ID365DataService _d365dataService;
    private FundingResult? _fundingResult;
    private RateSchedule _rateSchedule;
    private readonly Funding _funding;

    public FundingCalculator(Funding funding, IEnumerable<RateSchedule> rateSchedules)
    {
        _funding = funding;
        _rateSchedule = rateSchedules.First(sch => sch.Id == _funding?.ofm_rate_schedule?.ofm_rate_scheduleid);
    }

    private IEnumerable<LicenceDetail> CoreServices
    {
        get
        {
            var coreServices = _funding.ofm_facility.ofm_facility_licence.SelectMany(licence => licence.ofm_licence_licencedetail);
            foreach (var service in coreServices)
            {
                service.RateSchedule = _rateSchedule;
            }
            return coreServices;
        }
    }

    private int TotalSpaces => CoreServices.Sum(detail => detail.ofm_operational_spaces!.Value);

    #region Pre-defined Queries

    private string RequestUriCCLR
    {
        get
        {
            string fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
                      <entity name="ofm_cclr_ratio">
                        <attribute name="ofm_cclr_ratioid" />
                        <attribute name="ofm_order_number" />
                        <attribute name="ofm_caption" />
                        <attribute name="ofm_fte_min_ite" />
                        <attribute name="ofm_fte_min_ece" />
                        <attribute name="ofm_fte_min_ecea" />
                        <attribute name="ofm_fte_min_ra" />
                        <attribute name="ofm_spaces_max" />
                        <attribute name="ofm_spaces_min" />
                        <attribute name="ofm_group_size" />
                        <attribute name="ofm_licence_group" />
                        <attribute name="ofm_licence_mapping" />
                        <attribute name="ofm_rate_schedule" />
                        <filter>
                          <condition attribute="statuscode" operator="eq" value="1">
                            <value>1</value>
                          </condition>
                        </filter>
                        <order attribute="ofm_order_number" />
                      </entity>
                    </fetch>          
                    """;

            var requestUri = $"""
                         ofm_cclr_ratios?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

    private string RequestUriApplication
    {
        get
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
                                &$filter=(ofm_applicationid eq '{_funding?.ofm_application}') and (ofm_licence_application/any(o1:(o1/statecode eq 0) and (o1/ofm_licence_licencedetail/any(o2:(o2/ofm_licence_detailid ne null))))) and (ofm_application_funding/any(o3:(o3/statecode eq 0)))
                                """;

            return requestUri;
        }
    }

    #endregion

    #region DATA

    //public ofm_rate_schedule? RateSchedule
    //{
    //    get
    //    {
    //        if (_rateSchedule == null)
    //        {
    //            var rateScheduleData = _d365dataService.FetchData(RequestUriRateSchedule, "ratescheduleKey");
    //            _rateSchedule = rateScheduleData.Deserialize<ofm_rate_schedule>(Setup.s_readOptionsRelaxed)!;
    //        }

    //        return _rateSchedule;
    //    }
    //}

    //public async Task<ofm_rate_schedule> GetRateScheduleData()
    //{
    //    //_logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P205SendNotificationProvider));

    //    //if (_data is null && _processParams is not null)
    //    //{
    //    //_logger.LogDebug(CustomLogEvent.Process, "Getting active contacts from a marketinglist with query {requestUri}", RequestUri.CleanLog());

    //    var response = await _d365dataService.FetchDataAsync(RequestUriRateSchedule, "ratescheduleKey");


    //    //if (!response.IsSuccessStatusCode)
    //        //{
    //        //    var responseBody = await response.Content.ReadAsStringAsync();
    //        //    _logger.LogError(CustomLogEvent.Process, "Failed to query members on the contact list with the server error {responseBody}", responseBody.CleanLog());

    //        //    return await Task.FromResult(new ProcessData(string.Empty));
    //        //}

    //        //var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

    //        JsonNode d365Result = string.Empty;
    //        if (response?.TryGetPropertyValue("value", out var currentValue) == true)
    //        {
    //            //if (currentValue?.AsArray().Count == 0)
    //            //{
    //            //    _logger.LogInformation(CustomLogEvent.Process, "No members on the contact list found with query {requestUri}", RequestUri.CleanLog());
    //            //}
    //            d365Result = currentValue!;
    //        }

    //        var data = new ProcessData(d365Result);

    //        var rateScheduleData = await _d365dataService.FetchDataAsync(RequestUriRateSchedule, "ratescheduleKey");
    //        var deserializedRateScheduleData = JsonSerializer.Deserialize<ofm_rate_schedule>(rateScheduleData, Setup.s_readOptionsRelaxed);

    //    //_logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data?.Data.ToString().CleanLog());
    //    //}

    //    return await Task.FromResult(deserializedRateScheduleData);
    //}

    #endregion

    #region HR Envelopes

    #endregion

    public void Evaluate()
    {
        // NOTE: For HR envelopes, apply the FTE minimum (0.5) at the combined care type level.(Decision made on Feb 07, 2024 with the Ministry)

        #region Validation

        var fundingRules = new MustHaveFundingNumberBaseRule();
        fundingRules.NextValidator(new ValidApplicationStatusRule())
                       //            .NextValidator(new ValidCoreServiceRule())
                       //            .NextValidator(new ApplicationLastModifiedRule())
                       //            .NextValidator(new GoodStandingRule())
                       //            .NextValidator(new DuplicateLicenceTypeRule())
                       //.NextValidator(new SplitRoomRule())
                       .NextValidator(new MustHaveValidRateScheduleRule());
        try
        {
            var validationResult = fundingRules.Validate(_funding);
        }
        catch (ValidationException exp)
        {
            _fundingResult = FundingResult.AutoRejected(_funding.ofm_funding_number, null, null, new[] { exp.Message });
        }

        #endregion

        FundingAmounts fundingAmounts = new() {
            //Projected Base Amounts
            HRTotal_Projected = CoreServices.Sum(cs => cs.TotalRenumeration),
            HRWagesPaidTimeOff_Projected = CoreServices.Sum(cs => cs.TotalStaffingCost),
            HRBenefits_Projected = CoreServices.Sum(cs => cs.TotalBenefitsCostPerYear),
            //HREmployerHealthTax_Projected = CoreServices.Sum(cs => cs.GetEmployerHealthTax()),
            HRProfessionalDevelopmentHours_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),
            HRProfessionalDevelopmentExpenses_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),

            //NonHRProgramming_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),
            //NonHRAdmistrative_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),
            //NonHROperational_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),
            //NonHRFacility_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses)

            ////Parent Fees
            //ofm_envelope_hr_total_pf = fm.HRTotal_PF,
            //ofm_envelope_hr_wages_paidtimeoff_pf = fm.HRWagesPaidTimeOff_PF,
            //ofm_envelope_hr_benefits_pf = fm.HRBenefits_PF,
            //ofm_envelope_hr_employerhealthtax_pf = fm.HREmployerHealthTax_PF,
            //ofm_envelope_hr_prodevexpenses_pf = fm.HRProfessionalDevelopmentExpenses_PF,

            //ofm_envelope_programming_pf = fm.NonHRProgramming_PF,
            //ofm_envelope_administrative_pf = fm.NonHRAdmistrative_PF,
            //ofm_envelope_operational_pf = fm.NonHROperational_PF,
            //ofm_envelope_facility_pf = fm.NonHRFacility_PF,

            ////Base Amounts Column
            //ofm_envelope_hr_total = fm.HRTotal,
            //ofm_envelope_hr_wages_paidtimeoff = fm.HRWagesPaidTimeOff,
            //ofm_envelope_hr_benefits = fm.HRBenefits,
            //ofm_envelope_hr_employerhealthtax = fm.HREmployerHealthTax,
            //ofm_envelope_hr_prodevexpenses = fm.HRProfessionalDevelopmentExpenses,

            //ofm_envelope_programming = fm.NonHRProgramming,
            //ofm_envelope_administrative = fm.NonHRAdmistrative,
            //ofm_envelope_operational = fm.NonHROperational,
            //ofm_envelope_facility = fm.NonHRFacility,

            ////Grand Totals
            //ofm_envelope_grand_total_proj = fm.GrandTotal_Projected,
            //ofm_envelope_grand_total_pf = fm.GrandTotal_PF,
            //ofm_envelope_grand_total = fm.GrandTotal
        };

        #region Non-HR Calculation

        //CalculateNonHRFundingAmountsCommand fundingCalculator = new(_fundingAmountsRepository, _funding);
        //fundingCalculator.Execute();

        #endregion

        _fundingResult = FundingResult.AutoApproved(_funding.ofm_funding_number, fundingAmounts, null);
    }

    public async Task<bool> ProcessFundingResult()
    {
        if (_fundingResult is null || !_fundingResult.IsValidFundingResult())
            return await Task.FromResult(false); //Log the message

        await _fundingRepository.SaveFundingAmounts(_fundingResult);

        // Generate the PDF Funding Agreement & Save it to the funding record

        // Send Funding Agreement Notifications to the provider

        // Other tasks

        return await Task.FromResult(false); 
    }

    public async Task<bool> CalculateDefaultSpaceAllocation()
    {
        if (_fundingResult is null || !_fundingResult.IsValidFundingResult())
            return await Task.FromResult(false); //Log the message

        return await Task.FromResult(false);
    }

    internal Task<bool> SetDefaultSpaceAllocations(IEnumerable<ofm_space_allocation> spaceAllocations)
    {
        throw new NotImplementedException();
    }
}

public enum ValidationMode
{
    Simple,
    Comprehensive,
    HROnly,
    NonHROnly,
    PrivateOnly,
    NonProfitOnly
}