using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;

public class P400VerifyGoodStandingProvider : ID365ProcessProvider
{
    private readonly ProcessSettings _processSettings;
    private readonly BCRegistrySettings _BCRegistrySettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P400VerifyGoodStandingProvider(IOptionsSnapshot<ProcessSettings> processSettings, IOptionsSnapshot<ExternalServices> ApiKeyBCRegistry, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _processSettings = processSettings.Value;
        _BCRegistrySettings = ApiKeyBCRegistry.Value.BCRegistryApi;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process); ;
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.ProviderProfile.VerifyGoodStandingId;
    public string ProcessName => Setup.Process.ProviderProfile.VerifyGoodStandingName;
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
                          <condition attribute="accountid" operator="eq" value="{_processParams?.Organization?.organizationId}" />                  
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
        _processParams = processParams;

        var startTime = _timeProvider.GetTimestamp();

        string? queryValue = (!String.IsNullOrEmpty(processParams?.Organization?.incorporationNumber)) ?
             (_processParams?.Organization?.incorporationNumber)!.Trim() : (_processParams?.Organization?.legalName)!.Trim();

        var legalType = "A,B,BC,BEN,C,CC,CCC,CEM,CP,CS,CUL,EPR,FI,FOR,GP,LIC,LIB,LL,LLC,LP,MF,PA,PAR,PFS,QA,QB,QC,QD,QE,REG,RLY,S,SB,SP,T,TMY,ULC,UQA,UQB,UQC,UQD,UQE,XCP,XL,XP,XS";
        var status = "active";
        var queryString = $"?query=value:{queryValue}::identifier:::bn:::name:" +
                          $"&categories=legalType:{legalType}::status:{status}";

        var path = $"{_BCRegistrySettings.RegistrySearchUrl}" + $"{queryString}";

        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Account-Id", "1");
        request.Headers.Add(_BCRegistrySettings.KeyName, _BCRegistrySettings.KeyValue);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        BCRegistrySearchResult? searchResult = await response.Content.ReadFromJsonAsync<BCRegistrySearchResult>();

        if (searchResult is null || searchResult.searchResults.totalResults < 1)
        {
            // Todo: Add a new message for this scenario or try seach by name
            _logger.LogError(CustomLogEvent.Process, "No results found.");
            return ProcessResult.PartialSuccess(ProcessId, ["No records found."], 0, 0).SimpleProcessResult;
        }

        if (searchResult.searchResults.totalResults > 1)
        {
            // ToDo: Process and filter the result further
            _logger.LogError(CustomLogEvent.Process, "More than one records returned. Please resolve this issue to ensure uniqueness");
            return ProcessResult.PartialSuccess(ProcessId, ["Multiple results returned."], 0, 0).SimpleProcessResult;
        }

        var statement = $"accounts({_processParams?.Organization?.organizationId.ToString()})";

        var payload = new JsonObject {
                { "ofm_good_standing_status", searchResult.searchResults.results.First().goodStanding ? 1 : 0},
                { "ofm_good_standing_validated_on", DateTime.UtcNow }
        };

        var requestBody = JsonSerializer.Serialize(payload);

        var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, statement, requestBody);

        if (!patchResponse.IsSuccessStatusCode)
        {
            var responseBody = await patchResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to patch GoodStanding status on organization with the server error {responseBody}", responseBody.CleanLog());

            return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}