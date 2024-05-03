using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.Text.Json.Serialization;

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
    public int? ofm_program { get; set; }
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
    public PortalRole? role { get; set; }
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
       
            role = new PortalRole
            {
                ofm_portal_roleid = firstContact.ofm_portal_role_id?.ofm_portal_roleid,
                ofm_portal_role_number = firstContact.ofm_portal_role_id?.ofm_portal_role_number

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
                        statuscode = facility.statuscode,
                        ofm_program=facility.ofm_program,
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
    public PortalRole? ofm_portal_role_id { get; set; }
}

public record PortalRole
{
    public Guid? ofm_portal_roleid{ get; set; }
    public string? ofm_portal_role_number { get; set; }
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
    public int? ofm_program { get; set; }
}

#endregion

public record D365Template
{
    public string? title { get; set; }
    public string? safehtml { get; set; }
     public string? body { get; set; }
    public string? templateid { get; set; }
    public string? templatecode { get; set; }
    
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

    public Email_Activity_Parties[] email_activity_parties { get; set; }

    public bool IsCompleted
    {
        get
        {
            return (statecode == 1);
        }
    }
}

public record D365Organization_Account
{
    public string? accountid { get; set; }
    public string? name { get; set; }
    public string? ofm_incorporation_number { get; set; }
    public string? ofm_business_number { get; set; }
    public bool? ofm_bypass_bc_registry_good_standing { get; set; }  
    public int statecode { get; set; }
    public Guid _primarycontactid_value { get; set; }
    public Guid _ofm_primarycontact_value { get; set; }


}

