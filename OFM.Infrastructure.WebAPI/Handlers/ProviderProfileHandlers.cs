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
    public static async Task<Results<BadRequest<string>, NotFound<string>, ProblemHttpResult, Ok<BusinessBCeID>>> GetProfileAsync(
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

            JsonNode d365Result = string.Empty;
            if (jsonDom?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0) { return TypedResults.NotFound($"User not found: {userId}"); }
                d365Result = currentValue!;
            }

            using (logger.BeginScope("ScopeProfile: {userId}", userId))
            {
                logger.LogInformation("ScopeProfile: Response Time: {timer.ElapsedMilliseconds}", timeProvider.GetElapsedTime(startTime, endTime));
            }

            var serializedProfile = JsonSerializer.Deserialize<IEnumerable<BCeIDFacility>>(d365Result!.ToString());

            BusinessBCeID portalProfile = new();
            portalProfile.MapBusinessBCeIDFacilityPermissions(serializedProfile!);

            return TypedResults.Ok(portalProfile);
        }
        else
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            var traceId = "";
            if (problemDetails?.Extensions.TryGetValue("traceId", out var currentValue) == true)
                traceId = currentValue?.ToString();

            using (logger.BeginScope($"ScopeProfile: {userId}"))
            {
                logger.LogWarning("API Failure: Failed to Retrieve profile: {userName}. Response: {response}. TraceId: {traceId}. " +
                    "Finished in {timer.ElapsedMilliseconds} miliseconds.", userId, response, traceId, timeProvider.GetElapsedTime(startTime, endTime));
                return TypedResults.Problem($"Failed to Retrieve profile: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }
        }
    }
}