using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OFM.Infrastructure.WebAPI.Models;

#region BCeID Json objects for Portal

public record FacilityPermission
{
    public bool ofm_portal_access { get; set; }
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

public class BusinessBCeID
{
    public string? contactid { get; set; }
    public string? ccof_userid { get; set; }
    public string? ccof_username { get; set; }
    public string? emailaddress1 { get; set; }
    public string? ofm_first_name { get; set; }
    public string? ofm_last_name { get; set; }
    public Organization? organization { get; set; }
    public string? ofm_portal_role { get; set; }
    public List<FacilityPermission>? facility_permission { get; set; }

    public void MapBusinessBCeIDFacilityPermissions(IEnumerable<BCeIDFacility> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Count() == 0) throw new ArgumentException($"Must have at least one facility permission! {nameof(BCeIDFacility)}");

        var facilityPermissions = new List<FacilityPermission>();
        var firstPermission = permissions.First();

        contactid = firstPermission.ofm_bceid.contactid;
        ccof_userid = firstPermission.ofm_bceid.ccof_userid;
        ccof_username = firstPermission.ofm_bceid.ccof_username;
        ofm_first_name = firstPermission.ofm_bceid.ofm_first_name;
        ofm_last_name = firstPermission.ofm_bceid.ofm_last_name;
        emailaddress1 = firstPermission.ofm_bceid.emailaddress1;
        ofm_portal_role = firstPermission.ofm_bceid.ofm_portal_role;

        organization = new Organization
        {
            accountid = firstPermission.ofm_bceid.parentcustomerid_account.accountid,
            accountnumber = firstPermission.ofm_bceid.parentcustomerid_account.accountnumber,
            name = firstPermission.ofm_bceid.parentcustomerid_account.name,
            ccof_accounttype = firstPermission.ofm_bceid.parentcustomerid_account.ccof_accounttype,
            statecode = firstPermission.ofm_bceid.parentcustomerid_account.statecode,
            statuscode = firstPermission.ofm_bceid.parentcustomerid_account.statuscode
        };

        using (var enumerator = permissions.GetEnumerator())
            while (enumerator.MoveNext()) {
                facilityPermissions.Add(new FacilityPermission
                {
                    facility = new Facility
                    {
                        accountid = enumerator.Current.ofm_facility.accountid,
                        accountnumber = enumerator.Current.ofm_facility.accountnumber,
                        name = enumerator.Current.ofm_facility.name,
                    },
                    ofm_portal_access = enumerator.Current.ofm_portal_access
                });

            }
        facility_permission = facilityPermissions;
    }
}

#endregion

#region Temp BCeID related objects for serialization

public record BCeIDFacility
{
    public string? odataetag { get; set; }
    public string? ofm_bceid_facilityid { get; set; }
    public string? _ofm_facility_value { get; set; }
    public string? ofm_name { get; set; }
    public bool ofm_portal_access { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public required Ofm_Facility ofm_facility { get; set; }
    public required Ofm_Bceid ofm_bceid { get; set; }
}

public record Ofm_Facility
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public string? name { get; set; }
    public int? ccof_accounttype { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public record Ofm_Bceid
{
    public string? ccof_userid { get; set; }
    public string? ccof_username { get; set; }
    public string? contactid { get; set; }
    public string? emailaddress1 { get; set; }
    public string? ofm_first_name { get; set; }
    public string? ofm_last_name { get; set; }
    public string? ofm_portal_role { get; set; }
    public required Parentcustomerid_Account parentcustomerid_account { get; set; }
}

public record Parentcustomerid_Account
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public int? ccof_accounttype { get; set; }
    public string? name { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

#endregion