public record D365StandingHistory
{
    public string? ofm_standing_historyid { get; set; }
    public string? _ofm_organization_value { get; set; }
    public int? ofm_good_standing_status { get; set; }
    public DateTime? ofm_start_date { get; set; }
    public DateTime? ofm_end_date { get; set; }
    public DateTime? ofm_validated_on { get; set; }
    public decimal? ofm_duration { get; set; }
    public int? ofm_no_counter { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public record FileMapping
{
    public required string ofm_subject { get; set; }
    public required string ofm_description { get; set; }
    public required string ofm_extension { get; set; }
    public required decimal ofm_file_size { get; set; }
    public required string entity_name_set { get; set; }
    public required string regardingid { get; set; }
    public required string ofm_category { get; set; }
}

public record Email_Activity_Parties
{
    public int? participationtypemask { get; set; }
    public string? _partyid_value { get; set; }
    public string? _activityid_value { get; set; }
    public string? activitypartyid { get; set; }
    public string? addressused { get; set; }
}



public record Payment_File_Exchange
{
    public string ofm_batch_number { get; set; }
    public string ofm_oracle_batch_name { get; set; }
    public string ofm_payment_file_exchangeid { get; set; }
    

}

public class Payment_Line :ofm_payment
{
    public string? ofm_paymentid { get; set; }
    public decimal? ofm_amount { get; set; }
    public decimal? ofm_amount_paid { get; set; }
    public DateTime? ofm_effective_date { get; set; }
    public string _ofm_fiscal_year_value { get; set; }
    public string? _ofm_funding_value { get; set; }
    public int? ofm_invoice_line_number { get; set; }
    public DateTime? ofm_paid_date { get; set; }
    public string? ofm_remittance_message { get; set; }
    public string? ofm_invoice_number { get; set; }
    public string? ofm_siteid { get; set; }
    public string? ofm_supplierid { get; set; }
    public int? ofm_payment_method { get; set; }
    [property: JsonPropertyName("ofm_fiscal_year.ofm_financial_year")]
    public string? ofm_financial_year { get; set; }
    [property: JsonPropertyName("ofm_funding.ofm_funding_number")]
    public string? ofm_funding_number { get; set; }
    [property: JsonPropertyName("ofm_facility.name")]
    public string? accountname { get; set; }
    [property: JsonPropertyName("ofm_facility.accountnumber")]
    public string? accountnumber { get; set; }
    // public ofm_fiscal_year ofm_fiscal_year { get { return new ofm_fiscal_year { Id= new Guid(_ofm_fiscal_year_value)}; }  }


}


#region Funding


public class RateSchedule : ofm_rate_schedule
{
    public new decimal? ofm_transport_reimbursement_per_km { get; set; }
    public new decimal? ofm_greater_than20_spaces_lease_cap_per_month { get; set; }
    public new decimal? ofm_facilities_with_9_or_less_spaces_inclusio { get; set; }
    public new decimal? ofm_facilities_with_20_or_more_spaces_ip { get; set; }
    public new decimal? ofm_wages_ra_cost { get; set; }
    public new decimal? ofm_licenced_childcare_cap_per_fte_per_year { get; set; }
    public new decimal? ofm_wages_ecea_cost { get; set; }
    public new decimal? ofm_facilities_with_9_or_less_spaces_ip { get; set; }
    public new decimal? ofm_wages_ite_cost { get; set; }
    public new decimal? ofm_wages_ece_cost { get; set; }
    public new decimal? ofm_elf_educational_programming_cap_fte_year { get; set; }
    public new decimal? ofm_parent_fee_per_day_pt { get; set; }
    public new decimal? ofm_wages_ra_supervisor_differential { get; set; }
    public new decimal? ofm_standard_dues_per_fte { get; set; }
    public new decimal? ofm_parent_fee_per_month_ft { get; set; }
    public new decimal? ofm_facilities_with_10_to_19_spaces_inclusion { get; set; }
    public new decimal? ofm_less_than20_spaces_lease_cap_per_month { get; set; }
    public new decimal? ofm_wages_ite_sne_supervisor_differential { get; set; }
    public new decimal? ofm_parent_fee_per_day_ft { get; set; }
    public new decimal? ofm_facilities_with_20_or_more_spaces_inclusi { get; set; }
    public new decimal? ofm_facilities_with_10_to_19_spaces_ip { get; set; }
    public new decimal? ofm_parent_fee_per_month_pt { get; set; }
    public new decimal? ofm_facilities_with_10_to_19_spaces_ip_base { get; set; }
    public new decimal? ofm_wages_ece_supervisor_differential { get; set; }
    public FundingRate[] ofm_rateschedule_fundingrate { get; set; }
}

public class FundingRate : ofm_funding_rate
{
    public new decimal? ofm_rate { get; set; }
}
public class PaymentApplication : ofm_application
{
    
    public string? ofm_applicationid { get; set; }
    public string? _ofm_facility_value { get; set; }
    public new Funding? ofm_funding { get; set; }
}
public class Funding : ofm_funding
{
    public string? ofm_fundingid { get; set; }
    public new decimal? ofm_envelope_grand_total { get; set; }
}
public class Application : ofm_application
{
    public string? ofm_applicationid { get; set; }
    public string? _ofm_facility_value { get; set; }
    public new Funding? ofm_funding { get; set; }
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

    public new Facility? ofm_facility { get; set; }
}

public class FacilityLicence: ofm_licence
{
    public new LicenceDetail[]? ofm_licence_licencedetail { get; set; }
}

public class LicenceDetail: ofm_licence_detail
{
    public new string ofm_week_days { get; set; }
}

public class Supplementary : ofm_allowance
{
    public new decimal? ofm_funding_amount { get; set; }
    public new decimal? ofm_transport_estimated_monthly_km { get; set; }
    public new decimal? ofm_transport_monthly_lease { get; set; }
    public new decimal? ofm_transport_odometer { get; set; }
    public SupplementarySchedule ofm_supplementary_schedule { get; set; }
    public string _ofm_application_value { get; set; }
    //public bool ofm_transport_lease { get; set; }
}

public class SupplementarySchedule : ofm_supplementary_schedule
{
    public new decimal? ofm_indigenous_10_to_19_spaces { get; set; }
    public new decimal? ofm_indigenous_ge_20_spaces { get; set; }
    public new decimal? ofm_indigenous_le_9_spaces { get; set; }
    public new decimal? ofm_needs_10_to_19_spaces { get; set; }
    public new decimal? ofm_needs_ge_20_spaces { get; set; }
    public new decimal? ofm_needs_le_9_spaces { get; set; }
    public new decimal? ofm_sqw_caps_for_centers { get; set; }
    public new decimal? ofm_sqw_caps_for_homebased { get; set; }
    public new decimal? ofm_transport_ge_20_spaces_lease_cap_month { get; set; }
    public new decimal? ofm_transport_less_20_spaces_lease_cap_month { get; set; }
    public new decimal? ofm_transport_reimbursement_rate_per_km { get; set; }
}

#endregion