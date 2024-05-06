using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json.Linq;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
    public string? subjectsafehtml { get; set; }
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

public record D365CommunicationType
{
    public string? ofm_communication_typeid { get; set; }
    public Int16? ofm_communication_type_number { get; set; }
}

public record Payment_File_Exchange
{
    public string ofm_batch_number { get; set; }
    public string ofm_oracle_batch_name { get; set; }
    public string ofm_payment_file_exchangeid { get; set; }


}

public class Payment_Line : ofm_payment
{
    public required string ofm_paymentid { get; set; }
    public required decimal ofm_amount { get; set; }
     public required DateTime ofm_effective_date { get; set; }
    public required string _ofm_fiscal_year_value { get; set; }
    public required string _ofm_funding_value { get; set; }
    public required int ofm_invoice_line_number { get; set; }
    public string? ofm_remittance_message { get; set; }
    public required string ofm_invoice_number { get; set; }
    public required string ofm_siteid { get; set; }
    public required string ofm_supplierid { get; set; }
    public required int ofm_payment_method { get; set; }
    [property: JsonPropertyName("ofm_fiscal_year.ofm_financial_year")]
    public required string ofm_financial_year { get; set; }
    [property: JsonPropertyName("ofm_funding.ofm_funding_number")]
    public required string ofm_funding_number { get; set; }
    [property: JsonPropertyName("ofm_facility.name")]
    public required string accountname { get; set; }
    [property: JsonPropertyName("ofm_facility.accountnumber")]
    public required string accountnumber { get; set; }
    // public ofm_fiscal_year ofm_fiscal_year { get { return new ofm_fiscal_year { Id= new Guid(_ofm_fiscal_year_value)}; }  }


}

#region External Parameters

#endregion