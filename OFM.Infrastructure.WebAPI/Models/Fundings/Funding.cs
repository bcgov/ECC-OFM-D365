using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public class Funding: ofm_funding
{
    //public string odataetag { get; set; }
    //public string _createdby_value { get; set; }
    //public DateTime createdon { get; set; }
    //public string _modifiedby_value { get; set; }
    //public DateTime modifiedon { get; set; }
    //public string _ofm_application_value { get; set; }
    //public bool ofm_apply_duplicate_caretypes_condition { get; set; }
    //public bool ofm_apply_room_split_condition { get; set; }
    //public DateTime ofm_end_date { get; set; }
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
    //public string _ofm_facility_value { get; set; }
    //public object ofm_funding_envelope { get; set; }
    //public string ofm_funding_number { get; set; }
    //public string ofm_fundingid { get; set; }
    //public bool ofm_manual_intervention { get; set; }
    //public object ofm_new_allocation_date { get; set; }
    //public string _ofm_rate_schedule_value { get; set; }
    //public DateTime ofm_start_date { get; set; }
    //public int ofm_version_number { get; set; }
    //public string _ownerid_value { get; set; }
    //public string _owningbusinessunit_value { get; set; }
    //public int statecode { get; set; }
    //public int statuscode { get; set; }
    public new object[] ofm_funding_spaceallocation { get; set; }
    public new Ofm_Facility? ofm_facility { get; set; }
    public new Ofm_Application? ofm_application { get; set; }
    public new Ofm_Rate_Schedule? ofm_rate_schedule { get; set; }
}

public class Ofm_Facility
{
    public string accountid { get; set; }
    public string accountnumber { get; set; }
    public string name { get; set; }
    public Ofm_Facility_Licence[] ofm_facility_licence { get; set; }
}

public class Ofm_Facility_Licence
{
    public DateTime createdon { get; set; }
    public object ofm_accb_providerid { get; set; }
    public string ofm_ccof_facilityid { get; set; }
    public string ofm_ccof_organizationid { get; set; }
    public string _ofm_facility_value { get; set; }
    public int ofm_health_authority { get; set; }
    public string ofm_licence { get; set; }
    public object ofm_tdad_funding_agreement_number { get; set; }
    public string _ownerid_value { get; set; }
    public int statuscode { get; set; }
    public string ofm_licenceid { get; set; }
    public CoreService[] ofm_licence_licencedetail { get; set; }
}

public class Ofm_Licence_Licencedetail
{
    public DateTime createdon { get; set; }
    public int ofm_care_type { get; set; }
    public int ofm_enrolled_spaces { get; set; }
    public string _ofm_licence_value { get; set; }
    public string ofm_licence_detail { get; set; }
    public int ofm_licence_spaces { get; set; }
    public int ofm_licence_type { get; set; }
    public DateTime ofm_operation_hours_from { get; set; }
    public DateTime ofm_operation_hours_to { get; set; }
    public int ofm_operational_spaces { get; set; }
    public int ofm_overnight_care { get; set; }
    public string ofm_week_days { get; set; }
    public int ofm_weeks_in_operation { get; set; }
    public string _ownerid_value { get; set; }
    public int statuscode { get; set; }
    public string ofm_licence_detailid { get; set; }
}

public class Ofm_Application
{
    public string ofm_application { get; set; }
    public string ofm_applicationid { get; set; }
}

