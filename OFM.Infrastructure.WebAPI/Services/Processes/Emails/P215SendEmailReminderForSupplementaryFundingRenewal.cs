using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails
{
    public class P215SendEmailReminderForSupplementaryFundingRenewal : ID365ProcessProvider
    {

        private readonly NotificationSettings _notificationSettings;
        private readonly ID365AppUserService _appUserService;
        private readonly ID365WebApiService _d365webapiservice;
        private readonly ILogger _logger;
        private readonly TimeProvider _timeProvider;
        private ProcessData? _data;
        private string[] _communicationTypesForEmailSentToUserMailBox = [];
        private ProcessParameter? _processParams;
        private string _requestUri = string.Empty;

        public short ProcessId => Setup.Process.Emails.SendEmailReminderForSupplementaryFundingRenewalId;

        public string ProcessName => Setup.Process.Emails.SendEmailReminderForSupplementaryFundingRenewalName;

        private string RetrieveContacts
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $$"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="ofm_bceid_facility">
                    <attribute name="ofm_portal_access" />
                    <attribute name="ofm_bceid" />
                    <attribute name="ofm_is_expense_authority" />
                    <attribute name="ofm_is_additional_contact" />
                    <attribute name="ofm_bceid_facilityid" />
                    <order attribute="ofm_bceid" descending="false" />
                    <filter type="and">
                      <condition attribute="ofm_portal_access" operator="eq" value="1" />
                      <condition attribute="statecode" operator="eq" value="0" />
                      <condition attribute="ofm_facility" operator="eq"  value="{DB3C8C74-AD6E-EE11-8179-000D3A09D699}" />
                    </filter>
                    <link-entity name="contact" from="contactid" to="ofm_bceid" visible="false" link-type="outer" alias="a_408e15f922744d00b4d50ff712be3cc5">
                      <attribute name="emailaddress1" />
                      <attribute name="ofm_portal_role" />
                    </link-entity>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                            emails?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

                return requestUri.CleanCRLF();
            }
        }

        private string RetrieveEmailReminders
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $$"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="ofm_bceid_facility">
                    <attribute name="ofm_portal_access" />
                    <attribute name="ofm_bceid" />
                    <attribute name="ofm_is_expense_authority" />
                    <attribute name="ofm_is_additional_contact" />
                    <attribute name="ofm_bceid_facilityid" />
                    <order attribute="ofm_bceid" descending="false" />
                    <filter type="and">
                      <condition attribute="ofm_portal_access" operator="eq" value="1" />
                      <condition attribute="statecode" operator="eq" value="0" />
                      <condition attribute="ofm_facility" operator="eq"  value="{DB3C8C74-AD6E-EE11-8179-000D3A09D699}" />
                    </filter>
                    <link-entity name="contact" from="contactid" to="ofm_bceid" visible="false" link-type="outer" alias="a_408e15f922744d00b4d50ff712be3cc5">
                      <attribute name="emailaddress1" />
                      <attribute name="ofm_portal_role" />
                    </link-entity>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                            emails?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

                return requestUri.CleanCRLF();
            }
        }
        #region Private methods

        private string TemplatetoRetrieveUri
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="template">
                    <attribute name="title" />
                    <attribute name="templatetypecode" />
                    <attribute name="safehtml" />
                    <attribute name="languagecode" />
                    <attribute name="templateid" />
                    <attribute name="subject" />
                    <attribute name="description" />
                    <attribute name="body" />
                    <order attribute="title" descending="false" />
                    <filter type="and">
                      <condition attribute="templateid" operator="eq"  uitype="template" value="{_processParams.Notification.TemplateId}" />
                    </filter>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                            templates?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

                return requestUri.CleanCRLF();
            }
        }
        private async Task<ProcessData> GetTemplateToSendEmail()
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetTemplateToSendEmail");

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, TemplatetoRetrieveUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query Emmail Template to update with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                //if (currentValue?.AsArray().Count == 0)
                //{
                //    _logger.LogInformation(CustomLogEvent.Process, "No template found with query {requestUri}", EmailsToUpdateRequestUri.CleanLog());
                //}
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }


        public Task<ProcessData> GetDataAsync()
        {
            throw new NotImplementedException();
        }

        public Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            throw new NotImplementedException();
        }
    }
}
