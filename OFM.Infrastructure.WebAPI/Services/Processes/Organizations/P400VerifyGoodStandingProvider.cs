using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualBasic;
using System.Collections.Generic;
using Microsoft.OpenApi.Services;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Requests;

public class P400VerifyGoodStandingProvider : ID365ProcessProvider
{
    private readonly ProcessSettings _processSettings;
    private readonly APIKeyBCRegistry _ApiKeyBCRegistry;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P400VerifyGoodStandingProvider(IOptionsSnapshot<ProcessSettings> processSettings, IOptionsSnapshot<APIKeyBCRegistry> ApiKeyBCRegistry, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _processSettings = processSettings.Value;
        _ApiKeyBCRegistry = ApiKeyBCRegistry.Value; 
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process); ;
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Organization.VerifyGoodStandingId;
    public string ProcessName => Setup.Process.Organization.VerifyGoodStandingName;
    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="account">
                        <attribute name="accountid" />
                        <attribute name="ofm_business_number" />
                        <attribute name="name" />
                        <attribute name="modifiedon" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <filter type="and">
                          <condition attribute="accountid" operator="eq" value="{_processParams?.organization?.organizationId}" />                  
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         accounts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P400VerifyGoodStandingProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query the requests with the server error {responseBody}", responseBody);

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No records found");
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString());
        }

        return await Task.FromResult(_data);
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _logger.LogDebug(CustomLogEvent.Process, "Getting account (organization) reords with query {requestUri}", RequestUri);

        _processParams = processParams;

        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetData();

        string? legalName = _processParams?.organization?.legalName;
        string? incorporationNumber = _processParams?.organization?.incorporationNumber;
        string? organizationId = _processParams?.organization?.organizationId.ToString();

        var ApiKeyNameBCRegistry = _ApiKeyBCRegistry.KeyName;
        var ApiKeyValueBCRegistry = _ApiKeyBCRegistry.KeyValue;

        bool goodStanding;

        string? queryValue = (incorporationNumber != null) ? incorporationNumber : legalName;

        bool use_BusinessSearch_API_v1= true;        // v2 - indentifier (incorporationNumber), v1 - facets/generic multiple-retrieve (e.g. legalName) 

        if (incorporationNumber != null && use_BusinessSearch_API_v1 == false)   // v2 - identifier used here
        {
            var identifier = incorporationNumber;
            var baseUri = "https://bcregistry-sandbox.apigee.net/business/api/v2/businesses";
            //var path = $"{baseUri}" + $"/{identifier}";
            var path = $"{baseUri}" + $"/FM0666811";

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, path);

            request.Headers.Add("Account-Id", "1");
            request.Headers.Add(ApiKeyNameBCRegistry, ApiKeyValueBCRegistry);
            var content = new StringContent("", null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Console.WriteLine(await response.Content.ReadAsStringAsync());

            var json = await response.Content.ReadAsStringAsync();

            var obj = JObject.Parse(json);
            goodStanding = (bool)obj["business"]["goodStanding"];

        }
        else
        {                                                               // v1 - facet-based generic search used here   
            var baseUri = "https://bcregistry-sandbox.apigee.net/registry-search/api/v1/businesses/search/facets";
            var legalType = "A,B,BC,BEN,C,CC,CCC,CEM,CP,CS,CUL,EPR,FI,FOR,GP,LIC,LIB,LL,LLC,LP,MF,PA,PAR,PFS,QA,QB,QC,QD,QE,REG,RLY,S,SB,SP,T,TMY,ULC,UQA,UQB,UQC,UQD,UQE,XCP,XL,XP,XS";
            var status = "active";
            var queryString = $"?query=value:{queryValue}::identifier:::bn:::name:" +
                              $"&categories=legalType:{legalType}::status:{status}";

            var path = $"{baseUri}" + $"{queryString}";

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, path);

            request.Headers.Add("Account-Id", "1");
            request.Headers.Add(ApiKeyNameBCRegistry, ApiKeyValueBCRegistry);
            var content = new StringContent("", null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Console.WriteLine(await response.Content.ReadAsStringAsync());

            var json = await response.Content.ReadAsStringAsync();

            JObject goodStandingSearch = JObject.Parse(json);
            IList<JToken> results = goodStandingSearch["searchResults"]["results"].Children().ToList();
            IList<GoodStanding> searchResults = new List<GoodStanding>();
            foreach (JToken result in results)
            {
                GoodStanding searchResult = result.ToObject<GoodStanding>();
                searchResults.Add(searchResult);
            }

            if (searchResults.Count == 1)
            {
                var templateobj = searchResults.FirstOrDefault();
                var tmp_bn = templateobj.bn;
                var tmp_goodStanding = templateobj.goodStanding;
                var tmp_identifier = templateobj.identifier;
                var tmp_legalType = templateobj.legalType;
                var tmp_name = templateobj.name;
                var tmp_identifierscore = templateobj.identifierscore;
                var tmp_status = templateobj.status;

                goodStanding = (bool)tmp_goodStanding;

            }else if (searchResults.Count < 1)
            {
                _logger.LogError(CustomLogEvent.Process, "No record returned.");

            }else
            {
                _logger.LogError(CustomLogEvent.Process, "More than one records returned. Please resolve this issue to ensure uniqueness");

            }
        }

        var goodStandingValue = (goodStanding = true) ? 1 : 0;

        // update goodStanding and validateOn fields in organization record
        var statement = $"accounts({organizationId})";

        var payload = new JsonObject {
                { "ofm_good_standing_status", $"{goodStandingValue}" },    // 0 - No, 1 = Yes
                { "ofm_good_standing_validated_on", DateTime.UtcNow }
             };
        var requestBody = JsonSerializer.Serialize(payload);

        var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZPortalAppUser, statement, requestBody);

        if (!patchResponse.IsSuccessStatusCode)
        {
            var responseBody = await patchResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to patch GoodStanding status on organization with the server error {responseBody}", responseBody.CleanLog());

            return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, localData.Data.AsArray().Count).SimpleProcessResult;
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}