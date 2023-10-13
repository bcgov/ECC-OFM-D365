using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OFM.Infrastructure.WebAPI.Handlers;

public static class ProviderProfileHandlers
{
    public static async Task<Results<BadRequest<string>, NotFound<string>, ProblemHttpResult, Ok<BCeID>>> GetProfileAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        TimeProvider timeProvider,
        ILogger<string> logger,
        string userId)
    {
        if (string.IsNullOrEmpty(userId)) return TypedResults.BadRequest("Invalid Request");

        var startTime = timeProvider.GetTimestamp();

        // For refrence only
        var fetchXml = $"""
                    <?xml version="1.0" encoding="utf-16"?>
                    <fetch distinct="true" no-lock="true">
                      <entity name="ofm_bceid_facility">
                        <attribute name="ofm_bceid_facilityid" />
                        <attribute name="ofm_facility" />
                        <attribute name="ofm_name" />
                        <attribute name="ofm_portal_access" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <link-entity name="account" from="accountid" to="ofm_facility" link-type="inner" alias="Facility">
                          <attribute name="accountid" />
                          <attribute name="accountnumber" />
                          <attribute name="name" />
                          <attribute name="ccof_accounttype" />
                          <attribute name="statecode" />
                          <attribute name="statuscode" />
                        </link-entity>
                        <link-entity name="contact" from="contactid" to="ofm_bceid" link-type="inner" alias="BCeID">
                          <attribute name="ccof_userid" />
                          <attribute name="ccof_username" />
                          <attribute name="contactid" />
                          <attribute name="emailaddress1" />
                          <attribute name="ofm_first_name" />
                          <attribute name="ofm_last_name" />
                          <filter>
                            <condition attribute="ccof_userid" operator="eq" value="" />
                          </filter>
                          <link-entity name="account" from="accountid" to="parentcustomerid" link-type="outer" alias="Organization">
                            <attribute name="accountid" />
                            <attribute name="accountnumber" />
                            <attribute name="ccof_accounttype" />
                            <attribute name="name" />
                            <attribute name="statecode" />
                            <attribute name="statuscode" />
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

        var requestUri = $"ofm_bceid_facilities?$select=ofm_bceid_facilityid,_ofm_facility_value,ofm_name,ofm_portal_access,statecode,statuscode&$expand=ofm_facility($select=accountid,accountnumber,name,ccof_accounttype,statecode,statuscode),ofm_bceid($select=ccof_userid,ccof_username,contactid,emailaddress1,ofm_first_name,ofm_last_name,ofm_portal_role;$expand=parentcustomerid_account($select=accountid,accountnumber,ccof_accounttype,name,statecode,statuscode))&$filter=(ofm_facility/accountid%20ne%20null)%20and%20(ofm_bceid/ccof_userid%20eq%20%27{userId}%27)";

        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZPortalAppUser, requestUri);

        var endTime = timeProvider.GetTimestamp();

        if (response.IsSuccessStatusCode)
        {
            var jsonDom = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode result = string.Empty;
            if (jsonDom?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0) { return TypedResults.NotFound($"User not found: {userId}"); }
                result = currentValue!;
            }

            //using (logger.BeginScope("ScopeProfile: {userName}", userName))
            //{
            //    logger.LogInformation("ScopeProfile: Response Time: {timer.ElapsedMilliseconds}", timeProvider.GetElapsedTime(startTime, endTime));
            //}

            var resultDeserialized = JsonSerializer.Deserialize<List<BCeIDFacility>>(result!.ToString());

            var myBCeID = new BCeID();

            var facilityAccess = new List<FacilityPermission>();

            foreach (var facility in resultDeserialized!)
            {
                myBCeID.contactid = facility.ofm_bceid.contactid;
                myBCeID.ccof_userid = facility.ofm_bceid.ccof_userid;
                myBCeID.ccof_username = facility.ofm_bceid.ccof_username;
                myBCeID.ofm_first_name = facility.ofm_bceid.ofm_first_name;
                myBCeID.ofm_last_name = facility.ofm_bceid.ofm_last_name;
                myBCeID.ofm_portal_role = facility.ofm_bceid.ofm_portal_role;

                myBCeID.organization = new Organization
                {
                    accountid = facility.ofm_bceid.parentcustomerid_account.accountid,
                    accountnumber = facility.ofm_bceid.parentcustomerid_account.accountnumber,
                    name = facility.ofm_bceid.parentcustomerid_account.name,
                    ccof_accounttype = facility.ofm_bceid.parentcustomerid_account.ccof_accounttype,
                    statecode = facility.ofm_bceid.parentcustomerid_account.statecode,
                    statuscode = facility.ofm_bceid.parentcustomerid_account.statuscode
                };

                facilityAccess.Add(new FacilityPermission
                {
                    facility = new Facility
                    {
                        accountid = facility.ofm_facility.accountid,
                        accountnumber = facility.ofm_facility.accountnumber,
                        name = facility.ofm_facility.name,
                    },
                    ofm_portal_access = true
                });
            }
            myBCeID.facility_permission = facilityAccess;

            return TypedResults.Ok(myBCeID);
        }
        else
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            var traceId = "";
            if (problemDetails?.Extensions.TryGetValue("traceId", out var currentValue) == true)
                traceId = currentValue?.ToString();

            //using (logger.BeginScope($"ScopeProfile: {userName}"))
            //{
            //    logger.LogWarning("API Failure: Failed to Retrieve profile: {userName}. Response: {response}. TraceId: {traceId}. " +
            //        "Finished in {timer.ElapsedMilliseconds} miliseconds.", userName, response, traceId, timeProvider.GetElapsedTime(startTime, endTime));
            return TypedResults.Problem($"Failed to Retrieve profile: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            //}
        }
    }
}