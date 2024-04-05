using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public interface ID365ScheduledProcessService
{
    Task<JsonObject> RunProcessByIdAsync(int processId, ProcessParameter processParams);
}
public interface ID365OndemandProcessService
{
    Task<ProcessResult> RunProcessByIdAsync(int processId, ProcessParameter processParams);
}