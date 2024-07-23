using ECC.Core.DataContext;
using System.Text.Json.Serialization;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public class Funding : ofm_funding
{
    public new decimal? ofm_envelope_administrative { get; set; }
    public new decimal? ofm_envelope_administrative_pf { get; set; }
    public new decimal? ofm_envelope_administrative_proj { get; set; }
    public new decimal? ofm_envelope_facility { get; set; }
    public new decimal? ofm_envelope_facility_pf { get; set; }
    public new decimal? ofm_envelope_facility_proj { get; set; }
    public new decimal? ofm_envelope_grand_total { get; set; }
    public new decimal? ofm_envelope_grand_total_pf { get; set; }
    public new decimal? ofm_envelope_grand_total_proj { get; set; }
    public new decimal? ofm_envelope_hr_benefits { get; set; }
    public new decimal? ofm_envelope_hr_benefits_pf { get; set; }
    public new decimal? ofm_envelope_hr_benefits_proj { get; set; }
    public new decimal? ofm_envelope_hr_employerhealthtax { get; set; }
    public new decimal? ofm_envelope_hr_employerhealthtax_pf { get; set; }
    public new decimal? ofm_envelope_hr_employerhealthtax_proj { get; set; }
    public new decimal? ofm_envelope_hr_prodevexpenses { get; set; }
    public new decimal? ofm_envelope_hr_prodevexpenses_pf { get; set; }
    public new decimal? ofm_envelope_hr_prodevexpenses_proj { get; set; }
    public new decimal? ofm_envelope_hr_prodevhours { get; set; }
    public new decimal? ofm_envelope_hr_prodevhours_pf { get; set; }
    public new decimal? ofm_envelope_hr_prodevhours_proj { get; set; }
    public new decimal? ofm_envelope_hr_total { get; set; }
    public new decimal? ofm_envelope_hr_total_pf { get; set; }
    public new decimal? ofm_envelope_hr_total_proj { get; set; }
    public new decimal? ofm_envelope_hr_wages_paidtimeoff { get; set; }
    public new decimal? ofm_envelope_hr_wages_paidtimeoff_pf { get; set; }
    public new decimal? ofm_envelope_hr_wages_paidtimeoff_proj { get; set; }
    public new decimal? ofm_envelope_operational { get; set; }
    public new decimal? ofm_envelope_operational_pf { get; set; }
    public new decimal? ofm_envelope_operational_proj { get; set; }
    public new decimal? ofm_envelope_programming { get; set; }
    public new decimal? ofm_envelope_programming_pf { get; set; }
    public new decimal? ofm_envelope_programming_proj { get; set; }
    public Guid? _ofm_provider_approver_value { get; set; }
    public new SpaceAllocation[]? ofm_funding_spaceallocation { get; set; }
    public new Facility? ofm_facility { get; set; }
    public new Application? ofm_application { get; set; }
    public new RateSchedule? ofm_rate_schedule { get; set; }
}

public class Facility : Account
{
    public new Licence[]? ofm_facility_licence { get; set; }
}

public class Licence : ofm_licence
{
    public new LicenceDetail[]? ofm_licence_licencedetail { get; set; }
}

public class SpaceAllocation : ofm_space_allocation
{
    public new CCLRRatio? ofm_cclr_ratio { get; set; }
}

public class CCLRRatio : ofm_cclr_ratio
{
    public new string? ofm_licence_mapping { get; set; }
}

public class RateSchedule : ofm_rate_schedule
{
    public new decimal? ofm_wages_ra_cost { get; set; }
    public new decimal? ofm_licenced_childcare_cap_per_fte_per_year { get; set; }
    public new decimal? ofm_wages_ecea_cost { get; set; }
    public new decimal? ofm_wages_ite_cost { get; set; }
    public new decimal? ofm_wages_ece_cost { get; set; }
    public new decimal? ofm_elf_educational_programming_cap_fte_year { get; set; }
    public new decimal? ofm_parent_fee_per_day_pt { get; set; }
    public new decimal? ofm_standard_dues_per_fte { get; set; }
    public new decimal? ofm_parent_fee_per_month_ft { get; set; }
    public new decimal? ofm_parent_fee_per_day_ft { get; set; }
    public new decimal? ofm_parent_fee_per_month_pt { get; set; }
    public new FundingRate[]? ofm_rateschedule_fundingrate { get; set; }
    public new CCLRRatio[]? ofm_rateschedule_cclr { get; set; }
    public new decimal? ofm_average_benefit_load { get; set; }
    public new decimal? ofm_cpp { get; set; }
    public new decimal? ofm_eht_maximum_cost_for_profit { get; set; }
    public new decimal? ofm_eht_maximum_cost_not_for_profit { get; set; }
    public new decimal? ofm_eht_minimum_cost_for_profit { get; set; }
    public new decimal? ofm_ei { get; set; }
    public new decimal? ofm_extended_benefits { get; set; }
    public new decimal? ofm_for_profit_eht_over_1_5m { get; set; }
    public new decimal? ofm_for_profit_eht_over_500k { get; set; }
    public new decimal? ofm_hours_per_day { get; set; }
    public new decimal? ofm_not_for_profit_eht_over_1_5m { get; set; }
    public new decimal? ofm_pde_cultural_training { get; set; }
    public new decimal? ofm_pde_inclusion_training { get; set; }
    public new decimal? ofm_pto_breaks { get; set; }
    public new decimal? ofm_quality_enhancement_factor { get; set; }
    public new decimal? ofm_sick_hours_per_fte { get; set; }
    public new decimal? ofm_licensed_childcare_hours_per_fte { get; set; }
    public new decimal? ofm_statutory_breaks { get; set; }
    public new decimal? ofm_total_fte_hours_per_year { get; set; }
    public new decimal? ofm_vacation_hours_per_fte { get; set; }
    public new decimal? ofm_wcb { get; set; }
    public new decimal? ofm_supervisor_rate { get; set; }
}

