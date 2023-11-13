using System.Diagnostics;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class ProcessResult
{
    private ProcessResult(Int16 processId,bool result, IEnumerable<string>? errors, string status, int processed, int totalRecords)
    {
        ProcessId = processId;
        CompletedNoErrors = result;
        Errors = errors ?? Array.Empty<string>();
        Status = status;
        TotalProcessed = processed;
        TotalRecords = totalRecords;
        CompletedAt = DateTime.Now;
        ResultMessage = CompletedNoErrors? "All records have been successfully processed with no warnings.": "Errors happened.";
    }

    public Int16 ProcessId { get; }
    public bool CompletedNoErrors { get; }
    public string Status { get; }
    public int TotalProcessed { get; }
    public int TotalRecords { get; }
    public DateTime CompletedAt { get; }
    public string ResultMessage { get; }

    public IEnumerable<string> Errors { get; }

    public static ProcessResult Success(Int16 processId, int processed, int totalRecords) => new(processId, true, null, "Successful",  processed, totalRecords);
    public static ProcessResult PartialSuccess(Int16 processId, int processed, int totalRecords) => new(processId, true, null, "PartialSuccess", processed, totalRecords);
    public static ProcessResult Failure(Int16 processId, IEnumerable<string> errors, int processed, int totalRecords) => new(processId, false, errors,"Failed", processed, totalRecords);
}