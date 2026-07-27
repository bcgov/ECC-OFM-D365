using ECC.Core.DataContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class P300BaseFundingProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, IFundingRepository fundingRepository, TimeProvider timeProvider) : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly IFundingRepository _fundingRepository = fundingRepository;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly TimeProvider _timeProvider = timeProvider;
    private ProcessData? _data;
    private Guid fundingID;
    private string formattedApplicationID;
    private int fundingYear;

    public short ProcessId => Setup.Process.Fundings.CalculateBaseFundingId;
    public string ProcessName => Setup.Process.Fundings.CalculateBaseFundingName;

    public string RequestUri
    {
        get
        {
            var requestUri = $"""

                """;

            return requestUri;
        }
    }

    public string RetrieveFundingAllocation
    {
        get
        {
            var reallocationFetchXML = $"""
                                <fetch>
                                  <entity name="ofm_funding_envelope_change">
                                    <filter>
                                      <condition attribute="ofm_applicationid" operator="eq" value="{formattedApplicationID}" />
                                      <condition attribute="statecode" operator="eq" value="0" />
                                      <condition attribute="statuscode" operator="eq" value="1" />
                                      <condition attribute="ofm_year_of_agreement" operator="eq" value="{fundingYear}" />
                                    </filter>
                                    <link-entity name="ofm_funding_allocation" from="ofm_funding_envelop" to="ofm_funding_envelope_changeid" link-type="inner">
                                      <attribute name="ofm_amount" />
                                      <attribute name="ofm_funding_envelope_from" />
                                      <attribute name="ofm_funding_envelope_to" />
                                      <filter>
                                        <condition attribute="statecode" operator="eq" value="0" />
                                      </filter>
                                    </link-entity>
                                  </entity>
                                </fetch>
                                """;

            var reallocationRequestUri = $"""
                         ofm_funding_envelope_changes?fetchXml={WebUtility.UrlEncode(reallocationFetchXML)}
                         """;

            return reallocationRequestUri;
        }
    }

    public string AllRelatedFundings
    {
        get
        {
            var allRelatedFundingFetchXML = $"""
                                <fetch>
                                  <entity name="ofm_funding">
                                    <attribute name="ofm_fundingid" />
                                    <attribute name="ofm_start_date" />
                                    <attribute name="ofm_end_date" />
                                    <attribute name="ofm_version_number" />
                                    <attribute name="statecode" />
                                    <attribute name="statuscode" />
                                    <filter>
                                      <condition attribute="ofm_application" operator="eq" value="{formattedApplicationID}" />
                                    </filter>
                                    <order attribute="ofm_version_number" descending="true" />
                                  </entity>
                                </fetch>
                                """;

            var allRelatedFundingUri = $"""
                         ofm_fundings?fetchXml={WebUtility.UrlEncode(allRelatedFundingFetchXML)}
                         """;

            return allRelatedFundingUri;
        }
    }

    public async Task<ProcessData> GetDataAsync()
    {
        _logger!.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P300BaseFundingProvider));

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
    public async Task<ProcessData> GetAllRelatedFundingDataAsync()
    {
        HttpResponseMessage response = new HttpResponseMessage();

        _logger.LogDebug(CustomLogEvent.Process, "Calling GetAllRelatedFundingDataAsync");

        response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, AllRelatedFundings, formatted: true, isProcess: true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to retrieve Funding data with the following server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Funding Allocation data found with query {requestUri}", AllRelatedFundings.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public async Task<ProcessData> GetFundingAllocationDataAsync()
    {
        HttpResponseMessage response = new HttpResponseMessage();

        _logger.LogDebug(CustomLogEvent.Process, "Calling RetrieveFundingAllocation");

        response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RetrieveFundingAllocation, formatted: true, isProcess: true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to retrieve Funding Allocation data with the following server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Funding Allocation data found with query {requestUri}", RetrieveFundingAllocation.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        Funding? _funding = await _fundingRepository!.GetFundingByIdAsync(new Guid(processParams.Funding!.FundingId!));
        IEnumerable<RateSchedule> _rateSchedules = await _fundingRepository!.LoadRateSchedulesAsync();

        fundingID = new Guid(processParams.Funding!.FundingId!);
        Guid applicationID = _funding!.ofm_application!.Id;
        if (applicationID != Guid.Empty) {
            formattedApplicationID = applicationID.ToString("D");
        }

        //To determine if Funding is in Year 1, 2 or 3
        var allfundingData = await GetAllRelatedFundingDataAsync();
        List<Funding> deserializedAllFundingData = null;
        DateTime originalFundingStartDate = DateTime.UtcNow;
        Guid latestActiveFundingID = Guid.Empty;

        if (allfundingData != null && allfundingData.Data != null)
        {
            deserializedAllFundingData = JsonSerializer.Deserialize<List<Funding>>(allfundingData.Data.ToString());
            foreach (var fundingRecord in deserializedAllFundingData)
            {
                if (fundingRecord.ofm_version_number == 0 && fundingRecord.ofm_start_date != null)
                {
                    //Original Funding Record
                    originalFundingStartDate = (DateTime)fundingRecord.ofm_start_date;
                }
                if (fundingRecord.statuscode == ofm_funding_StatusCode.Active && latestActiveFundingID == Guid.Empty)
                {
                    latestActiveFundingID = fundingRecord.Id;
                }
            }
        }

        DateTime originalFundingStartDatePST = originalFundingStartDate.ToLocalPST().Date;

        DateTime year1 = originalFundingStartDatePST.AddYears(1).AddDays(-1);
        DateTime year2 = originalFundingStartDatePST.AddYears(2).AddDays(-1);
        DateTime year3 = originalFundingStartDatePST.AddYears(3).AddDays(-1);

        DateTime currentDatePST = DateTime.UtcNow.ToLocalPST().Date;

        if (currentDatePST <= year1)
        {
            fundingYear = 1;
        }
        else if (currentDatePST > year1 && currentDatePST <= year2)
        {
            fundingYear = 2;
        }
        else if (currentDatePST > year2 && currentDatePST <= year3)
        {
            fundingYear = 3;
        }
        else
        {
            fundingYear = 0;
        }

        var fundingAllocationData = await GetFundingAllocationDataAsync();
        List<D365FundingEnvelope> deserializedFundingReallocationData = null;
        if (fundingAllocationData !=null && fundingAllocationData.Data != null && fundingID == latestActiveFundingID)
        {
            deserializedFundingReallocationData = JsonSerializer.Deserialize<List<D365FundingEnvelope>>(fundingAllocationData.Data.ToString());
        }

        FundingCalculator calculator = new(_fundingRepository, _funding, _rateSchedules, deserializedFundingReallocationData, _logger);
        _ = await calculator.CalculateAsync();
        _ = await calculator.ProcessFundingResultAsync();
        await calculator.LogProgressAsync(_d365webapiservice!, _appUserService!, _logger!); // This line should always be at the end to avoid any impacts to the calculator's main functionalities

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}