using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IFundingValidator<T> where T : class
{
    bool Validate(T funding);
    IFundingValidator<T> NextValidator(IFundingValidator<T> next);
}

public static class GeneralValidation
{
    public static bool HasValidApplicationStatus(ofm_application_StatusCode? applicationStatus)
    {
        return applicationStatus switch
        {
            ofm_application_StatusCode.Submitted => true,
            ofm_application_StatusCode.InReview => true,
            ofm_application_StatusCode.AwaitingProvider => true,
            ofm_application_StatusCode.Verified => true,
            ofm_application_StatusCode.Approved => true,
            _ => false
        };
    }

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
                new ValidationResult("Invalid Funding record or Funding record must have a funding agreement number", ["Funding Record / Funding Agreement Number"]), null, null);
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
                new ValidationResult("Base/Core Application must have one of the following statuses: Submitted, In Review, Awaiting Provider, Verified or Approved.", ["Application Status"]), null, null);
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
                new ValidationResult("The Funding record must have a valid funding rate schedule associated.", ["Funding Rate Schedule"]), null, null);
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
                new ValidationResult("The associated application must have a valid Ownership type", ["Associated Application"]), null, null);
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
                new ValidationResult("The associated facility must have a valid licence.", new List<string>() { "Licences" }), null, null);
        }

        var licenceCount = funding.ofm_facility?.ofm_facility_licence?.Where(licence => licence.statuscode == ofm_licence_StatusCode.Active &&
                                                                                             licence.ofm_start_date.GetValueOrDefault().Date <= TimeProvider.System.GetLocalNow().Date &&
                                                                                             (licence.ofm_end_date is null ||
                                                                                             licence.ofm_end_date.Value.Date >= TimeProvider.System.GetLocalNow().Date))?.
                                                                                             Count();


        if (licenceCount == 0)
            throw new ValidationException(
                new ValidationResult("The associated facility must have at least one valid and active licence.", ["Licences"]), null, null);

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
        var licenceDetails = funding.ofm_facility?.ofm_facility_licence?.SelectMany(ld => ld.ofm_licence_licencedetail!);
        if (licenceDetails is null || !licenceDetails.Any())
        {
            throw new ValidationException(
                new ValidationResult("The associated facility must have at least one valid service delivery detail", ["Service Delivery Details"]), null, null);
        }

        var licenceDetailCount = licenceDetails.Where(licenceDetail => licenceDetail.statuscode == ofm_licence_detail_StatusCode.Active).Count();
        if (licenceDetailCount == 0)
            throw new ValidationException(
                new ValidationResult("The associated facility must have at least one valid and active service delivery detail.", ["Service Delivery Details"]), null, null);

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
}