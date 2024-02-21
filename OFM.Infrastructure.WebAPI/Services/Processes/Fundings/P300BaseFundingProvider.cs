﻿using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class P300BaseFundingProvider : ID365ProcessProvider
{
    private readonly ID365AppUserService? _appUserService;
    private readonly ID365WebApiService? _d365webapiservice;
    private readonly IFundingRepository? _fundingRepository;
    private readonly ILogger _logger;
    private readonly TimeProvider? _timeProvider;
    private ProcessData? _data;

    public P300BaseFundingProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, IFundingRepository fundingRepository, TimeProvider timeProvider)
    {
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _fundingRepository = fundingRepository;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public short ProcessId => Setup.Process.Funding.CalculateBaseFundingId;
    public string ProcessName => Setup.Process.Funding.CalculateBaseFundingName;

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
                          <condition attribute="accountid" operator="eq" value="" />                  
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
        _logger!.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P300BaseFundingProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                //_logger.LogError(CustomLogEvent.Process, "Failed to query the requests with the server error {responseBody}", responseBody);

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
        Funding? _funding = await _fundingRepository!.GetFundingByIdAsync(new Guid(processParams.Funding!.FundingId!));
        IEnumerable<RateSchedule> _rateSchedules = await _fundingRepository!.LoadRateSchedulesAsync();

        var calculator = new FundingCalculator(_logger,_fundingRepository, _funding, _rateSchedules);
        _ = await calculator.Calculate();
        _ = await calculator.ProcessFundingResult();

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}