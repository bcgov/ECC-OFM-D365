using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class ProcessService(ID365AppUserService appUserService, ID365WebApiService service, IEnumerable<ID365ProcessProvider> processProviders) : ID365ScheduledProcessService
{
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = service;
    private readonly IEnumerable<ID365ProcessProvider> _processProviders = processProviders;

    public Task<JsonObject> RunProcessByIdAsync(int processId, ProcessParameter processParams) 
    {
        var provider = _processProviders.First(p => p.ProcessId == processId);

        return provider.RunProcessAsync(_appUserService, _d365webapiservice, processParams);
    }
}