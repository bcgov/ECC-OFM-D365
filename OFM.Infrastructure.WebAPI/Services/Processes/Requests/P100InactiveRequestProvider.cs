using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json;
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

    public P100InactiveRequestProvider(IOptionsSnapshot<ProcessSettings> processSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _processSettings = processSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process); ;
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Requests.CloseInactiveRequestsId;
    public string ProcessName => Setup.Process.Requests.CloseInactiveRequestsName;
    public string RequestUri
    {
        get
        {
            var maxInactiveDays = _processSettings.MaxRequestInactiveDays;

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
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P100InactiveRequestProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query inactive requests with the server error {responseBody}", responseBody);

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No inactive requests found");
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
        _logger.LogDebug(CustomLogEvent.Process, "Getting inactive requests with query {requestUri}", RequestUri);

        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetData();

        if (localData.Data.AsArray().Count == 0)
        {
            _logger.LogInformation(CustomLogEvent.Process, "Close inactive requests process completed. No inactive requests found.");
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        List<HttpRequestMessage> requests = new() { };

        foreach (var request in localData.Data.AsArray())
        {
            var requestId = request!["ofm_assistance_requestid"]!.ToString();

            var body = new JsonObject()
            {
                ["statecode"] = 1,
                ["statuscode"] = 5,
                ["ofm_closing_reason"] = _processSettings.ClosingReason
            };

            requests.Add(new D365UpdateRequest(new EntityReference("ofm_assistance_requests", new Guid(requestId)), body));
        }

        var batchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requests, null);
       
        var endTime = _timeProvider.GetTimestamp();

        _logger.LogInformation(CustomLogEvent.Process, "Close inactive requests process finished in {totalElapsedTime} minutes", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes);

        if (batchResult.Errors.Any())
        {
            var result = ProcessResult.Failure(ProcessId, batchResult.Errors, batchResult.TotalProcessed, batchResult.TotalRecords);

            _logger.LogError(CustomLogEvent.Process, "Close inactive requests process finished with an error {error}", JsonValue.Create(result)!.ToJsonString());

            return result.SimpleProcessResult;
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}