public class FundingRate : ofm_funding_rate
{
    public new decimal? ofm_rate { get; set; }
}

public class Application : ofm_application
{
    public new decimal? ofm_costs_furniture_equipment { get; set; }
    public new decimal? ofm_costs_yearly_operating_costs { get; set; }
    public new decimal? ofm_costs_year_facility_costs { get; set; }
    public new decimal? ofm_costs_applicable_fee { get; set; }
    public new decimal? ofm_costs_property_insurance_base { get; set; }
    public new decimal? ofm_costs_maintenance_repairs { get; set; }
    public new decimal? ofm_costs_utilities { get; set; }
    public new decimal? ofm_costs_rent_lease { get; set; }
    public new decimal? ofm_costs_mortgage { get; set; }
    public new decimal? ofm_costs_upkeep_labour_supplies { get; set; }
    public new decimal? ofm_costs_property_municipal_tax { get; set; }
    public new decimal? ofm_costs_property_insurance { get; set; }
    public new decimal? ofm_costs_supplies { get; set; }
    public new decimal? ofm_costs_strata_fee { get; set; }
    public Guid? _ofm_contact_value { get; set; }
    public Guid? _ofm_expense_authority_value { get; set; }
    public Guid? _ofm_summary_submittedby_value { get; set; }
    public new D365Facility? ofm_facility { get; set; }
    public new D365Organization? ofm_organization { get; set; }
    public new SupplementaryApplication[]? ofm_application_allowance { get; set; }
    public new ExpenseApplication[]? ofm_application_expense { get; set; }
    public new Funding[]? ofm_application_funding { get; set; }
}

public class FacilityLicence : ofm_licence
{
    public new LicenceDetail[]? ofm_licence_licencedetail { get; set; }
}

public class SupplementaryApplication : ofm_allowance
{
    public new decimal? ofm_funding_amount { get; set; }
    public new decimal? ofm_transport_monthly_lease { get; set; }
    public new decimal? ofm_transport_odometer { get; set; }
    public new decimal? ofm_monthly_amount { get; set; }
    public new decimal? ofm_retroactive_amount { get; set; }
    public new SupplementarySchedule? ofm_supplementary_schedule { get; set; }
    public string? _ofm_application_value { get; set; }   
    public new Guid? ofm_allowanceid { get; set; }
    public new string? ofm_allowance_number { get; set; }
    public new DateTime? createdon { get; set; }
    public new int? statuscode { get; set; }
    public new int? ofm_renewal_term { get; set; }
    [property: JsonPropertyName("con.ofm_first_name")]
    public string? ofm_first_name { get; set; }
    [property: JsonPropertyName("con.ofm_last_name")]
    public string? ofm_last_name { get; set; }
    [property: JsonPropertyName("app.ofm_contact")]
    public Guid? _ofm_contact_value { get; set; }
    [property: JsonPropertyName("app.ofm_summary_submittedby")]
    public Guid? _ofm_summary_submittedby_value { get; set; }
    [property: JsonPropertyName("app.statuscode")]
    public int? appstatuscode { get; set; }
    [property: JsonPropertyName("app.ofm_funding_number_base")]
    public string? ofm_funding_number_base { get; set; }
    [property: JsonPropertyName("funding.statuscode")]
    public int? fundingStatusCode { get; set; }

}

public class BusinessClosure
{
    public string msdyn_businessclosureid { get; set; }
    public string msdyn_name { get; set; }
    public int msdyn_duration { get; set; }
    public string msdyn_starttime { get; set; }
    public string msdyn_endtime { get; set; }
    public string _createdby_value { get; set; }
    public int msdyn_selectedyear { get; set; }
}

public class SupplementarySchedule : ofm_supplementary_schedule
{
    public new decimal? ofm_needs_less_lower_limit_amount { get; set; }
    public new decimal? ofm_needs_between_limits_amount { get; set; }
    public new decimal? ofm_needs_greater_upper_limit_amount { get; set; }
    public new decimal? ofm_indigenous_less_lower_limit_amount { get; set; }
    public new decimal? ofm_indigenous_between_limits_amount { get; set; }
    public new decimal? ofm_indigenous_greater_upper_limit_amount { get; set; }
    public new decimal? ofm_sqw_caps_for_centers { get; set; }
    public new decimal? ofm_sqw_caps_for_homebased { get; set; }
    public new decimal? ofm_transport_ge_20_spaces_lease_cap_month { get; set; }
    public new decimal? ofm_transport_less_20_spaces_lease_cap_month { get; set; }
    public new decimal? ofm_transport_reimbursement_rate_per_km { get; set; }
} 

public record NonHRStepAction(int Step, decimal AllocatedSpaces, decimal Rate, decimal Cost, string Envelope, int MinSpaces, int MaxSpaces, ecc_Ownership Ownership);