using OFM.Infrastructure.WebAPI.Models.Fundings;

namespace OFM.Infrastructure.WebAPI.Extensions;

public static class OFMExtensions
{
    public static bool IsValidFundingResult(this FundingResult fundingResult)
    {
        //var check1 = fundingResult.FundingEnvelopeArray is [FundingAmounts, FundingAmounts, FundingAmounts];
        //var check2 = fundingResult.Decision == FundingDecision.Aproved;

        var check1 = (fundingResult.Decision is FundingDecision.AutoApproved, FundingDecision.Aproved);
        var check2 = (fundingResult.Decision is FundingDecision.AutoTempApproved, FundingDecision.Unknown);

        return check1.Item1 && check2.Item1;
    }
}
