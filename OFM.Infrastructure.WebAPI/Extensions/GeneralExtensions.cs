using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Extensions;

public static class GeneralExtensions
{
    public static bool IsValidFundingResult(this FundingResult fundingResult)
    {
        //var check1 = fundingResult.FundingEnvelopeArray is [FundingAmounts, FundingAmounts, FundingAmounts];
        //var check2 = fundingResult.Decision == FundingDecision.Aproved;

        var decisionCheck = (fundingResult.Decision is not FundingDecision.InvalidData, FundingDecision.AutoRejected, FundingDecision.Rejected);
        //var check2 = (fundingResult.Decision is FundingDecision.AutoTempApproved, FundingDecision.Unknown);

        //return check1.Item1 && check2.Item1;

        // Validating the fundingResult model
        ICollection<ValidationResult> results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(fundingResult.FundingAmounts!, new ValidationContext(fundingResult.FundingAmounts!), results, validateAllProperties: true))
        {
            foreach (ValidationResult result in results)
            {
                _ = fundingResult.Errors.Append(result.ErrorMessage);
            }
        }

        return decisionCheck.Item1 && !fundingResult.Errors.Any();
    }
}
