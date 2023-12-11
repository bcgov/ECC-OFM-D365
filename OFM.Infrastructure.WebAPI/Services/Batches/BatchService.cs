
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Batches;

public class D365BatchService : ID365BatchService
{
    protected readonly ID365WebApiService _d365webapiservice;
    private readonly IEnumerable<ID365BatchProvider> _providers;
    private readonly ID365AppUserService _appUserService;

    public D365BatchService(ID365AppUserService appUserService, ID365WebApiService service, IEnumerable<ID365BatchProvider> providers)
    {
        _d365webapiservice = service;
        _providers = providers;
        _appUserService = appUserService;
    }

    public async Task<ProcessResult> ExecuteAsync(JsonDocument jsonDocument, Int16 batchTypeId)
    {
        ID365BatchProvider provider = _providers.First(p => p.BatchTypeId == batchTypeId);

        var result = await provider.ExecuteAsync(jsonDocument, _appUserService, _d365webapiservice);
        //process the result and return

        return await Task.FromResult<ProcessResult>(ProcessResult.ODCompleted());
    }
}