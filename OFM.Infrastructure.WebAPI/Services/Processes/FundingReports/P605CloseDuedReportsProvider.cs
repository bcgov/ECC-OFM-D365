using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.FundingReports;

public class P605CloseDuedReportsProvider : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly D365AuthSettings _d365AuthSettings;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P605CloseDuedReportsProvider(IOptionsSnapshot<D365AuthSettings> d365AuthSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _d365AuthSettings = d365AuthSettings.Value;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.FundingReports.CloseDuedReportsId;
    public string ProcessName => Setup.Process.FundingReports.CloseDuedReportsName;
    public string RequestUri
    {
        get
        {
            // Note: Get the funding report response
            var currentDateInUTC = DateTime.UtcNow;
                //for reference only
                var fetchXml = $"""
                                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
                                  <entity name="ofm_survey_response">
                                    <filter>
                                      <condition attribute="ofm_duedate" operator="lt" value="{currentDateInUTC}" />
                                      <condition attribute="statecode" operator="eq" value="0" />
                                      <condition attribute="ofm_unlock" operator="eq" value="0" />
                                    </filter>
                                  </entity>
                                </fetch>
                                """;

            var requestUri = $"""                                
                                ofm_survey_responses?fetchXml={WebUtility.UrlEncode(fetchXml)}
                                """;

            return requestUri.CleanCRLF();
        }
    }

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P605CloseDuedReportsProvider));

        if (_data is null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting  with query {requestUri}", RequestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query reports with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No reports found with query {requestUri}", RequestUri);
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString(Setup.s_writeOptionsForLogs));
        }

        return await Task.FromResult(_data);
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {

        _processParams = processParams;

        var startTime = _timeProvider.GetTimestamp();
        var localData = await GetDataAsync();

        if (localData.Data.AsArray().Count == 0)
        {
            _logger.LogInformation(CustomLogEvent.Process, "Close past due report process completed. No reports found.");
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        List<HttpRequestMessage> requests = [];

        foreach (var report in localData.Data.AsArray())
        {
            var reportId = report!["ofm_survey_responseid"]!.ToString();
            
            var deactivateReportBody = new JsonObject
            {
                {"statuscode",(int)ofm_survey_response_StatusCode.Closed },
                {"statecode",(int)ofm_survey_response_statecode.Inactive }
            };

            requests.Add(new D365UpdateRequest(new D365EntityReference("ofm_survey_responses", new Guid(reportId)), deactivateReportBody));
        }



        var batchRequest = requests.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / 1000).Select(x => x.Select(v => v.Value).ToList()).ToList();
        List<BatchResult> batchResults = [];
        var numOfRequest = requests.Count;
        
        foreach (var batch in batchRequest)
        {
            batchResults.Add(await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, batch, null));

        }

        var endTime = _timeProvider.GetTimestamp();

        foreach(var batchResult in batchResults)
        {
            if (batchResult.Errors.Any())
            {
                var result = ProcessResult.Failure(ProcessId, batchResult.Errors, batchResult.TotalProcessed, batchResult.TotalRecords);

                _logger.LogError(CustomLogEvent.Process, "Close past due report process finished with an error {error}", JsonValue.Create(result)!.ToJsonString());

                return result.SimpleProcessResult;
            }
        }

        _logger.LogInformation(CustomLogEvent.Process, "Close past due report process finished in {totalElapsedTime} minutes", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes);

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

}