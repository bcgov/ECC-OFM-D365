using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings.Validators;

public interface IFundingValidator<T> where T : class
{
    bool Validate(T funding);
    IFundingValidator<T> NextValidator(IFundingValidator<T> next);
}

public class MustHaveValidApplicationStatusRule : IFundingValidator<Funding>
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