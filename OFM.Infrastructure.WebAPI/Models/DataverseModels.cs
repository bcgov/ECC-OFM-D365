using System;

namespace OFM.Infrastructure.WebAPI.Models;

#region Contact-related objects for Portal

public record FacilityPermission
{
    public required string ofm_bceid_facilityid { get; set; }
    public bool? ofm_portal_access { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public required Facility facility { get; set; }
}

public record Facility
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public string? name { get; set; }
    public int ccof_accounttype { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public record Organization
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
    public Organization? organization { get; set; }
    public string? ofm_portal_role { get; set; }
    public bool? ofm_is_primary_contact { get; set; }
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
        ofm_is_primary_contact = firstContact.ofm_is_primary_contact;

        organization = new Organization
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
                    facility = new Facility
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
    public string? ofm_portal_role { get; set; }
    public string? ccof_userid { get; set; }
    public string? ccof_username { get; set; }
    public string? contactid { get; set; }
    public string? emailaddress1 { get; set; }
    public string? telephone1 { get; set; }
    public bool? ofm_is_primary_contact { get; set; }
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



public record Ofm_Rate_Schedule
{
    public DateTime ofm_end_date { get; set; }
    public double ofm_parent_fee_per_day_ft { get; set; }
    public double ofm_parent_fee_per_day_pt { get; set; }
    public double ofm_parent_fee_per_month_ft { get; set; }
    public double ofm_parent_fee_per_month_pt { get; set; }
    public DateTime ofm_start_date { get; set; }
    public int statecode { get; set; }
    public Ofm_Fundingrate[] ofm_rateschedule_fundingrate { get; set; }
}

public record Ofm_Fundingrate
{
    public int ofm_nonhr_funding_envelope { get; set; }
    public float ofm_rate { get; set; }
    public int ofm_spaces_max { get; set; }
    public int ofm_spaces_min { get; set; }
    public int ofm_step { get; set; }
    public int ofm_ownership { get; set; }
    public int statecode { get; set; }
}



public record Ofm_Application
{
    public double ofm_costs_furniture_equipment { get; set; }
    public double ofm_costs_yearly_operating_costs { get; set; }
    public int statuscode { get; set; }
    public double ofm_costs_year_facility_costs { get; set; }
    public int ofm_summary_declaration_a_status { get; set; }
    public string ofm_application { get; set; }
    public double ofm_costs_strata_fee { get; set; }
    public int ofm_provider_type { get; set; }
    public int ofm_staff_ec_educator_ft { get; set; }
    public string ofm_summary_signing_authority { get; set; }
    public int ofm_staff_ec_educator_pt { get; set; }
    public string _ofm_facility_value { get; set; }
    public double ofm_costs_applicable_fee { get; set; }
    public double ofm_costs_property_insurance_base { get; set; }
    public double ofm_costs_maintenance_repairs { get; set; }
    public DateTime modifiedon { get; set; }
    public string ofm_funding_agreement_number { get; set; }
    public int ofm_costs_facility_type { get; set; }
    public int ofm_staff_ec_educator_assistant_pt { get; set; }
    public string ofm_applicationid { get; set; }
    public int ofm_summary_declaration_b_status { get; set; }
    public int ofm_staff_responsible_adult_ft { get; set; }
    public int ofm_application_type { get; set; }
    public int ofm_staff_ec_educator_assistant_ft { get; set; }
    public int ofm_staff_responsible_adult_pt { get; set; }
    public string ofm_funding_number_base { get; set; }
    public int statecode { get; set; }
    public int ofm_staff_infant_ec_educator_pt { get; set; }
    public double ofm_costs_utilities { get; set; }
    public double ofm_costs_rent_lease { get; set; }
    public double ofm_costs_mortgage { get; set; }
    public double ofm_costs_upkeep_labour_supplies { get; set; }
    public double ofm_costs_property_municipal_tax { get; set; }
    public string ofm_summary_submittedby { get; set; }
    public double ofm_costs_property_insurance { get; set; }
    public int ofm_summary_ownership { get; set; }
    public int ofm_staff_infant_ec_educator_ft { get; set; }
    public int versionnumber { get; set; }
    public Ofm_Licence_Application[] ofm_licence_application { get; set; }
    public Ofm_Application_Funding[] ofm_application_funding { get; set; }
}

public record Ofm_Licence_Application
{
    public string _ofm_application_value { get; set; }
    public string ofm_licenceid { get; set; }
    public string ofm_accb_providerid { get; set; }
    public string ofm_ccof_facilityid { get; set; }
    public string ofm_ccof_organizationid { get; set; }
    public int ofm_health_authority { get; set; }
    public string ofm_licence { get; set; }
    public string ofm_tdad_funding_agreement_number { get; set; }
    public int statuscode { get; set; }
    public Ofm_Licence_Licencedetail[] ofm_licence_licencedetail { get; set; }
}

public record Ofm_Licence_Licencedetail
{
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

public record Ofm_Application_Funding
{
    public string ofm_fundingid { get; set; }
}


#region External Parameters

#endregion