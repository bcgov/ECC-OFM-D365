using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IFundingValidator<T> where T : class
{
    bool Validate(T funding);
    IFundingValidator<T> NextValidator(IFundingValidator<T> next);
}

public static class GeneralValidation {
    public static bool HasValidApplicationStatus(ofm_application_StatusCode? applicationStatus) => applicationStatus switch
                                                                                                  {
                                                                                                      ofm_application_StatusCode.Submitted => true,
                                                                                                      ofm_application_StatusCode.InReview => true,
                                                                                                      ofm_application_StatusCode.Verified => true,
                                                                                                      ofm_application_StatusCode.Approved => true,
                                                                                                      _ => false
                                                                                                  };

    public static bool IsValidCalculation(CalculatorDecision decision) =>
                        decision switch
                        {
                            CalculatorDecision.Auto => true,
                            CalculatorDecision.Manual => true,
                            _ => false
                        };

    public static bool IsValidFundingResult(this FundingResult fundingResult)
    {
        // Validating the FundingAmounts model
        ICollection<ValidationResult> results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(fundingResult.FundingAmounts!, new ValidationContext(fundingResult.FundingAmounts!), results, validateAllProperties: true))
        {
            foreach (ValidationResult result in results)
            {
                _ = fundingResult.Errors.Append(result.ErrorMessage);
            }
        }

        return IsValidCalculation(fundingResult.Decision) && !fundingResult.Errors.Any();
    }
}

public class MustHaveFundingNumberBaseRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        if (funding is null || string.IsNullOrEmpty(funding.ofm_funding_number))
        {
            throw new ValidationException(
                new ValidationResult("Invalid Funding record or Funding record must have a funding agreement number", new List<string>() { "Funding Record / Funding Agreement Number" }), null, null);
        }

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
}

public class MustHaveValidApplicationStatusRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        if (!GeneralValidation.HasValidApplicationStatus(funding.ofm_application?.statuscode))
        {
            throw new ValidationException(
                new ValidationResult("Main Application must be submitted", new List<string>() { "Application Status" }), null, null);
        }

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
}

public class MustHaveValidRateScheduleRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        if (funding.ofm_rate_schedule is null)
        {
            throw new ValidationException(
                new ValidationResult("Funding record must have a funding rate schedule associated.", new List<string>() { "Funding Rate Schedule" }), null, null);
        }

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
}

public class MustHaveValidOwnershipTypeRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        if (funding.ofm_application?.ofm_summary_ownership is null)
        {
            throw new ValidationException(
                new ValidationResult("Associated application must have a valid ownership type", new List<string>() { "Associated Application" }), null, null);
        }

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
}

public class MustHaveValidLicenceRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        if (funding.ofm_facility?.ofm_facility_licence is null)
        {
            throw new ValidationException(
                new ValidationResult("Associated facility must have a valid licence", new List<string>() { "Associated Facility" }), null, null);
        }

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
}

public class MustHaveAtLeastOneValidLicenceDetailRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        var licenceDetails = funding.ofm_facility?.ofm_facility_licence?.SelectMany(ld => ld.ofm_licence_licencedetail);
        if (licenceDetails is null || !licenceDetails.Any())
        {
            throw new ValidationException(
                new ValidationResult("Associated facility must have at least one valid core service(licence detail)", new List<string>() { "Associated Facility's Core Services" }), null, null);
        }

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
}