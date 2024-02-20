using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IFundingValidator<T> where T : class
{
    bool Validate(T funding);
    IFundingValidator<T> NextValidator(IFundingValidator<T> next);
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
        if (funding.ofm_application!.statuscode >= ofm_application_StatusCode.Submitted)
        {
            throw new ValidationException(
                new ValidationResult("Application must be at least submitted", new List<string>() { "Application Status" }), null, null);
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