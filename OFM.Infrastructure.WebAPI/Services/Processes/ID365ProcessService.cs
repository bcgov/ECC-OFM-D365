using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public interface ID365ProcessService
{
    Task<ProcessResult> RunProcessByIdAsync(int processId, ProcessParameter processParams);
}