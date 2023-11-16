using System.Diagnostics;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class ProcessResult
{
    private ProcessResult(Int16 processId, bool completed, IEnumerable<string>? errors, string status, int processed, int totalRecords)
    {
        ProcessId = processId;
        CompletedNoErrors = completed;
        Errors = errors ?? Array.Empty<string>();
        Status = status;
        TotalProcessed = processed;
        TotalRecords = totalRecords;
        CompletedAt = DateTime.Now;
        ResultMessage = (status == "Successful") ? "All records have been successfully processed with no warnings." :
            (status == "Completed") ? "The process has been triggered successfully. The result should be logged once the process is completed.":"Check the logs for warnings or errors.";
    }

    public Int16 ProcessId { get; }
    public bool CompletedNoErrors { get; }
    public string Status { get; }
    public int TotalProcessed { get; }
    public int TotalRecords { get; }
    public DateTime CompletedAt { get; }
    public string ResultMessage { get; }

    public IEnumerable<string> Errors { get; }

    public static ProcessResult Success(Int16 processId, int processed, int totalRecords) => new(processId, true, null, "Successful", processed, totalRecords);
    public static ProcessResult PartialSuccess(Int16 processId, int processed, int totalRecords) => new(processId, true, null, "PartialSuccess", processed, totalRecords);
    public static ProcessResult Failure(Int16 processId, IEnumerable<string> errors, int processed, int totalRecords) => new(processId, false, errors, "Failed", processed, totalRecords);
    public static ProcessResult Completed(Int16 processId) => new(processId, true, null, "Completed", 0, 0);
}