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

public static class ProviderProfilesHandlers
{
    public static async Task<Results<BadRequest<string>, NotFound<string>, ProblemHttpResult, Ok<ProviderProfile>>> GetProfileAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        TimeProvider timeProvider,
        ILogger<string> logger,
        string? userId,
        string? userName)
    {
        if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(userName)) return TypedResults.BadRequest("A userId or a userName is required.");

        var startTime = timeProvider.GetTimestamp();

        // For Reference Only
        var fetchXml = $"""
                    <fetch version="1.0" mapping="logical" distinct="true" no-lock="true">
                      <entity name="contact">
                        <attribute name="ofm_first_name" />
                        <attribute name="ofm_last_name" />
                        <attribute name="ofm_portal_role" />
                        <attribute name="ccof_userid" />
                        <attribute name="ccof_username" />
                        <attribute name="contactid" />
                        <attribute name="emailaddress1" />
                        <attribute name="ofm_is_primary_contact" />
                        <attribute name="telephone1" />
                        <filter type="or">
                          <condition attribute="ccof_userid" operator="eq" value="" />
                          <condition attribute="ccof_username" operator="eq" value="" />
                        </filter>
                        <filter type="and">
                          <condition attribute="statuscode" operator="eq" value="1" />
                        </filter>
                        <link-entity name="ofm_bceid_facility" from="ofm_bceid" to="contactid" link-type="outer" alias="Permission">
                          <attribute name="ofm_bceid" />
                          <attribute name="ofm_facility" />
                          <attribute name="ofm_name" />
                          <attribute name="ofm_portal_access" />
                          <attribute name="ofm_bceid_facilityid" />
                          <attribute name="statecode" />
                          <attribute name="statuscode" />
                          <filter>
                            <condition attribute="statuscode" operator="eq" value="1" />
                          </filter>
                          <link-entity name="account" from="accountid" to="ofm_facility" link-type="outer" alias="Facility">
                            <attribute name="accountid" />
                            <attribute name="accountnumber" />
                            <attribute name="ccof_accounttype" />
                            <attribute name="statecode" />
                            <attribute name="statuscode" />
                            <attribute name="name" />
                            <filter>
                              <condition attribute="statuscode" operator="eq" value="1" />
                            </filter>
                          </link-entity>
                        </link-entity>
                        <link-entity name="account" from="accountid" to="parentcustomerid" link-type="outer" alias="Organization">
                          <attribute name="accountid" />
                          <attribute name="accountnumber" />
                          <attribute name="ccof_accounttype" />
                          <attribute name="name" />
                          <attribute name="statecode" />
                          <attribute name="statuscode" />
                          <filter>
                            <condition attribute="statuscode" operator="eq" value="1" />
                          </filter>
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

        var requestUri = $"""
                         contacts?$select=ofm_first_name,ofm_last_name,ofm_portal_role,ccof_userid,ccof_username,contactid,emailaddress1,ofm_is_primary_contact,telephone1&$expand=ofm_facility_business_bceid($select=_ofm_bceid_value,_ofm_facility_value,ofm_name,ofm_portal_access,ofm_bceid_facilityid,statecode,statuscode;$expand=ofm_facility($select=accountid,accountnumber,ccof_accounttype,statecode,statuscode,name;$filter=(statuscode eq 1));$filter=(statuscode eq 1)),parentcustomerid_account($select=accountid,accountnumber,ccof_accounttype,name,statecode,statuscode;$filter=(statuscode eq 1))&$filter=(ccof_userid eq '{userId}' or ccof_username eq '{userName}') and (statuscode eq 1)
                         """;
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZPortalAppUser, requestUri);

        var endTime = timeProvider.GetTimestamp();

        if (response.IsSuccessStatusCode)
        {
            var jsonDom = await response.Content.ReadFromJsonAsync<JsonObject>();

            #region Validation

            JsonNode d365Result = string.Empty;
            if (jsonDom?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0) { return TypedResults.NotFound($"User not found."); }
                d365Result = currentValue!;
            }

            var serializedProfile = JsonSerializer.Deserialize<IEnumerable<D365Contact>>(d365Result!.ToString());

            if (serializedProfile!.First().parentcustomerid_account is null ||
                serializedProfile!.First().ofm_facility_business_bceid is null)
                return TypedResults.NotFound($"No profile found.");

            if (serializedProfile!.First().ofm_facility_business_bceid!.Length == 0)
                return TypedResults.NotFound($"No permissions.");

            #endregion

            #region Logging

            using (logger.BeginScope("ScopeProfile: {userId}", userId))
            {
                logger.LogInformation("ScopeProfile: Response Time: {timer.ElapsedMilliseconds}", timeProvider.GetElapsedTime(startTime, endTime));
            }

            #endregion

            ProviderProfile portalProfile = new();
            portalProfile.MapProviderProfile(serializedProfile!);

            return TypedResults.Ok(portalProfile);
        }
        else
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

            #region Logging

            var traceId = "";
            if (problemDetails?.Extensions.TryGetValue("traceId", out var currentValue) == true)
                traceId = currentValue?.ToString();

            using (logger.BeginScope($"ScopeProfile: {userId}"))
            {
                logger.LogWarning("API Failure: Failed to Retrieve profile: {userName}. Response: {response}. TraceId: {traceId}. " +
                    "Finished in {timer.ElapsedMilliseconds} miliseconds.", userId, response, traceId, timeProvider.GetElapsedTime(startTime, endTime));
            }
            #endregion

            return TypedResults.Problem($"Failed to Retrieve profile: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
    }
}