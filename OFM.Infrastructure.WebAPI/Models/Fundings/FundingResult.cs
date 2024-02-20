using OFM.Infrastructure.WebAPI.Extensions;
using System.Text.Json.Nodes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;
public enum FundingDecision
{
    AutoCalculated,
    ManuallyRecalculated,
    InvalidData,
    Aproved,
    AutoApproved,
    AutoTempApproved,
    AutoFullApproved,
    AutoRejected,
    ToManualReview,
    Rejected,
    Unknown
}

public class FundingResult
{
    private FundingResult(string fundingNumber, FundingDecision decision, FundingAmounts? fundingAmounts, IEnumerable<JsonObject>? result, IEnumerable<string>? errors)
    {
        FundingNumber = fundingNumber;
        Decision = decision;
        FundingAmounts = fundingAmounts;
        Result = result ?? Array.Empty<JsonObject>();
        ResultMessage = (decision == FundingDecision.AutoCalculated) ? "Auto Calculated Funding Amounts." : "Not auto calculated or errors.";
        Errors = errors ?? Array.Empty<string>();
        CompletedAt = DateTime.Now;
    }

    public string FundingNumber { get; }
    public FundingDecision Decision { get; }
    public FundingAmounts? FundingAmounts{ get; }
    public IEnumerable<JsonObject> Result { get; }
    public string ResultMessage { get; }
    public IEnumerable<string> Errors { get; }
    public DateTime CompletedAt { get; }
    public IEnumerable<JsonObject> ActionsLog { get; }

    public static FundingResult AutoCalculated(string fundingNumber, FundingAmounts fundingAmounts, IEnumerable<JsonObject>? result) => new(fundingNumber, FundingDecision.AutoCalculated, fundingAmounts, result, null);
    public static FundingResult ManuallyRecalculated(string fundingNumber, FundingAmounts fundingAmounts, IEnumerable<JsonObject>? result) => new(fundingNumber, FundingDecision.ManuallyRecalculated, fundingAmounts, result, null);
    public static FundingResult InvalidData(string fundingNumber, IEnumerable<string>? errors) => new(fundingNumber, FundingDecision.InvalidData, null, null, errors);
    public static FundingResult Approved(string fundingNumber, FundingAmounts fundingAmounts, IEnumerable<JsonObject>? result) => new(fundingNumber, FundingDecision.Aproved, fundingAmounts, result, null);
    public static FundingResult AutoApproved(string fundingNumber, FundingAmounts fundingAmounts, IEnumerable<JsonObject>? result) => new(fundingNumber, FundingDecision.AutoApproved, fundingAmounts, result, null);
    public static FundingResult Rejected(string fundingNumber, FundingAmounts fundingAmounts, IEnumerable<JsonObject>? result, IEnumerable<string>? errors) => new(fundingNumber, FundingDecision.Rejected, fundingAmounts, result, null);
    public static FundingResult AutoRejected(string fundingNumber, FundingAmounts fundingAmounts, IEnumerable<JsonObject>? result, IEnumerable<string>? errors) => new(fundingNumber, FundingDecision.AutoRejected, fundingAmounts,result, null);

    #region Output

    public JsonObject SimpleResult
    {
        get
        {
            return new JsonObject()
            {
                { "decision",Decision.ToString()},
                { "completedAt",CompletedAt},
                { "errors",JsonValue.Create(Errors)},
                { "resultMessage",ResultMessage}
            };
        }
    }

    #endregion

}