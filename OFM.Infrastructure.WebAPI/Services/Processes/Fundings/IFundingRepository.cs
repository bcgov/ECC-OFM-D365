﻿using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk.Client;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IFundingRepository
{
    Task<IEnumerable<RateSchedule>> LoadRateSchedulesAsync();
    Task<Funding> GetFundingByIdAsync(Guid id);
    Task<IEnumerable<SpaceAllocation>> GetSpacesAllocationByFundingIdAsync(Guid id);
    Task<bool> SaveFundingAmounts(FundingResult fundingResult);
    Task<bool> SaveDefaultSpacesAllocation(IEnumerable<ofm_space_allocation> spacesAllocation);
}

public class FundingRepository : IFundingRepository
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ID365DataService _dataService;
    private Guid? _fundingId;

    public FundingRepository(ID365AppUserService appUserService, ID365WebApiService service, ID365DataService dataService)
    {
        _appUserService = appUserService;
        _d365webapiservice = service;
        _dataService = dataService;
    }

    #region Pre-Defined Queries

    private string RateScheduleRequestUri
    {
        get
        {
            // For reference only
            string fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="ofm_rate_schedule">
                        <attribute name="ofm_caption" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_parent_fee_per_day_ft" />
                        <attribute name="ofm_parent_fee_per_day_pt" />
                        <attribute name="ofm_parent_fee_per_month_ft" />
                        <attribute name="ofm_parent_fee_per_month_pt" />
                        <attribute name="ofm_start_date" />
                        <attribute name="createdon" />
                        <attribute name="ofm_average_benefit_load" />
                        <attribute name="ofm_cpp" />
                        <attribute name="ofm_cultural_hours_per_fte" />
                        <attribute name="ofm_days_in_a_week" />
                        <attribute name="ofm_eht_maximum_cost_for_profit" />
                        <attribute name="ofm_eht_maximum_cost_not_for_profit" />
                        <attribute name="ofm_eht_minimum_cost_for_profit" />
                        <attribute name="ofm_ei" />
                        <attribute name="ofm_elf_educational_programming_cap_fte_year" />
                        <attribute name="ofm_elf_hours_per_fte" />
                        <attribute name="ofm_extended_benefits" />
                        <attribute name="ofm_for_profit_eht_over_1_5m" />
                        <attribute name="ofm_for_profit_eht_over_500k" />
                        <attribute name="ofm_hours_per_day" />
                        <attribute name="ofm_inclusion_hours_per_fte" />
                        <attribute name="ofm_licenced_childcare_cap_per_fte_per_year" />
                        <attribute name="ofm_licensed_childcare_hours_per_fte" />
                        <attribute name="ofm_no_of_sick_days" />
                        <attribute name="ofm_no_of_stat_holidays" />
                        <attribute name="ofm_no_of_vacation_days" />
                        <attribute name="ofm_not_for_profit_eht_over_1_5m" />
                        <attribute name="ofm_pde_cultural_training" />
                        <attribute name="ofm_pde_inclusion_training" />
                        <attribute name="ofm_professional_development_hours" />
                        <attribute name="ofm_pto_breaks" />
                        <attribute name="ofm_quality_enhancement_factor" />
                        <attribute name="ofm_rate_scheduleid" />
                        <attribute name="ofm_schedule_number" />
                        <attribute name="ofm_sick_hours_per_fte" />
                        <attribute name="ofm_standard_dues_per_fte" />
                        <attribute name="ofm_statutory_breaks" />
                        <attribute name="ofm_supervisor_rate" />
                        <attribute name="ofm_supervisor_ratio" />
                        <attribute name="ofm_total_fte_hours_per_year" />
                        <attribute name="ofm_vacation_hours_per_fte" />
                        <attribute name="ofm_wage_grid_markup" />
                        <attribute name="ofm_wages_ece_cost" />
                        <attribute name="ofm_wages_ece_supervisor_differential" />
                        <attribute name="ofm_wages_ecea_cost" />
                        <attribute name="ofm_wages_ite_cost" />
                        <attribute name="ofm_wages_ite_sne_supervisor_differential" />
                        <attribute name="ofm_wages_ra_cost" />
                        <attribute name="ofm_wages_ra_supervisor_differential" />
                        <attribute name="ofm_wcb" />
                        <attribute name="ofm_weeks_in_a_year" />
                        <attribute name="ownerid" />
                        <attribute name="owningbusinessunit" />
                        <attribute name="statuscode" />
                        <link-entity name="ofm_funding_rate" from="ofm_rate_schedule" to="ofm_rate_scheduleid" alias="FR">
                          <attribute name="ofm_caption" />
                          <attribute name="ofm_nonhr_funding_envelope" />
                          <attribute name="ofm_rate" />
                          <attribute name="ofm_spaces_max" />
                          <attribute name="ofm_spaces_min" />
                          <attribute name="ofm_step" />
                          <attribute name="statecode" />
                          <attribute name="ofm_ownership" />
                          <attribute name="ofm_funding_rateid" />
                          <attribute name="ofm_rate_schedule" />
                          <attribute name="statuscode" />
                          <attribute name="createdon" />
                          <attribute name="modifiedon" />
                          <attribute name="ownerid" />
                          <attribute name="owningbusinessunit" />
                        </link-entity>
                        <link-entity name="ofm_cclr_ratio" from="ofm_rate_schedule" to="ofm_rate_scheduleid" alias="CCLR">
                          <attribute name="ofm_caption" />
                          <attribute name="ofm_cclr_ratioid" />
                          <attribute name="ofm_fte_min_ece" />
                          <attribute name="ofm_fte_min_ecea" />
                          <attribute name="ofm_fte_min_ite" />
                          <attribute name="ofm_fte_min_ra" />
                          <attribute name="ofm_group_size" />
                          <attribute name="ofm_licence_group" />
                          <attribute name="ofm_licence_mapping" />
                          <attribute name="ofm_rate_schedule" />
                          <attribute name="ofm_spaces_max" />
                          <attribute name="ofm_spaces_min" />
                          <attribute name="statuscode" />
                          <attribute name="createdon" />
                          <attribute name="modifiedon" />
                          <attribute name="ofm_order_number" />
                          <attribute name="ownerid" />
                          <attribute name="owningbusinessunit" />
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_rate_schedules?$select=&$expand=ofm_rateschedule_fundingrate($select=),ofm_rateschedule_cclr($select=)&$filter=(ofm_rateschedule_fundingrate/any(o1:(o1/ofm_funding_rateid%20ne%20null)))%20and%20(ofm_rateschedule_cclr/any(o2:(o2/ofm_cclr_ratioid%20ne%20null)))
                         """;

            return requestUri;
        }
    }

    private string FundingRequestUri
    {
        get
        {
            // For reference only
            var fetchXml = $"""
                            <fetch distinct="true" no-lock="true">
                              <entity name="ofm_funding">
                                <attribute name="createdby" />
                                <attribute name="createdon" />
                                <attribute name="modifiedby" />
                                <attribute name="modifiedon" />
                                <attribute name="ofm_application" />
                                <attribute name="ofm_apply_duplicate_caretypes_condition" />
                                <attribute name="ofm_apply_room_split_condition" />
                                <attribute name="ofm_end_date" />
                                <attribute name="ofm_envelope_administrative" />
                                <attribute name="ofm_envelope_administrative_pf" />
                                <attribute name="ofm_envelope_administrative_proj" />
                                <attribute name="ofm_envelope_facility" />
                                <attribute name="ofm_envelope_facility_pf" />
                                <attribute name="ofm_envelope_facility_proj" />
                                <attribute name="ofm_envelope_grand_total" />
                                <attribute name="ofm_envelope_grand_total_pf" />
                                <attribute name="ofm_envelope_grand_total_proj" />
                                <attribute name="ofm_envelope_hr_benefits" />
                                <attribute name="ofm_envelope_hr_benefits_pf" />
                                <attribute name="ofm_envelope_hr_benefits_proj" />
                                <attribute name="ofm_envelope_hr_employerhealthtax" />
                                <attribute name="ofm_envelope_hr_employerhealthtax_pf" />
                                <attribute name="ofm_envelope_hr_employerhealthtax_proj" />
                                <attribute name="ofm_envelope_hr_prodevexpenses" />
                                <attribute name="ofm_envelope_hr_prodevexpenses_pf" />
                                <attribute name="ofm_envelope_hr_prodevexpenses_proj" />
                                <attribute name="ofm_envelope_hr_prodevhours" />
                                <attribute name="ofm_envelope_hr_prodevhours_pf" />
                                <attribute name="ofm_envelope_hr_prodevhours_proj" />
                                <attribute name="ofm_envelope_hr_total" />
                                <attribute name="ofm_envelope_hr_total_pf" />
                                <attribute name="ofm_envelope_hr_total_proj" />
                                <attribute name="ofm_envelope_hr_wages_paidtimeoff" />
                                <attribute name="ofm_envelope_hr_wages_paidtimeoff_pf" />
                                <attribute name="ofm_envelope_hr_wages_paidtimeoff_proj" />
                                <attribute name="ofm_envelope_operational" />
                                <attribute name="ofm_envelope_operational_pf" />
                                <attribute name="ofm_envelope_operational_proj" />
                                <attribute name="ofm_envelope_programming" />
                                <attribute name="ofm_envelope_programming_pf" />
                                <attribute name="ofm_envelope_programming_proj" />
                                <attribute name="ofm_facility" />
                                <attribute name="ofm_funding_envelope" />
                                <attribute name="ofm_funding_number" />
                                <attribute name="ofm_fundingid" />
                                <attribute name="ofm_manual_intervention" />
                                <attribute name="ofm_new_allocation_date" />
                                <attribute name="ofm_rate_schedule" />
                                <attribute name="ofm_start_date" />
                                <attribute name="ofm_version_number" />
                                <attribute name="ownerid" />
                                <attribute name="owningbusinessunit" />
                                <attribute name="statecode" />
                                <attribute name="statuscode" />
                                <filter>
                                  <condition attribute="ofm_fundingid" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                                </filter>
                                <link-entity name="ofm_space_allocation" from="ofm_funding" to="ofm_fundingid" link-type="outer" alias="NewAllocation">
                                  <attribute name="createdon" />
                                  <attribute name="ofm_adjusted_allocation" />
                                  <attribute name="ofm_caption" />
                                  <attribute name="ofm_cclr_ratio" />
                                  <attribute name="ofm_default_allocation" />
                                  <attribute name="ofm_funding" />
                                  <attribute name="ofm_order_number" />
                                  <attribute name="ownerid" />
                                  <attribute name="owningbusinessunit" />
                                  <attribute name="statuscode" />
                                  <link-entity name="ofm_cclr_ratio" from="ofm_cclr_ratioid" to="ofm_cclr_ratio" alias="CCLR">
                                    <attribute name="ofm_caption" />
                                    <attribute name="ofm_fte_min_ece" />
                                    <attribute name="ofm_fte_min_ecea" />
                                    <attribute name="ofm_fte_min_ite" />
                                    <attribute name="ofm_fte_min_ra" />
                                    <attribute name="ofm_group_size" />
                                    <attribute name="ofm_licence_group" />
                                    <attribute name="ofm_licence_mapping" />
                                    <attribute name="ofm_order_number" />
                                    <attribute name="ofm_rate_schedule" />
                                    <attribute name="ofm_spaces_max" />
                                    <attribute name="ofm_spaces_min" />
                                    <attribute name="statuscode" />
                                  </link-entity>
                                </link-entity>
                                <link-entity name="account" from="accountid" to="ofm_facility" link-type="inner" alias="Facility">
                                  <attribute name="accountid" />
                                  <attribute name="accountnumber" />
                                  <attribute name="name" />
                                  <link-entity name="ofm_licence" from="ofm_facility" to="accountid" link-type="outer" alias="Licences">
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
                                    <link-entity name="ofm_licence_detail" from="ofm_licence" to="ofm_licenceid" link-type="outer" alias="CoreService">
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
                                </link-entity>
                                <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" link-type="outer" alias="App">
                                  <attribute name="ofm_application" />
                                  <attribute name="ofm_applicationid" />
                                  <attribute name="ofm_summary_ownership" />
                                  <attribute name="ofm_application_type" />
                                  <attribute name="ofm_costs_applicable_fee" />
                                  <attribute name="ofm_costs_facility_type" />
                                  <attribute name="ofm_costs_furniture_equipment" />
                                  <attribute name="ofm_costs_maintenance_repairs" />
                                  <attribute name="ofm_costs_mortgage" />
                                  <attribute name="ofm_costs_property_insurance" />
                                  <attribute name="ofm_costs_property_municipal_tax" />
                                  <attribute name="ofm_costs_rent_lease" />
                                  <attribute name="ofm_costs_strata_fee" />
                                  <attribute name="ofm_costs_supplies" />
                                  <attribute name="ofm_costs_upkeep_labour_supplies" />
                                  <attribute name="ofm_costs_utilities" />
                                  <attribute name="ofm_costs_year_facility_costs" />
                                  <attribute name="ofm_costs_yearly_operating_costs" />
                                  <attribute name="ofm_funding_number_base" />
                                </link-entity>
                                <link-entity name="ofm_rate_schedule" from="ofm_rate_scheduleid" to="ofm_rate_schedule" alias="RateSchedule">
                                  <attribute name="ofm_rate_scheduleid" />
                                  <attribute name="ofm_schedule_number" />
                                </link-entity>
                              </entity>
                            </fetch>
                            """;

            var requestUri = $"""                                
                             ofm_fundings?$select=_createdby_value,createdon,_modifiedby_value,modifiedon,_ofm_application_value,ofm_apply_duplicate_caretypes_condition,ofm_apply_room_split_condition,ofm_end_date,ofm_envelope_administrative,ofm_envelope_administrative_pf,ofm_envelope_administrative_proj,ofm_envelope_facility,ofm_envelope_facility_pf,ofm_envelope_facility_proj,ofm_envelope_grand_total,ofm_envelope_grand_total_pf,ofm_envelope_grand_total_proj,ofm_envelope_hr_benefits,ofm_envelope_hr_benefits_pf,ofm_envelope_hr_benefits_proj,ofm_envelope_hr_employerhealthtax,ofm_envelope_hr_employerhealthtax_pf,ofm_envelope_hr_employerhealthtax_proj,ofm_envelope_hr_prodevexpenses,ofm_envelope_hr_prodevexpenses_pf,ofm_envelope_hr_prodevexpenses_proj,ofm_envelope_hr_prodevhours,ofm_envelope_hr_prodevhours_pf,ofm_envelope_hr_prodevhours_proj,ofm_envelope_hr_total,ofm_envelope_hr_total_pf,ofm_envelope_hr_total_proj,ofm_envelope_hr_wages_paidtimeoff,ofm_envelope_hr_wages_paidtimeoff_pf,ofm_envelope_hr_wages_paidtimeoff_proj,ofm_envelope_operational,ofm_envelope_operational_pf,ofm_envelope_operational_proj,ofm_envelope_programming,ofm_envelope_programming_pf,ofm_envelope_programming_proj,_ofm_facility_value,ofm_funding_envelope,ofm_funding_number,ofm_fundingid,ofm_manual_intervention,ofm_new_allocation_date,_ofm_rate_schedule_value,ofm_start_date,ofm_version_number,_ownerid_value,_owningbusinessunit_value,statecode,statuscode&$expand=ofm_funding_spaceallocation($select=createdon,ofm_adjusted_allocation,ofm_caption,_ofm_cclr_ratio_value,ofm_default_allocation,_ofm_funding_value,ofm_order_number,_ownerid_value,_owningbusinessunit_value,statuscode;$expand=ofm_cclr_ratio($select=ofm_caption,ofm_fte_min_ece,ofm_fte_min_ecea,ofm_fte_min_ite,ofm_fte_min_ra,ofm_group_size,ofm_licence_group,ofm_licence_mapping,ofm_order_number,_ofm_rate_schedule_value,ofm_spaces_max,ofm_spaces_min,statuscode)),ofm_facility($select=accountid,accountnumber,name;$expand=ofm_facility_licence($select=createdon,ofm_accb_providerid,ofm_ccof_facilityid,ofm_ccof_organizationid,_ofm_facility_value,ofm_health_authority,ofm_licence,ofm_tdad_funding_agreement_number,_ownerid_value,statuscode;$expand=ofm_licence_licencedetail($select=createdon,ofm_care_type,ofm_enrolled_spaces,_ofm_licence_value,ofm_licence_detail,ofm_licence_spaces,ofm_licence_type,ofm_operation_hours_from,ofm_operation_hours_to,ofm_operational_spaces,ofm_overnight_care,ofm_week_days,ofm_weeks_in_operation,_ownerid_value,statuscode))),ofm_application($select=ofm_application,ofm_applicationid,ofm_summary_ownership,ofm_application_type,ofm_costs_applicable_fee,ofm_costs_facility_type,ofm_costs_furniture_equipment,ofm_costs_maintenance_repairs,ofm_costs_mortgage,ofm_costs_property_insurance,ofm_costs_property_municipal_tax,ofm_costs_rent_lease,ofm_costs_strata_fee,ofm_costs_supplies,ofm_costs_upkeep_labour_supplies,ofm_costs_utilities,ofm_costs_year_facility_costs,ofm_costs_yearly_operating_costs,ofm_funding_number_base),ofm_rate_schedule($select=ofm_rate_scheduleid,ofm_schedule_number)&$filter=(ofm_fundingid eq '{_fundingId!.Value}') and (ofm_facility/accountid ne null) and (ofm_rate_schedule/ofm_rate_scheduleid ne null)
                             """;

            return requestUri;
        }
    }

    private string SpacesAllocationRequestUri
    {
        get
        {
            // For reference only
            var fetchXml = $"""
                            <fetch distinct="true" no-lock="true">
                              <entity name="ofm_space_allocation">
                                <attribute name="createdon" />
                                <attribute name="ofm_adjusted_allocation" />
                                <attribute name="ofm_caption" />
                                <attribute name="ofm_cclr_ratio" />
                                <attribute name="ofm_default_allocation" />
                                <attribute name="ofm_funding" />
                                <attribute name="ofm_order_number" />
                                <attribute name="ofm_space_allocationid" />
                                <attribute name="ownerid" />
                                <attribute name="owningbusinessunit" />
                                <attribute name="statuscode" />
                                <filter>
                                  <condition attribute="ofm_funding" operator="eq" value="00000000-0000-0000-0000-000000000000" />
                                </filter>
                                <link-entity name="ofm_cclr_ratio" from="ofm_cclr_ratioid" to="ofm_cclr_ratio" alias="CCLR">
                                  <attribute name="ofm_caption" />
                                  <attribute name="ofm_cclr_ratioid" />
                                  <attribute name="ofm_group_size" />
                                  <attribute name="ofm_licence_group" />
                                  <attribute name="ofm_licence_mapping" />
                                </link-entity>
                              </entity>
                            </fetch>
                            """;

            var requestUri = $"""                                
                             ofm_space_allocations?$select=createdon,ofm_adjusted_allocation,ofm_caption,_ofm_cclr_ratio_value,ofm_default_allocation,_ofm_funding_value,ofm_order_number,ofm_space_allocationid,_ownerid_value,_owningbusinessunit_value,statuscode&$expand=ofm_cclr_ratio($select=ofm_caption,ofm_cclr_ratioid,ofm_group_size,ofm_licence_group,ofm_licence_mapping)&$filter=(_ofm_funding_value eq '{_fundingId!.Value}') and (ofm_cclr_ratio/ofm_cclr_ratioid ne null) 
                             """;

            return requestUri;
        }
    }

    #endregion

    public async Task<IEnumerable<RateSchedule>> LoadRateSchedulesAsync()
    {
        var localdata = await _dataService.FetchDataAsync(RateScheduleRequestUri, "RateSchedules");
        var deserializedData = localdata.Data.Deserialize<List<RateSchedule>>(Setup.s_readOptionsRelaxed);

        return await Task.FromResult(deserializedData!); ;
    }

    public async Task<Funding> GetFundingByIdAsync(Guid id)
    {
        _fundingId = id;
        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, FundingRequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            //_logger.LogError(CustomLogEvent.Process, "Failed to query the requests with the server error {responseBody}", responseBody);

            //return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            //if (currentValue?.AsArray().Count == 0)
            //{
            //    _logger.LogInformation(CustomLogEvent.Process, "No records found");
            //}

            d365Result = currentValue!;
        }

        var deserializedData = d365Result.Deserialize<List<Funding>>(Setup.s_writeOptionsForLogs);

        return await Task.FromResult(deserializedData!.First());
    }

    public async Task<IEnumerable<SpaceAllocation>> GetSpacesAllocationByFundingIdAsync(Guid id)
    {
        _fundingId = id;
        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, SpacesAllocationRequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            //_logger.LogError(CustomLogEvent.Process, "Failed to query the requests with the server error {responseBody}", responseBody);

            //return await Task.FromResult([]);
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                //_logger.LogInformation(CustomLogEvent.Process, "No records found");
            }

            d365Result = currentValue!;
        }

        var deserializedData = d365Result.Deserialize<IEnumerable<SpaceAllocation>>(Setup.s_writeOptionsForLogs);

        return await Task.FromResult(deserializedData!);
    }

    public async Task<bool> SaveFundingAmounts(FundingResult fundingResult)
    {
        FundingAmounts fm = fundingResult.FundingAmounts;
        var updateFundingUrl = @$"ofm_fundings({_fundingId})";
        var newfundingAmounts = new
        {
            //Projected Base Amounts
            ofm_envelope_hr_total_proj = fm.HRTotal_Projected,
            ofm_envelope_hr_wages_paidtimeoff_proj = fm.HRWagesPaidTimeOff_Projected,
            ofm_envelope_hr_benefits_proj = fm.HRBenefits_Projected,
            ofm_envelope_hr_employerhealthtax_proj = fm.HREmployerHealthTax_Projected,
            ofm_envelope_hr_prodevhours_proj = fm.HRProfessionalDevelopmentHours_Projected,
            ofm_envelope_hr_prodevexpenses_proj = fm.HRProfessionalDevelopmentExpenses_Projected,

            ofm_envelope_programming_proj = fm.NonHRProgramming_Projected,
            ofm_envelope_administrative_proj = fm.NonHRAdmistrative_Projected,
            ofm_envelope_operational_proj = fm.NonHROperational_Projected,
            ofm_envelope_facility_proj = fm.NonHRFacility_Projected,

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

        var requestBody = JsonSerializer.Serialize(newfundingAmounts);
        var response = await _d365webapiservice.SendPatchRequestAsync(_appUserService.AZSystemAppUser, updateFundingUrl, requestBody);

        //_logger.LogDebug(CustomLogEvent.Process, "Update Funding Record {fundingId}", fundingId);

        return await Task.FromResult(true);
    }

    public async Task<bool> SaveDefaultSpacesAllocation(IEnumerable<ofm_space_allocation> spacesAllocation)
    {
        List<HttpRequestMessage> updateRequests = [];
        foreach (var space in spacesAllocation)
        {
            var data = new JsonObject {
                { "ofm_default_allocation", space.ofm_default_allocation ?? 0 }
            };

            updateRequests.Add(new UpdateRequest(new EntityReference(ofm_space_allocation.EntityLogicalName, space.ofm_space_allocationid), data));
        }

        var batchResult = await _d365webapiservice.SendBatchMessageAsync(_appUserService.AZSystemAppUser, updateRequests, null);

        if (batchResult.Errors.Any())
        {
            await Task.FromResult(false);
        }

        return await Task.FromResult(true);
    }
}