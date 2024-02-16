using OFM.Infrastructure.WebAPI.Extensions;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;
public enum FundingDecision
{
    Aproved,
    AutoApproved,
    AutoTempApproved,
    AutoFullApproved,
    AutoRejected,
    ToManualReview,
    ToFraudRisk,
    Rejected,
    Unknown
}

public class FundingResult
{
    private FundingResult(string fundingNumber, FundingDecision decision, FundingAmounts? fundingAmounts, IEnumerable<JsonObject>? result, IEnumerable<string>? errors)
    {
        FundingNumber = fundingNumber;
        Decision = decision;
        //FundingEnvelopes = fundingEnvelopes ?? Array.Empty<FundingAmounts>();
        FundingAmounts = fundingAmounts;
        Result = result ?? Array.Empty<JsonObject>();
        ResultMessage = decision == FundingDecision.AutoApproved ? "Funding is auto-approved." : "Check the logs for warnings or errors.";
        Errors = errors ?? Array.Empty<string>();
        CompletedAt = DateTime.Now;
    }

    public string FundingNumber { get; }
    public FundingDecision Decision { get; }
    public FundingAmounts FundingAmounts{ get; }
    public IEnumerable<FundingAmounts> FundingEnvelopes { get; }
    //public FundingAmounts[] FundingEnvelopeArray { get; }
    public IEnumerable<JsonObject> Result { get; }
    public string ResultMessage { get; }
    public IEnumerable<string> Errors { get; }
    public DateTime CompletedAt { get; }
    public IEnumerable<JsonObject> ActionsLog { get; }
    //public Funding Funding { get; }

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