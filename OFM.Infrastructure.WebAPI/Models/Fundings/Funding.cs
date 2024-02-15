using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public class Funding : ofm_funding
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
    public new SpaceAllocation[]? ofm_funding_spaceallocation { get; set; }
    public new Facility? ofm_facility { get; set; }
    //public new Ofm_Application? ofm_application { get; set; }
    public new Application? ofm_application { get; set; }
    public new RateSchedule? ofm_rate_schedule { get; set; }
}

public class Facility : Account
{
    public Licence[] ofm_facility_licence { get; set; }
}

public class Licence : ofm_licence
{
    public LicenceDetail[] ofm_licence_licencedetail { get; set; }
}

public class SpaceAllocation : ofm_space_allocation
{
    //public int ofm_adjusted_allocation { get; set; }
    //public string ofm_caption { get; set; }
    //public string _ofm_cclr_ratio_value { get; set; }
    //public object ofm_default_allocation { get; set; }
    //public string _ofm_funding_value { get; set; }
    //public int ofm_order_number { get; set; }
    //public string ofm_space_allocationid { get; set; }
    //public string _ownerid_value { get; set; }
    //public string _owningbusinessunit_value { get; set; }
    //public int statuscode { get; set; }
    public new CCLRRatio? ofm_cclr_ratio { get; set; }
}

//public class Ofm_Cclr_Ratio : CCLRRatio
//{
//    //public string ofm_caption { get; set; }
//    //public string ofm_cclr_ratioid { get; set; }
//    //public int ofm_group_size { get; set; }
//    //public string ofm_licence_group { get; set; }
//    //public string ofm_licence_mapping { get; set; }
//}
