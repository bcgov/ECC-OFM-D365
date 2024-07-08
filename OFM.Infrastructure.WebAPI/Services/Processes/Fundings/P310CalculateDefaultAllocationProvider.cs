using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class P310CalculateDefaultAllocationProvider : ID365ProcessProvider
{
    private readonly ID365AppUserService? _appUserService;
    private readonly ID365WebApiService? _d365webapiservice;
    private readonly ILoggerFactory? loggerFactory;
    private readonly IFundingRepository? _fundingRepository;
    private readonly ILogger _logger;
    private readonly TimeProvider? _timeProvider;
    private ProcessData? _data;
    private Funding? _funding;

    public P310CalculateDefaultAllocationProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, IFundingRepository fundingRepository, TimeProvider timeProvider)
    {
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _fundingRepository = fundingRepository;
        _logger = loggerFactory.CreateLogger(LogCategory.Process); ;
        _timeProvider = timeProvider;
    }

    public short ProcessId => Setup.Process.Fundings.CalculateDefaultSpacesAllocationId;
    public string ProcessName => Setup.Process.Fundings.CalculateDefaultSpacesAllocationName;

    public string RequestUri
    {
        get
        {
            var requestUri = $"""
                          
                         """;

            return requestUri;
        }
    }

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P310CalculateDefaultAllocationProvider));

        return await Task.FromResult<ProcessData>(null);
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _funding = await _fundingRepository!.GetFundingByIdAsync(new Guid(processParams.Funding!.FundingId!));
        IEnumerable<RateSchedule> _rateSchedules = await _fundingRepository!.LoadRateSchedulesAsync();

        var calculator = new DefaultCalculator(_fundingRepository, _funding, _rateSchedules, _logger);
        await calculator.CalculateDefaultSpacesAllocationAsync();
        await calculator.CalculateAsync();
        await calculator.LogProgressAsync(_d365webapiservice!, _appUserService!, _logger!, titlePrefix: "Default"); // This line should always be at the end to avoid any impacts to the calculator's main functionalities

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}