namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class ProcessResult
{
    private ProcessResult(bool result, IEnumerable<string>? errors, string status, int processed, int totalRecords)
    {
        CompletedNoErrors = result;
        Errors = errors ?? Array.Empty<string>();
        Status = status;
        TotalProcessed = processed;
        TotalRecords = totalRecords;
        CompletedAt = DateTime.Now;
        ResultMessage = CompletedNoErrors? "All records have been successfully processed with no warnings.": "Errors happened.";
    }

    public bool CompletedNoErrors { get; }
    public string Status { get; }
    public int TotalProcessed { get; }
    public int TotalRecords { get; }
    public DateTime CompletedAt { get; }
    public string ResultMessage { get; }

    public IEnumerable<string> Errors { get; }

    public static ProcessResult Success(int processed, int totalRecords) => new(true, null, "Successful", processed, totalRecords);
    public static ProcessResult PartialSuccess(int processed, int totalRecords) => new(true, null, "PartialSuccess", processed, totalRecords);
    public static ProcessResult Failure(IEnumerable<string> errors, int processed, int totalRecords) => new(false, errors,"Failed", processed, totalRecords);
}