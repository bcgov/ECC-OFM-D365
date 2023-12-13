﻿using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using System.Xml;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Emails;

public class P205SendNotificationProvider : ID365ProcessProvider
{
    private readonly NotificationSettings _notificationSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _requestUri = string.Empty;

    public P205SendNotificationProvider(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _notificationSettings = notificationSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Email.SendNotificationsId;
    public string ProcessName => Setup.Process.Email.SendNotificationsName;
    public string RequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            // Use Paging Cookie for large datasets
            // Use exact filters to reduce the matching data and enhance performance
            if (string.IsNullOrEmpty(_requestUri))
            {
                var fetchXml = $"""
                <fetch distinct="true" no-lock="true">
                  <entity name="contact">
                    <attribute name="ccof_username" />
                    <attribute name="ccof_userid" />
                    <attribute name="ofm_first_name" />
                    <attribute name="ofm_last_name" />
                    <attribute name="contactid" />
                    <attribute name="donotbulkemail" />
                    <attribute name="donotpostalmail" />
                    <attribute name="emailaddress1" />
                    <attribute name="statecode" />
                    <attribute name="statuscode" />
                    <link-entity name="listmember" from="entityid" to="contactid" link-type="inner" alias="clist" intersect="true">
                      <attribute name="listid" />
                      <attribute name="name" />
                      <filter>
                        <condition attribute="listid" operator="eq" value="{_processParams.Notification.MarketingListId}" />
                      </filter>
                    </link-entity>
                  </entity>
                </fetch>
                """;

                var requestUri = $"""
                            contacts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

                _requestUri = requestUri.CleanCRLF();
            }

            return _requestUri;
        }
    }

    private string EmailsToUpdateRequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000 records per request
            var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="email">
                    <attribute name="subject" />
                    <attribute name="from" />
                    <attribute name="prioritycode" />
                    <attribute name="activityid" />
                    <attribute name="createdon" />
                    <attribute name="to" />
                    <attribute name="scheduledend" />
                    <attribute name="statuscode" />
                    <attribute name="statecode" />
                    <attribute name="ownerid" />
                    <attribute name="regardingobjectid" />
                    <order attribute="createdon" descending="true" />
                    <filter type="and">
                      <condition attribute="createdon" operator="last-x-hours" value="3" />
                      <condition attribute="ofm_communication_type" operator="not-null" />
                      <condition attribute="statuscode" operator="eq" value="1" />
                    </filter>
                    <link-entity name="activityparty" from="activityid" to="activityid" link-type="inner" alias="ae">
                    <filter type="and">
                      <condition attribute="participationtypemask" operator="eq" value="1" />
                      <condition attribute="partyid" operator="eq" uitype="systemuser" value="{_processParams.Notification.SenderId}" />
                    </filter>
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

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P205SendNotificationProvider));

        if (_data is null && _processParams is not null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting active contacts from a marketinglist with query {requestUri}", RequestUri.CleanLog());

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query members on the contact list with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No members on the contact list found with query {requestUri}", RequestUri.CleanLog());
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data?.Data.ToString().CleanLog());
        }

        return await Task.FromResult(_data!);
    }

    private async Task<ProcessData> GetDataToUpdate()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetDataToUpdate");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, EmailsToUpdateRequestUri, isProcess: true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query pending emails to update with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No pending emails on the contact list found with query {requestUri}", EmailsToUpdateRequestUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;

        var startTime = _timeProvider.GetTimestamp();

     // var localData = await GetData();
        var localData = await GetAllDataWithPagingCookie();

        var serializedData = JsonSerializer.Deserialize<List<D365Contact>>(localData.Data.ToString());



        #region Step 3: Send the notifications

        // Emails are created with "Completed - Pending Send" status. Step 3 is needed to send emails via GC-Notify or Exchange Online

        #endregion

        var result = ProcessResult.Success(ProcessId, serializedData!.Count);

        var endTime = _timeProvider.GetTimestamp();

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = JsonSerializer.Serialize(result, serializeOptions);

        //_logger.LogInformation(CustomLogEvent.Process, "Send Notification process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, JsonValue.Create(result)!.ToString().CleanLog());
        _logger.LogInformation(CustomLogEvent.Process, "Send Notification process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, json);

        return result.SimpleProcessResult;

    }
 

    private async Task<ProcessData> GetAllDataWithPagingCookie()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetDataToUpdate");

        if (_data is null && _processParams is not null)
        {

            JsonNode d365Result = string.Empty;

            // *******************************************************************************************
            int fetchCount = 5000;
            int pageNumber = 1;
            int recordCount = 0;
            string pagingCookie = null;

            var strCookieExtract = string.Empty;
            var decodedCookie = string.Empty;
            var decodedCookie2 = string.Empty;
            string fetchXml_Cookie;

            var fetchXml = $"""
            <fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false' >
              <entity name='account' >
                <attribute name='name' />
                <attribute name='primarycontactid' />
                <attribute name='telephone1' />
                <attribute name='accountid' />
                <order attribute='name' descending='false' />
              </entity>
            </fetch>
            """;

            while (true)
            {

                fetchXml_Cookie = CreateXml(fetchXml, pagingCookie, pageNumber, fetchCount);

                var requestUri_Cookie = $"""
								accounts?fetchXml={WebUtility.UrlEncode(fetchXml_Cookie)}
								""";

                requestUri_Cookie = requestUri_Cookie.CleanCRLF();

                //var response_Cookie = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUri_Cookie, isProcess: true);
                var response_Cookie = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUri_Cookie, formatted: true, isProcess: true);

                var jsonObject_Cookie = await response_Cookie.Content.ReadFromJsonAsync<JsonObject>();


                //concat json objects
                var serializeOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json_prev = JsonSerializer.Serialize(d365Result, serializeOptions);

                if (jsonObject_Cookie?.TryGetPropertyValue("value", out var currentValue) == true)
                {
                    if (currentValue?.AsArray().Count == 0)
                    {
                        _logger.LogInformation(CustomLogEvent.Process, "No members on the contact list found with query {requestUri}", RequestUri.CleanLog());
                    }
                    d365Result = currentValue!;
                }
                string json_current = JsonSerializer.Serialize(d365Result, serializeOptions);

                var JsonString_After_Concat = JsonConvert.SerializeObject(
                                                 new[] { JsonConvert.DeserializeObject(json_prev),
                                                         JsonConvert.DeserializeObject(json_current) });

                var JsonObject_After_Concat = JsonConvert.DeserializeObject(JsonString_After_Concat);

                //d365Result = (JsonNode)JsonObject_After_Concat;


                // determine next fetch (if any)

                bool isMoreRecords = false;

                if (jsonObject_Cookie?.TryGetPropertyValue("@Microsoft.Dynamics.CRM.morerecords", out var moreRecords) == true)
                {
                    isMoreRecords = (bool)moreRecords!;
                }

                if (isMoreRecords)
                {
                    var strCookie = string.Empty;

                    if (jsonObject_Cookie?.TryGetPropertyValue("@Microsoft.Dynamics.CRM.fetchxmlpagingcookie", out var pgCookie) == true)
                    {
                        strCookie = (string)pgCookie!;
                    }
                    strCookieExtract = strCookie?.Substring(strCookie.IndexOf("%253c"), strCookie.LastIndexOf("\" istracking") - strCookie.IndexOf("%253c"));
                    decodedCookie = HttpUtility.UrlDecode(strCookieExtract);
                    decodedCookie = HttpUtility.UrlDecode(decodedCookie);

                    pageNumber++;
                    pagingCookie = decodedCookie;
                }
                else
                {
                    break;
                }
            }

            // *******************************************************************************************

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data?.Data.ToString().CleanLog());
        }

        return await Task.FromResult(_data!);
    }


    public static string CreateXml(string xml, string cookie, int page, int count)
    {
        StringReader stringReader = new StringReader(xml);
        var reader = new XmlTextReader(stringReader);

        // Load document
        XmlDocument doc = new XmlDocument();
        doc.Load(reader);

        XmlAttributeCollection attrs = doc.DocumentElement.Attributes;

        if (cookie != null)
        {
            XmlAttribute pagingAttr = doc.CreateAttribute("paging-cookie");
            pagingAttr.Value = cookie;
            attrs.Append(pagingAttr);
        }

        XmlAttribute pageAttr = doc.CreateAttribute("page");
        pageAttr.Value = System.Convert.ToString(page);
        attrs.Append(pageAttr);

        XmlAttribute countAttr = doc.CreateAttribute("count");
        countAttr.Value = System.Convert.ToString(count);
        attrs.Append(countAttr);

        StringBuilder sb = new StringBuilder(1024);
        StringWriter stringWriter = new StringWriter(sb);

        XmlTextWriter writer = new XmlTextWriter(stringWriter);
        doc.WriteTo(writer);
        writer.Close();

        return sb.ToString();
    }


}