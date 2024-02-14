using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System;

namespace OFM.Infrastructure.WebAPI.Models;

#region Contact-related objects for Portal

public record FacilityPermission
{
    public required string ofm_bceid_facilityid { get; set; }
    public bool? ofm_portal_access { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public required D365Facility facility { get; set; }
}

public record D365Facility
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public string? name { get; set; }
    public int ccof_accounttype { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public FacilityLicence[]? ofm_facility_licence { get; set; }
}

public record D365Organization
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public int? ccof_accounttype { get; set; }
    public string? name { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public class ProviderProfile
{
    public string? contactid { get; set; }
    public string? ccof_userid { get; set; }
    public string? ccof_username { get; set; }
    public string? emailaddress1 { get; set; }
    public string? telephone1 { get; set; }
    public string? ofm_first_name { get; set; }
    public string? ofm_last_name { get; set; }
    public D365Organization? organization { get; set; }
    public int? ofm_portal_role { get; set; }
    public IList<FacilityPermission>? facility_permission { get; set; }

    public void MapProviderProfile(IEnumerable<D365Contact> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);

        if (contacts.Count() == 0) throw new ArgumentException($"Must have at least one facility permission! {nameof(D365Contact)}");

        var facilityPermissions = new List<FacilityPermission>();
        var firstContact = contacts.First();

        contactid = firstContact.contactid;
        ccof_userid = firstContact.ccof_userid;
        ccof_username = firstContact.ccof_username;
        ofm_first_name = firstContact.ofm_first_name;
        ofm_last_name = firstContact.ofm_last_name;
        emailaddress1 = firstContact.emailaddress1;
        telephone1 = firstContact.telephone1;
        ofm_portal_role = firstContact.ofm_portal_role;


        organization = new D365Organization
        {
            accountid = firstContact!.parentcustomerid_account!.accountid!,
            accountnumber = firstContact.parentcustomerid_account.accountnumber,
            name = firstContact.parentcustomerid_account.name,
            ccof_accounttype = firstContact.parentcustomerid_account.ccof_accounttype,
            statecode = firstContact.parentcustomerid_account.statecode,
            statuscode = firstContact.parentcustomerid_account.statuscode
        };

        for (int i = 0; i < firstContact.ofm_facility_business_bceid!.Count(); i++)
        {
            if (firstContact.ofm_facility_business_bceid![i] is not null &&
                firstContact.ofm_facility_business_bceid[i].ofm_facility is not null)
            {
                var facility = firstContact.ofm_facility_business_bceid[i].ofm_facility!;
                facilityPermissions.Add(new FacilityPermission
                {
                    ofm_bceid_facilityid = firstContact.ofm_facility_business_bceid![i].ofm_bceid_facilityid!,
                    facility = new D365Facility
                    {
                        accountid = facility.accountid ?? "",
                        accountnumber = facility.accountnumber,
                        name = facility.name,
                        statecode = facility.statecode,
                        statuscode = facility.statuscode
                    },
                    ofm_portal_access = firstContact.ofm_facility_business_bceid[i].ofm_portal_access,
                    statecode = firstContact.ofm_facility_business_bceid[i].statecode,
                    statuscode = firstContact.ofm_facility_business_bceid[i].statuscode
                });
            }
        }

        facility_permission = facilityPermissions;
    }
}

#endregion

#region Temp Contact-related objects for serialization

public record D365Contact
{
    public string? odataetag { get; set; }
    public string? ofm_first_name { get; set; }
    public string? ofm_last_name { get; set; }
    public int? ofm_portal_role { get; set; }
    public string? ccof_userid { get; set; }
    public string? ccof_username { get; set; }
    public string? contactid { get; set; }
    public string? emailaddress1 { get; set; }
    public string? telephone1 { get; set; }
    public ofm_Facility_Business_Bceid[]? ofm_facility_business_bceid { get; set; }
    public Parentcustomerid_Account? parentcustomerid_account { get; set; }
}



public record Parentcustomerid_Account
{
    public string? accountid { get; set; }
    public string? accountnumber { get; set; }
    public int ccof_accounttype { get; set; }
    public string? name { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public record ofm_Facility_Business_Bceid
{
    public string? _ofm_bceid_value { get; set; }
    public string? _ofm_facility_value { get; set; }
    public string? ofm_name { get; set; }
    public bool? ofm_portal_access { get; set; }
    public string? ofm_bceid_facilityid { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public ofm_Facility? ofm_facility { get; set; }
}

public record ofm_Facility
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public int? ccof_accounttype { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public string? name { get; set; }
}

#endregion

public record D365Template
{
    public string? title { get; set; }
    public string? safehtml { get; set; }
    public string? body { get; set; }
    public string? templateid { get; set; }
}

public record D365Email
{
    public string? activityid { get; set; }
    public string? subject { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public string? sender { get; set; }
    public string? torecipients { get; set; }
    public string? _ofm_communication_type_value { get; set; }
    public int? Toparticipationtypemask { get; set; }
    public bool? isworkflowcreated { get; set; }
    public DateTime? lastopenedtime { get; set; }
    public DateTime? ofm_sent_on { get; set; }
    public DateTime? ofm_expiry_time { get; set; }
    public string? _regardingobjectid_value { get; set; }

    public Email_Activity_Parties[]? email_activity_parties { get; set; }

    public bool IsCompleted
    {
        get
        {
            return (statecode == 1);
        }
    }
}

public record FileMapping
{
    public required string ofm_subject { get; set; }
    public required string ofm_description { get; set; }
    public required string ofm_extension { get; set; }
    public required decimal ofm_file_size { get; set; }
    public required string entity_name_set { get; set; }
    public required string regardingid { get; set; }
}

public record Email_Activity_Parties
{
    public int? participationtypemask { get; set; }
    public string? _partyid_value { get; set; }
    public string? _activityid_value { get; set; }
    public string? activitypartyid { get; set; }
}

#region Fundings

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
    //public Ofm_Rateschedule_Fundingrate[] ofm_rateschedule_fundingrate { get; set; }
    public new decimal? ofm_average_benefit_load { get; set; }
    public new decimal? ofm_cpp { get; set; }
    //public int ofm_cultural_hours_per_fte { get; set; }
    //public int ofm_days_in_a_week { get; set; }
    public new decimal? ofm_eht_maximum_cost_for_profit { get; set; }
    public new decimal? ofm_eht_maximum_cost_not_for_profit { get; set; }
    public new decimal? ofm_eht_minimum_cost_for_profit { get; set; }
    public new decimal? ofm_ei { get; set; }
    //public int ofm_elf_hours_per_fte { get; set; }
    public new decimal? ofm_extended_benefits { get; set; }
    public new decimal? ofm_for_profit_eht_over_1_5m { get; set; }
    public new decimal? ofm_for_profit_eht_over_500k { get; set; }
    public new decimal? ofm_hours_per_day { get; set; }
    //public int ofm_inclusion_hours_per_fte { get; set; }
    //public int ofm_licensed_childcare_hours_per_fte { get; set; }
    //public int ofm_no_of_sick_days { get; set; }
    //public int ofm_no_of_stat_holidays { get; set; }
    //public int ofm_no_of_vacation_days { get; set; }
    public new decimal? ofm_not_for_profit_eht_over_1_5m { get; set; }
    public new decimal? ofm_pde_cultural_training { get; set; }
    public new decimal? ofm_pde_inclusion_training { get; set; }
    //public int ofm_professional_development_hours { get; set; }
    public new decimal? ofm_pto_breaks { get; set; }
    public new decimal? ofm_quality_enhancement_factor { get; set; }
    //public string ofm_rate_scheduleid { get; set; }
    //public int ofm_schedule_number { get; set; }
    public new decimal? ofm_sick_hours_per_fte { get; set; }
    public new decimal? ofm_statutory_breaks { get; set; }
    //public string ofm_supervisor_rate { get; set; }
    //public int ofm_supervisor_ratio { get; set; }
    public new decimal? ofm_total_fte_hours_per_year { get; set; }
    public new decimal? ofm_vacation_hours_per_fte { get; set; }
    //public new decimal? ofm_wage_grid_markup { get; set; }
    public new decimal? ofm_wcb { get; set; }
    //public int ofm_weeks_in_a_year { get; set; }
    public new decimal? ofm_supervisor_rate { get; set; }
}

public class FundingRate : ofm_funding_rate
{
    public decimal? ofm_rate { get; set; }
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

    public new D365Facility? ofm_facility { get; set; }
}

public class FacilityLicence : ofm_licence
{
    public new LicenceDetail[]? ofm_licence_licencedetail { get; set; }
}

//public class LicenceDetail : ofm_licence_detail
//{
//    public new string ofm_week_days { get; set; }
//}

public class SupplementaryApplication : ofm_allowance
{
    public new decimal? ofm_funding_amount { get; set; }
    public new decimal? ofm_transport_estimated_monthly_km { get; set; }
    public new decimal? ofm_transport_monthly_lease { get; set; }
    public new decimal? ofm_transport_odometer { get; set; }
    public SupplementarySchedule ofm_supplementary_schedule { get; set; }
    public string _ofm_application_value { get; set; }
}

public class SupplementarySchedule : ofm_supplementary_schedule
{
    public new decimal? ofm_sqw_caps_for_centers { get; set; }
    public new decimal? ofm_sqw_caps_for_homebased { get; set; }
    public new decimal? ofm_transport_ge_20_spaces_lease_cap_month { get; set; }
    public new decimal? ofm_transport_less_20_spaces_lease_cap_month { get; set; }
    public new decimal? ofm_transport_reimbursement_rate_per_km { get; set; }
}

#endregion

#region External Parameters

public class Ofm_Rateschedule_Fundingrate
{
    public string odataetag { get; set; }
    public string ofm_caption { get; set; }
    public int ofm_nonhr_funding_envelope { get; set; }
    public float ofm_rate { get; set; }
    public int ofm_spaces_max { get; set; }
    public int ofm_spaces_min { get; set; }
    public int ofm_step { get; set; }
    public int statecode { get; set; }
    public int ofm_ownership { get; set; }
    public string ofm_funding_rateid { get; set; }
    public string _ofm_rate_schedule_value { get; set; }
    public int statuscode { get; set; }
    public DateTime createdon { get; set; }
    public DateTime modifiedon { get; set; }
    public string _ownerid_value { get; set; }
    public string _owningbusinessunit_value { get; set; }
    public string _transactioncurrencyid_value { get; set; }
}

public class CCLRRatio : ofm_cclr_ratio
{
    //public string odataetag { get; set; }
    //public string ofm_caption { get; set; }
    //public string ofm_cclr_ratioid { get; set; }
    //public int ofm_fte_min_ece { get; set; }
    //public int ofm_fte_min_ecea { get; set; }
    //public int ofm_fte_min_ite { get; set; }
    //public int ofm_fte_min_ra { get; set; }
    //public int ofm_group_size { get; set; }
    //public string ofm_licence_group { get; set; }
    public new string? ofm_licence_mapping { get; set; }
    //public string _ofm_rate_schedule_value { get; set; }
    //public int ofm_spaces_max { get; set; }
    //public int ofm_spaces_min { get; set; }
    //public int statuscode { get; set; }
    //public DateTime createdon { get; set; }
    //public DateTime modifiedon { get; set; }
    //public int ofm_order_number { get; set; }
    //public string _ownerid_value { get; set; }
    //public string _owningbusinessunit_value { get; set; }
}

#endregion