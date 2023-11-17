using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Services.Documents;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class ProcessResult
{
    private ProcessResult(bool completed, IEnumerable<JsonObject>? result, IEnumerable<string>? errors, ProcessStatus status, int processed, int totalRecords)
    {
        CompletedWithNoErrors = completed;
        Errors = errors ?? Array.Empty<string>();
        Status = status;
        TotalProcessed = processed;
        TotalRecords = totalRecords;
        CompletedAt = DateTime.Now;
        Result = result;
        ResultMessage = (status == ProcessStatus.Successful) ? "All records have been successfully processed with no warnings." :
            (status == ProcessStatus.Completed) ? "The process has been triggered successfully. The result should be logged once the process is completed." : "Check the logs for warnings or errors.";
    }

    private ProcessResult(Int16 processId, bool completed, IEnumerable<string>? errors, ProcessStatus status, int processed, int totalRecords)
    {
        ProcessId = processId;
        CompletedWithNoErrors = completed;
        Errors = errors ?? Array.Empty<string>();
        Status = status;
        TotalProcessed = processed;
        TotalRecords = totalRecords;
        CompletedAt = DateTime.Now;
        ResultMessage = (status == ProcessStatus.Successful) ? "All records have been successfully processed with no warnings." :
            (status == ProcessStatus.Completed) ? "The process has been triggered successfully. The result should be logged once the process is completed." : "Check errors and the logs for warnings or error details.";
    }

    public Int16 ProcessId { get; }
    public bool CompletedWithNoErrors { get; }
    public ProcessStatus Status { get; }
    public int TotalProcessed { get; }
    public int TotalRecords { get; }
    public DateTime CompletedAt { get; }
    public IEnumerable<JsonObject>? Result { get; }
    public string ResultMessage { get; }
    public IEnumerable<string> Errors { get; }

    #region Common Process Results
    public static ProcessResult Success(Int16 processId, int processed, int totalRecords) => new(processId, true, null, ProcessStatus.Successful, processed, totalRecords);
    public static ProcessResult PartialSuccess(Int16 processId, int processed, int totalRecords) => new(processId, true, null, ProcessStatus.Partial, processed, totalRecords);
    public static ProcessResult Failure(Int16 processId, IEnumerable<string> errors, int processed, int totalRecords) => new(processId, false, errors, ProcessStatus.Failed, processed, totalRecords);
    public static ProcessResult Completed(Int16 processId) => new(processId, true, null, ProcessStatus.Completed, 0, 0);
    #endregion

    #region Non-Process Results

    public static ProcessResult Success(IEnumerable<JsonObject> result, int processed, int totalRecords) => new(true, result, null, ProcessStatus.Successful, processed, totalRecords);
    public static ProcessResult PartialSuccess(IEnumerable<JsonObject>? result, IEnumerable<string>? errors, int processed, int totalRecords) => new(true, result, errors, ProcessStatus.Partial, processed, totalRecords);
    public static ProcessResult Failure(IEnumerable<string>? errors, int processed, int totalRecords) => new(false, null, errors, ProcessStatus.Failed, processed, totalRecords);
    public static ProcessResult Completed() => new(true, null, null, ProcessStatus.Completed, 0, 0);
    #endregion
}