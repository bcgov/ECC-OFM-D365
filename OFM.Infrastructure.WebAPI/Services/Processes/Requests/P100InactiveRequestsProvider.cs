using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Requests;

public class P100InactiveRequestProvider : ID365ProcessProvider
{
    private readonly ProcessSettings _processSettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;

    public P100InactiveRequestProvider(IOptions<ProcessSettings> processSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _processSettings = processSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process); ;
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => CommonInfo.ProcessInfo.Request.CloseInactiveRequestsId;
    public string ProcessName => CommonInfo.ProcessInfo.Request.CloseInactiveRequestsName;
    public string RequestUri
    {
        get
        {
            var maxInactiveDays = _processSettings.MaxRequestInactiveDays;

            // Note: FetchXMl limit is 5000
            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="ofm_assistance_request">
                        <attribute name="ofm_assistance_requestid" />
                        <attribute name="ofm_name" />
                        <attribute name="ofm_subject" />
                        <attribute name="ofm_request_category" />
                        <attribute name="ofm_contact" />
                        <attribute name="modifiedon" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <filter>
                          <condition attribute="statuscode" operator="eq" value="4" />
                          <condition attribute="ofm_last_action_time" operator="olderthan-x-days" value="{maxInactiveDays}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_assistance_requests?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvents.Process, "Calling GetData of {nameof}", nameof(P100InactiveRequestProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri);
            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvents.Process, "No inactive requests found with query {requestUri}", RequestUri);
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);
        }
        return await Task.FromResult(_data);
    }

    public async Task<ProcessResult> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _logger.LogDebug(CustomLogEvents.Process, "Getting due emails with query {requestUri}", RequestUri);

        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetData();

        _logger.LogDebug(CustomLogEvents.Process, "Return Result {localData}", localData.Data);

        List<HttpRequestMessage> requests = new() { };

        foreach (var request in localData.Data.AsArray())
        {
            var requestId = request!["ofm_assistance_requestid"]!.ToString();

            var body = new JsonObject()
            {
                ["statecode"] = 1,
                ["statuscode"] = 5,
                ["ofm_closing_reason"] = "No Action"
            };

            requests.Add(new UpdateRequest(new EntityReference("ofm_assistance_requests", new Guid(requestId)), body));
        }

        HttpResponseMessage response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requests, null);
        var endTime = _timeProvider.GetTimestamp();

        _logger.LogInformation(CustomLogEvents.Process, "Querying data finished in {totalElapsedTime} seconds", _timeProvider.GetElapsedTime(startTime, endTime).TotalSeconds);

        if (!response.IsSuccessStatusCode)
        {
            return ProcessResult.Failure(ProcessId, new String[] { response.ReasonPhrase }, 0, localData.Data.AsArray().Count);
        }

        return ProcessResult.Success(ProcessId, localData.Data.AsArray().Count, localData.Data.AsArray().Count);
    }
}