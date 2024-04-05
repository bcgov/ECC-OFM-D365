using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Services.Documents;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class ProcessResult
{
    private ProcessResult(bool completed, IEnumerable<JsonObject>? result, IEnumerable<string>? errors, string status, int processed, int totalRecords)
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

    private ProcessResult(Int16 processId, bool completed, IEnumerable<string>? errors, string status, int processed, int totalRecords)
    {
        ProcessId = processId;
        CompletedWithNoErrors = completed;
        Errors = errors ?? Array.Empty<string>();
        Status = status;
        TotalProcessed = processed;
        TotalRecords = totalRecords;
        CompletedAt = DateTime.Now;
        ResultMessage = (status == ProcessStatus.Successful) ? "All records have been successfully processed with no warnings." :
            (status == ProcessStatus.Completed) ? "The process has been triggered successfully. The result is logged once the process is completed." : "Check the logs for warnings or error details.";
    }

    public Int16 ProcessId { get; }
    public bool CompletedWithNoErrors { get; }
    public string Status { get; }
    public int TotalProcessed { get; }
    public int TotalRecords { get; }
    public DateTime CompletedAt { get; }
    public IEnumerable<JsonObject>? Result { get; }
    public string ResultMessage { get; }
    public IEnumerable<string> Errors { get; }

    #region Set Scheduled Process Results

    public static ProcessResult Success(Int16 processId, int totalRecords) => new(processId, true, null, ProcessStatus.Successful, totalRecords, totalRecords);
    public static ProcessResult PartialSuccess(Int16 processId, IEnumerable<string> errors, int processed, int totalRecords) => new(processId, true, errors, ProcessStatus.Partial, processed, totalRecords);
    public static ProcessResult Failure(Int16 processId, IEnumerable<string> errors, int processed, int totalRecords) => new(processId, false, errors, ProcessStatus.Failed, processed, totalRecords);
    public static ProcessResult Completed(Int16 processId) => new(processId, true, null, ProcessStatus.Completed, 0, 0);
    
    #endregion

    #region Set On-Demand Results

    public static ProcessResult ODSuccess(IEnumerable<JsonObject> result, int processed, int totalRecords) => new(true, result, null, ProcessStatus.Successful, processed, totalRecords);
    public static ProcessResult ODPartialSuccess(IEnumerable<JsonObject>? result, IEnumerable<string>? errors, int processed, int totalRecords) => new(true, result, errors, ProcessStatus.Partial, processed, totalRecords);
    public static ProcessResult ODFailure(IEnumerable<string>? errors, int processed, int totalRecords) => new(false, null, errors, ProcessStatus.Failed, processed, totalRecords);
    public static ProcessResult ODCompleted() => new(true, null, null, ProcessStatus.Completed, 0, 0);

    #endregion

    #region Output

    public JsonObject SimpleProcessResult {
        get {
            return new JsonObject() {
                { "processId",ProcessId},
                { "status",Status},
                { "completedAt",CompletedAt},
                { "errors",Errors.FirstOrDefault()},
                { "resultMessage",ResultMessage}
            };
        } 
    }

    public JsonObject ODProcessResult
    {
        get
        {
            return new JsonObject() {
                { "status",Status},
                { "completedAt",CompletedAt},
                { "resultMessage",ResultMessage}
            };
        }
    }

    #endregion
}