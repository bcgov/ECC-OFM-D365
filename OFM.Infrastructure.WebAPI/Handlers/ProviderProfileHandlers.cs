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
    /// <summary>
    /// Get the Provider Profile by a Business BCeID
    /// </summary>
    /// <param name="d365WebApiService"></param>
    /// <param name="appUserService"></param>
    /// <param name="timeProvider"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="userName" example="BCeIDTest"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public static async Task<Results<BadRequest<string>, NotFound<string>, UnauthorizedHttpResult, ProblemHttpResult, Ok<ProviderProfile>>> GetProfileAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        string userName,
        string? userId)
    {
        var logger = loggerFactory.CreateLogger(LogCategory.ProviderProfile);
        using (logger.BeginScope("ScopeProvider: {userId}", userId))
        {
            //logger.LogDebug(CustomLogEvent.ProviderProfile, "Getting provider profile in D365 for userName:{userName}/userId:{userId}", userName, userId);

            if (string.IsNullOrEmpty(userName)) return TypedResults.BadRequest("The userName is required.");

            var startTime = timeProvider.GetTimestamp();

            // For Reference Only
            var fetchXml = $"""
                    <fetch version="1.0" mapping="logical" distinct="true" no-lock="true">
                      <entity name="contact">
                        <attribute name="ofm_first_name" />
                        <attribute name="ofm_last_name" />
                        <attribute name="ccof_userid" />
                        <attribute name="ccof_username" />
                        <attribute name="contactid" />
                        <attribute name="emailaddress1" />
                        <attribute name="ofm_portal_role_id" />
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
                          <attribute name="ofm_is_expense_authority" />
                          <attribute name="ofm_portal_access" />
                          <attribute name="ofm_bceid_facilityid" />
                          <attribute name="statecode" />
                          <attribute name="statuscode" />
                          <filter>
                            <condition attribute="statuscode" operator="eq" value="1" />
                          </filter>
                          <link-entity name="account" from="accountid" to="ofm_facility" link-type="inner" alias="Facility">
                            <attribute name="accountid" />
                            <attribute name="accountnumber" />
                            <attribute name="ccof_accounttype" />
                            <attribute name="ofm_program" />
                            <attribute name="statecode" />
                            <attribute name="statuscode" />
                            <attribute name="name" />
                            <attribute name="ofm_ccof_requirement" />
                            <attribute name="ofm_program_start_date" />
                            <attribute name="ofm_unionized" />
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
                          <attribute name="ofm_program" />
                          <filter>
                            <condition attribute="statuscode" operator="eq" value="1" />
                          </filter>
                        </link-entity>
                        <link-entity name="ofm_portal_role" from="ofm_portal_roleid" to="ofm_portal_role_id" link-type="inner" alias="ab">
                          <attribute name="ofm_portal_role_number" />
                        </link-entity>
                      </entity>
                    </fetch>
                    """;
     
            var requestUri = $"""
                         contacts?$select=ofm_first_name,ofm_last_name,ccof_userid,ccof_username,contactid,emailaddress1,_ofm_portal_role_id_value,telephone1&$expand=ofm_facility_business_bceid($select=_ofm_bceid_value,_ofm_facility_value,ofm_name,ofm_portal_access,ofm_is_expense_authority,ofm_bceid_facilityid,statecode,statuscode;$expand=ofm_facility($select=accountid,accountnumber,ccof_accounttype,ofm_program,statecode,statuscode,name,ofm_ccof_requirement,ofm_program_start_date,ofm_unionized);$filter=(statuscode eq 1)),parentcustomerid_account($select=accountid,accountnumber,ccof_accounttype,name,statecode,statuscode,ofm_program;$filter=(statuscode eq 1)),ofm_portal_role_id($select=ofm_portal_role_number)&$filter=(ccof_userid eq '{userId}' or ccof_username eq '{userName}') and (statuscode eq 1) and (ofm_portal_role_id/ofm_portal_roleid ne null)
                         """;

            logger.LogDebug(CustomLogEvent.ProviderProfile, "Getting provider profile with query {requestUri}", requestUri);

            var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZPortalAppUser, requestUri);

            var endTime = timeProvider.GetTimestamp();

            if (!response.IsSuccessStatusCode)
            {
                var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>() ?? new ProblemDetails();

                #region Logging

                var traceId = string.Empty;
                if (problemDetails?.Extensions.TryGetValue("traceId", out var traceIdValue) == true)
                    traceId = traceIdValue?.ToString();

                using (logger.BeginScope($"ScopeProvider: {userId}"))
                {
                    logger.LogWarning(CustomLogEvent.ProviderProfile, "API Failure: Failed to retrieve profile for {userName}. Response message: {response}. TraceId: {traceId}. " +
                        "Finished in {timer.ElapsedMilliseconds} miliseconds.", userId, response, traceId, timeProvider.GetElapsedTime(startTime, endTime).TotalMilliseconds);
                }

                #endregion

                return TypedResults.Problem($"Failed to Retrieve profile: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);

            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            #region Validation

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    logger.LogDebug(CustomLogEvent.ProviderProfile, "User not found.");

                    return TypedResults.NotFound($"User not found.");
                }
                if (currentValue?.AsArray().Count > 1)
                {
                    logger.LogDebug(CustomLogEvent.ProviderProfile, "Multiple profiles found.");

                    return TypedResults.Unauthorized();
                }
                d365Result = currentValue!;
            }

            var serializedProfile = JsonSerializer.Deserialize<IEnumerable<D365Contact>>(d365Result!.ToString());

            if (serializedProfile!.First().parentcustomerid_account is null ||
                serializedProfile!.First().ofm_facility_business_bceid is null ||
                serializedProfile!.First().ofm_facility_business_bceid!.Length == 0)
            {
                logger.LogDebug(CustomLogEvent.ProviderProfile, "Organization or facility permissions not found.");
                return TypedResults.Unauthorized();
            }

            #endregion

            ProviderProfile portalProfile = new();
            portalProfile.MapProviderProfile(serializedProfile!);
            if (string.IsNullOrEmpty(portalProfile.ccof_userid) && !string.IsNullOrEmpty(userId))
            {
                // Update the contact in Dataverse with the userid
                var statement = @$"contacts({portalProfile.contactid})";
                var requestBody = JsonSerializer.Serialize(new { ccof_userid = userId });

                var userResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZPortalAppUser, statement, requestBody);
                if (!userResponse.IsSuccessStatusCode)
                {
                    logger.LogError("Failed to update the userId for {userName}. Response: {response}.", userName, userResponse);
                }
               
                portalProfile.ccof_userid = userId; // Add the UseId to the return payload to help with validation on the portal
            }

            //logger.LogDebug(CustomLogEvent.ProviderProfile, "Return provider profile {portalProfile}", portalProfile);
            logger.LogInformation(CustomLogEvent.ProviderProfile, "Querying provider profile finished in {totalElapsedTime} miliseconds", timeProvider.GetElapsedTime(startTime, endTime).TotalMilliseconds);

            return TypedResults.Ok(portalProfile);
        }
    }
}