using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class ProcessService : ID365ScheduledProcessService
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly IEnumerable<ID365ScheduledProcessProvider> _processProviders;

    public ProcessService(ID365AppUserService appUserService, ID365WebApiService service, IEnumerable<ID365ScheduledProcessProvider> processProviders)
    {
        _appUserService = appUserService;
        _d365webapiservice = service;
        _processProviders = processProviders;
    }

    public Task<JsonObject> RunProcessByIdAsync(int processId, ProcessParameter processParams) 
    {
        var provider = _processProviders.First(p => p.ProcessId == processId);

        return provider.RunScheduledProcessAsync(_appUserService, _d365webapiservice, processParams);
    }
}