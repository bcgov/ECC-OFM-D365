using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
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

    public P100InactiveRequestProvider(IOptions<ProcessSettings> processSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _processSettings = processSettings.Value;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.BatchProcesses); ;
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => CommonInfo.ProcessInfo.Request.CloseInactiveRequestsId;
    public string ProcessName => CommonInfo.ProcessInfo.Request.CloseInactiveRequestsName;
    public string RequestUri
    {
        get
        {
            // Note: FetchXMl limit is 5000
            var days = _processSettings.MaxRequestInactiveDays;

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
        using (_logger.BeginScope("ScopeProcess: Running processs {processId} - {processName}", ProcessId, ProcessName))
        {
            _logger.LogDebug(CustomLogEvents.BatchProcesses, "Calling GetData of {nameof}", nameof(P100InactiveRequestProvider));

            if (_data is null)
            {
                var maxInactiveDays = _processSettings.MaxRequestInactiveDays;

                var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri);
                var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

                JsonNode d365Result = string.Empty;
                if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
                {
                    if (currentValue?.AsArray().Count == 0)
                    {
                        _logger.LogInformation(CustomLogEvents.BatchProcesses, "No inactive requests found with query {requestUri}", RequestUri);
                    }
                    d365Result = currentValue!;
                }

                _data = new ProcessData(d365Result);
            }
        }

        return await Task.FromResult(_data);
    }

    public async Task<ProcessResult> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        //get the data to process
        //var data = await Data;

        // log the data to process for debugging

        //process the data
        //var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, RequestUri);

        //log the result

        //if (!response.IsSuccessStatusCode)
        //{
        //    var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>() ?? new ProblemDetails();

        //    return TypedResults.Problem($"Failed to Retrieve inactive requests: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        //}

        //var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        using (_logger.BeginScope("ScopeProcess: Running processs {processId} - {processName}", ProcessId, ProcessName))
        {
            _logger.LogDebug(CustomLogEvents.BatchProcesses, "Getting due emails with query {requestUri}", RequestUri);

            var startTime = _timeProvider.GetTimestamp();

            var localData = await GetData();

            _logger.LogDebug(CustomLogEvents.BatchProcesses, "Return Result {localData}", localData.Data);
            var endTime = _timeProvider.GetTimestamp();

            //var serializedData = JsonSerializer.Deserialize<IEnumerable<D365Email>>(localData.Data.ToString());

            //var filtered = serializedData!.AsQueryable().Where(e => e.IsCompleted && (e.IsNewAndUnread || ValidateIsUnread(e))).ToList();

            //// Send only one email per contact
            //var filtered2 = filtered.GroupBy(e => e.torecipients).ToList();

            //foreach (var item in filtered2)
            //{
            //    //Add to bulk email create
            //}
            //var endTime = _timeProvider.GetTimestamp();

            _logger.LogInformation(CustomLogEvents.BatchProcesses, "Querying data finished in {totalElapsedTime} seconds", _timeProvider.GetElapsedTime(startTime, endTime).TotalSeconds);
             return ProcessResult.Success(ProcessId,localData.Data.AsArray().Count, localData.Data.AsArray().Count);
        }
    }
}