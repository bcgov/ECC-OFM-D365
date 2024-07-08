using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

[Flags]
public enum CalculatorDecision
{
    Unknown = 0,
    Auto = 1,
    Manual = 2,
    InvalidData = 3,
    Error = 4,
    Valid = Auto | Manual,
    InValid = InvalidData | Error | Unknown
}

public interface IFundingResult
{ 
    DateTime CompletedAt { get; }
    CalculatorDecision Decision { get; }
    IEnumerable<string> Errors { get; }
    IFundingAmounts? FundingAmounts { get; }
    string FundingNumber { get; }
    IEnumerable<JsonObject> Result { get; }
    string ResultMessage { get; }
    JsonObject SimpleResult { get; }
    IEnumerable<LicenceDetail> ActionsLog { get; }
}

public class FundingResult : IFundingResult
{
    private FundingResult(string fundingNumber, CalculatorDecision decision, IFundingAmounts? fundingAmounts, IEnumerable<LicenceDetail>? actionsLog, IEnumerable<string>? errors)
    {
        FundingNumber = fundingNumber;
        Decision = decision;
        FundingAmounts = fundingAmounts;
        ActionsLog = actionsLog ?? [];
        ResultMessage = GeneralValidation.IsValidCalculation(decision) ? "Auto Calculated Funding Amounts." : "Calculation error(s). See the logs for more details.";
        Errors = errors ?? [];
        CompletedAt = DateTime.Now;
    }

    public string FundingNumber { get; }
    public CalculatorDecision Decision { get; }
    public IFundingAmounts? FundingAmounts { get; }
    public IEnumerable<JsonObject> Result { get; }
    public string ResultMessage { get; }
    public IEnumerable<string> Errors { get; }
    public DateTime CompletedAt { get; }
    public IEnumerable<LicenceDetail> ActionsLog { get; }

    public static FundingResult Success(string fundingNumber, FundingAmounts fundingAmounts, IEnumerable<LicenceDetail>? actionsLog) => new(fundingNumber, CalculatorDecision.Auto, fundingAmounts, actionsLog, null);
    public static FundingResult InvalidData(string fundingNumber, IEnumerable<string>? errors) => new(fundingNumber, CalculatorDecision.InvalidData, new EmptyFundingAmounts(), null, errors);

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