public class Ofm_Rate_Schedule
{
    public string odataetag { get; set; }
    public string _createdby_value { get; set; }
    public object _createdonbehalfby_value { get; set; }
    public string _modifiedby_value { get; set; }
    public object _modifiedonbehalfby_value { get; set; }
    public string _ownerid_value { get; set; }
    public string _owningbusinessunit_value { get; set; }
    public object _owningteam_value { get; set; }
    public string _owninguser_value { get; set; }
    public string _transactioncurrencyid_value { get; set; }
    public DateTime createdon { get; set; }
    public int exchangerate { get; set; }
    public object importsequencenumber { get; set; }
    public DateTime modifiedon { get; set; }
    public int ofm_average_benefit_load { get; set; }
    public string ofm_caption { get; set; }
    public float ofm_cpp { get; set; }
    public int ofm_cultural_hours_per_fte { get; set; }
    public int ofm_days_in_a_week { get; set; }
    public int ofm_eht_maximum_cost_for_profit { get; set; }
    public int ofm_eht_maximum_cost_for_profit_base { get; set; }
    public int ofm_eht_maximum_cost_not_for_profit { get; set; }
    public int ofm_eht_maximum_cost_not_for_profit_base { get; set; }
    public int ofm_eht_minimum_cost_for_profit { get; set; }
    public int ofm_eht_minimum_cost_for_profit_base { get; set; }
    public float ofm_ei { get; set; }
    public int ofm_elf_educational_programming_cap_fte_year { get; set; }
    public int ofm_elf_educational_programming_cap_fte_year_base { get; set; }
    public int ofm_elf_hours_per_fte { get; set; }
    public DateTime ofm_end_date { get; set; }
    public float ofm_extended_benefits { get; set; }
    public float ofm_for_profit_eht_over_1_5m { get; set; }
    public float ofm_for_profit_eht_over_500k { get; set; }
    public float ofm_hours_per_day { get; set; }
    public int ofm_inclusion_hours_per_fte { get; set; }
    public int ofm_licenced_childcare_cap_per_fte_per_year { get; set; }
    public int ofm_licenced_childcare_cap_per_fte_per_year_base { get; set; }
    public int ofm_licensed_childcare_hours_per_fte { get; set; }
    public decimal ofm_no_of_sick_days { get; set; }
    public decimal ofm_no_of_stat_holidays { get; set; }
    public int ofm_no_of_vacation_days { get; set; }
    public float ofm_not_for_profit_eht_over_1_5m { get; set; }
    public decimal ofm_parent_fee_per_day_ft { get; set; }
    public decimal ofm_parent_fee_per_day_ft_base { get; set; }
    public decimal ofm_parent_fee_per_day_pt { get; set; }
    public decimal ofm_parent_fee_per_day_pt_base { get; set; }
    public decimal ofm_parent_fee_per_month_ft { get; set; }
    public decimal ofm_parent_fee_per_month_ft_base { get; set; }
    public decimal ofm_parent_fee_per_month_pt { get; set; }
    public decimal ofm_parent_fee_per_month_pt_base { get; set; }
    public decimal ofm_pde_cultural_training { get; set; }
    public decimal ofm_pde_cultural_training_base { get; set; }
    public decimal ofm_pde_inclusion_training { get; set; }
    public int ofm_pde_inclusion_training_base { get; set; }
    public decimal ofm_professional_development_hours { get; set; }
    public float ofm_pto_breaks { get; set; }
    public decimal ofm_quality_enhancement_factor { get; set; }
    public string ofm_rate_scheduleid { get; set; }
    public Ofm_Rateschedule_Cclr[] ofm_rateschedule_cclr { get; set; }
    public string ofm_rateschedule_cclrodatanextLink { get; set; }
    public Ofm_Rateschedule_Fundingrate[] ofm_rateschedule_fundingrate { get; set; }
    public string ofm_rateschedule_fundingrateodatanextLink { get; set; }
    public int ofm_schedule_number { get; set; }
    public decimal ofm_sick_hours_per_fte { get; set; }
    public decimal ofm_standard_dues_per_fte { get; set; }
    public decimal ofm_standard_dues_per_fte_base { get; set; }
    public DateTime ofm_start_date { get; set; }
    public decimal ofm_statutory_breaks { get; set; }
    public string ofm_supervisor_rate { get; set; }
    public decimal ofm_supervisor_ratio { get; set; }
    public decimal ofm_total_fte_hours_per_year { get; set; }
    public decimal ofm_vacation_hours_per_fte { get; set; }
    public int ofm_wage_grid_markup { get; set; }
    public float ofm_wages_ece_cost { get; set; }
    public float ofm_wages_ece_cost_base { get; set; }
    public decimal ofm_wages_ece_supervisor_differential { get; set; }
    public decimal ofm_wages_ece_supervisor_differential_base { get; set; }
    public decimal ofm_wages_ecea_cost { get; set; }
    public decimal ofm_wages_ecea_cost_base { get; set; }
    public float ofm_wages_ite_cost { get; set; }
    public float ofm_wages_ite_cost_base { get; set; }
    public decimal ofm_wages_ite_sne_supervisor_differential { get; set; }
    public decimal ofm_wages_ite_sne_supervisor_differential_base { get; set; }
    public decimal ofm_wages_ra_cost { get; set; }
    public decimal ofm_wages_ra_cost_base { get; set; }
    public decimal ofm_wages_ra_supervisor_differential { get; set; }
    public decimal ofm_wages_ra_supervisor_differential_base { get; set; }
    public float ofm_wcb { get; set; }
    public int ofm_weeks_in_a_year { get; set; }
    public object overriddencreatedon { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public int timezoneruleversionnumber { get; set; }
    public object utcconversiontimezonecode { get; set; }
    public int versionnumber { get; set; }
}

public class Ofm_Rateschedule_Fundingrate
{
    public string odataetag { get; set; }
    public string _ofm_rate_schedule_value { get; set; }
    public string _ownerid_value { get; set; }
    public string _owningbusinessunit_value { get; set; }
    public string _transactioncurrencyid_value { get; set; }
    public DateTime createdon { get; set; }
    public DateTime modifiedon { get; set; }
    public string ofm_caption { get; set; }
    public string ofm_funding_rateid { get; set; }
    public int ofm_nonhr_funding_envelope { get; set; }
    public int ofm_ownership { get; set; }
    public float ofm_rate { get; set; }
    public int ofm_spaces_max { get; set; }
    public int ofm_spaces_min { get; set; }
    public int ofm_step { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}
