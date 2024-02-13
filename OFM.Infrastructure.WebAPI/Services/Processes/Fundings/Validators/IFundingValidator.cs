using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings.Validators;

public interface IFundingValidator<T> where T : class
{
    bool Validate(T funding);
    //ValidationResult Validate2(T funding);
    IFundingValidator<T> NextValidator(IFundingValidator<T> next);
}

public class ValidApplicationStatusRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        //if (funding.statuscode == ofm_application_StatusCode.Cancelled)
        //{
        //    return false;
        //    throw new ValidationException(
        //        new ValidationResult("Application must be submitted", new List<string>() { "Application Status" }), null, null);

        //    //return FundingResult.Rejected(funding.ofm_funding_agreement_number, null, null);
        //}

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
    
}

public class MustHaveFundingNumberBaseRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        if (string.IsNullOrEmpty(funding.ofm_funding_number))
        {
            throw new ValidationException(
                new ValidationResult("Application must have a funding agreement number", new List<string>() { "Funding Agreement Number" }), null, null);
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
                new ValidationResult("Funding record must have a funding rate schedule", new List<string>() { "Funding Rate Schedule" }), null, null);
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

public class ValidCoreServiceRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        //if (funding.HasValidCoreServices())
        //{
        //    return false;

        //    throw new ValidationException(
        //        new ValidationResult("Application must have a file number", new List<string>() { "Application File Number" }), null, null);

        //    //return FundingResult.Rejected(funding.ofm_funding_agreement_number, null, null);
        //}

        _next?.Validate(funding);

        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding>? next)
    {
        _next = next;
        return next;
    }
}

public class ApplicationLastModifiedRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        //if (funding.ofm_summary_provider_last_updated.Value <= DateTime.UtcNow.AddDays(-60))
        //{
        //    throw new ValidationException(
        //        new ValidationResult("Application must be submitted in the last 60 days", new List<string>() { "Provider's Last Modified" }), null, null);

        //    //return FundingResult.Rejected(funding.ofm_funding_agreement_number, null, null);
        //}

        _next?.Validate(funding);
        return true;
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding> next)
    {
        _next = next;
        return next;
    }

}

public class GoodStandingRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        //if (CheckForGoodStanding(funding.ofm_facility))
        //{
        //    throw new ValidationException(
        //        new ValidationResult("Facility must be in good standing", new List<string>() { "Good Standing" }), null, null);

        //    //return FundingResult.Rejected(funding.ofm_funding_agreement_number, null, null);

        //    //return false;
        //}

        _next?.Validate(funding);

        return true;
    }

    private bool CheckForGoodStanding(EntityReference ofm_facility)
    {
        throw new NotImplementedException();
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding> next)
    {
        _next = next;
        return next;
    }
}

public class DuplicateLicenceTypeRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        //if (CheckForGoodStanding(funding.ofm_facility))
        //{
        //    throw new ValidationException(
        //        new ValidationResult("Facility must be in good standing", new List<string>() { "Good Standing" }), null, null);

        //    //return FundingResult.Rejected(funding.ofm_funding_agreement_number, null, null);

        //    //return false;
        //}

        _next?.Validate(funding);

        return true;
    }

    private bool CheckForGoodStanding(EntityReference ofm_facility)
    {
        throw new NotImplementedException();
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding> next)
    {
        _next = next;
        return next;
    }
}

public class SplitRoomRule : IFundingValidator<Funding>
{
    private IFundingValidator<Funding>? _next;

    public bool Validate(Funding funding)
    {
        //if (CheckForGoodStanding(funding.ofm_facility))
        //{
        //    throw new ValidationException(
        //        new ValidationResult("Facility must be in good standing", new List<string>() { "Good Standing" }), null, null);

        //    //return FundingResult.Rejected(funding.ofm_funding_agreement_number, null, null);

        //    //return false;
        //}

        _next?.Validate(funding);

        return true;
    }

    private bool CheckForGoodStanding(EntityReference ofm_facility)
    {
        throw new NotImplementedException();
    }

    public IFundingValidator<Funding> NextValidator(IFundingValidator<Funding> next)
    {
        _next = next;
        return next;
    }
}