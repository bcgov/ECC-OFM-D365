using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public interface ID365ProcessProvider
{
    int ProcessId { get; }
    string ProcessName { get; }
    Task<ProcessData> GetData();
    Task<ProcessResult> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService);
}