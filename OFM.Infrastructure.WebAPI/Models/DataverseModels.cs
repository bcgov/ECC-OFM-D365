using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OFM.Infrastructure.WebAPI.Models;

#region BCeID objects

public record FacilityPermission
{
    public bool ofm_portal_access { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public Facility facility { get; set; }
}

public record Facility
{
    public string accountid { get; set; }
    public string accountnumber { get; set; }
    public string name { get; set; }
    public int ccof_accounttype { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public record BCeID
{
    public string contactid { get; set; }
    public string ccof_userid { get; set; }
    public string ccof_username { get; set; }
    public string emailaddress1 { get; set; }
    public string ofm_first_name { get; set; }
    public string ofm_last_name { get; set; }
    public Organization organization { get; set; }
    public string ofm_portal_role { get; set; }
    public Dictionary<int, string> portal_role_list
    {
        get
        {
            var PermissionCollections = new Dictionary<int, string>
            {
                [1] = "Admin",
                [2] = "Read Only"
            };
            var permissions = ofm_portal_role.Split(',');
            var result = new Dictionary<int, string>();
            foreach (var permission in permissions)
                result.Add(Convert.ToInt32(permission), PermissionCollections[Convert.ToInt32(permission)]);

            return result;
        }
    }
    public List<FacilityPermission> facility_permission { get; set; }
}

public record Organization
{
    public string accountid { get; set; }
    public string accountnumber { get; set; }
    public int ccof_accounttype { get; set; }
    public string name { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

#endregion

#region Temp Objects

public record BCeIDFacility
{
    public string odataetag { get; set; }
    public string ofm_bceid_facilityid { get; set; }
    public string _ofm_facility_value { get; set; }
    public string ofm_name { get; set; }
    public bool ofm_portal_access { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public Ofm_Facility ofm_facility { get; set; }
    public Ofm_Bceid ofm_bceid { get; set; }
}

public record Ofm_Facility
{
    public string accountid { get; set; }
    public string accountnumber { get; set; }
    public string name { get; set; }
    public int ccof_accounttype { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public record Ofm_Bceid
{
    public string ccof_userid { get; set; }
    public string ccof_username { get; set; }
    public string contactid { get; set; }
    public string emailaddress1 { get; set; }
    public string ofm_first_name { get; set; }
    public string ofm_last_name { get; set; }
    public string ofm_portal_role { get; set; }
    public Parentcustomerid_Account parentcustomerid_account { get; set; }
}

public record Parentcustomerid_Account
{
    public string accountid { get; set; }
    public string accountnumber { get; set; }
    public int ccof_accounttype { get; set; }
    public string name { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

#endregion