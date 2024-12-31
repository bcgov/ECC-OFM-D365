using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Extensions;
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
        licence.ofm_start_date <= ((funding.ofm_application!.ofm_summary_submittedon)?.ToLocalPST().Date ?? (funding.ofm_application!.createdon)?.ToLocalPST().Date ?? new DateTime()) &&
        (licence.ofm_end_date is null || licence.ofm_end_date >= ((funding.ofm_application!.ofm_summary_submittedon)?.ToLocalPST().Date ?? (funding.ofm_application!.createdon)?.ToLocalPST().Date ?? new DateTime())))?.Count();

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

public class MustHaveAtLeastOneOperationalSpaceRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        var operationalSpaces = funding.ofm_facility?.ofm_facility_licence?.SelectMany(ld => ld
                                .ofm_licence_licencedetail!).Where(licDetail => licDetail.statuscode 
                                == ofm_licence_detail_StatusCode.Active && (licDetail.ofm_operational_spaces 
                                < 1 || licDetail.ofm_operational_spaces == null)).Count();

        if (operationalSpaces > 0)
        {
            throw new ValidationException(
                new ValidationResult("The Service Delivery Details must have valid Operational Spaces.", ["Service Delivery Details"]), null, null);
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

public class MustHaveWeeksInOperationRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        var weeksInOperation = funding.ofm_facility?.ofm_facility_licence?.SelectMany(ld => ld
                               .ofm_licence_licencedetail!).Where(licDetail => licDetail.statuscode 
                               == ofm_licence_detail_StatusCode.Active && (licDetail.ofm_weeks_in_operation 
                               < 1 || licDetail.ofm_weeks_in_operation == null)).Count();

        if (weeksInOperation > 0)
        {
            throw new ValidationException(
                new ValidationResult("The Service Delivery Details must have valid Weeks in Operation.", ["Service Delivery Details"]), null, null);
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

public class MustHaveHoursOfOperationRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        var operationHoursFrom = funding.ofm_facility?.ofm_facility_licence?.SelectMany(ld => ld
                                 .ofm_licence_licencedetail!).Where(licDetail => licDetail.statuscode 
                                 == ofm_licence_detail_StatusCode.Active && licDetail.ofm_operation_hours_from 
                                 == null).Count();
        var operationHoursTo = funding.ofm_facility?.ofm_facility_licence?.SelectMany(ld => ld
                               .ofm_licence_licencedetail!).Where(licDetail => licDetail.statuscode 
                               == ofm_licence_detail_StatusCode.Active && licDetail.ofm_operation_hours_to 
                               == null).Count();

        if (operationHoursFrom > 0 || operationHoursTo > 0)
        {
            throw new ValidationException(
                new ValidationResult("The Service Delivery Details must have valid Operation Hours.", ["Service Delivery Details"]), null, null);
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

public class MustHaveDaysOfWeekRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        var weekDays = funding.ofm_facility?.ofm_facility_licence?.SelectMany(ld => ld
                                 .ofm_licence_licencedetail!).Where(licDetail => licDetail.statuscode
                                 == ofm_licence_detail_StatusCode.Active && licDetail.ofm_week_days
                                 == null).Count();

        if (weekDays > 0)
        {
            throw new ValidationException(
                new ValidationResult("The Service Delivery Details must have valid Week Days when the ChildCare is operating.", ["Service Delivery Details"]), null, null);
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