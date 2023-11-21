using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Services.Documents;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class BatchResult
{
    private BatchResult(bool completed, IEnumerable<JsonObject>? result, IEnumerable<string>? errors, string status, int processed, int totalRecords)
    {
        CompletedWithNoErrors = completed;
        Errors = errors ?? Array.Empty<string>();
        Status = status;
        TotalProcessed = processed;
        TotalRecords = totalRecords;
        CompletedAt = DateTime.Now;
        Result = result;
        ResultMessage = (status == ProcessStatus.Successful) ? "All records have been successfully processed with no warnings." : "Check the logs for warnings or errors.";
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

    public static BatchResult Success(IEnumerable<JsonObject>? result, int processed, int totalRecords) => new(true, result, null, ProcessStatus.Successful, processed, totalRecords);
    public static BatchResult PartialSuccess(IEnumerable<JsonObject>? result, IEnumerable<string>? errors, int processed, int totalRecords) => new(true, result, errors, ProcessStatus.Partial, processed, totalRecords);
    public static BatchResult Failure(IEnumerable<string>? errors, int processed, int totalRecords) => new(false, null, errors, ProcessStatus.Failed, processed, totalRecords);

    #region Output

    public JsonObject SimpleBatchResult {
        get {
            return new JsonObject() {              
                { "status",Status},
                { "completedAt",CompletedAt},
                { "errors",JsonValue.Create<IEnumerable<string>>(Errors)},
                { "resultMessage",ResultMessage}
            };
        } 
    }

    #endregion
}