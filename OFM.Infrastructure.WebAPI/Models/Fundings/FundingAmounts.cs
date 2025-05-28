using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public interface IFundingAmounts
{
    DateTime CalculatedOn { get; set; }
    decimal Base_GrandTotal { get; }
    decimal PF_GrandTotal { get; set; }
    decimal Projected_GrandTotal { get; set; }

    decimal Base_HRBenefits { get; }
    decimal PF_HRBenefits { get; set; }
    decimal Projected_HRBenefits { get; set; }

    decimal Base_HREmployerHealthTax { get; }
    decimal PF_HREmployerHealthTax { get; set; }
    decimal Projected_HREmployerHealthTax { get; set; }

    decimal Base_HRProfessionalDevelopmentExpenses { get; }
    decimal PF_HRProfessionalDevelopmentExpenses { get; set; }
    decimal Projected_HRProfessionalDevelopmentExpenses { get; set; }

    decimal Base_HRProfessionalDevelopmentHours { get; }
    decimal PF_HRProfessionalDevelopmentHours { get; set; }
    decimal Projected_HRProfessionalDevelopmentHours { get; set; }

    decimal Base_HRTotal { get; }
    decimal PF_HRTotal { get; }
    decimal Projected_HRTotal { get; set; }

    decimal Base_HRWagesPaidTimeOff { get; }
    decimal PF_HRWagesPaidTimeOff { get; set; }
    decimal Projected_HRWagesPaidTimeOff { get; set; }

    decimal Base_NonHRAdmistrative { get; }
    decimal PF_NonHRAdmistrative { get; set; }
    decimal Projected_NonHRAdmistrative { get; set; }

    decimal Base_NonHRFacility { get; }
    decimal PF_NonHRFacility { get; set; }
    decimal Projected_NonHRFacility { get; set; }

    decimal Base_NonHROperational { get; }
    decimal PF_NonHROperational { get; set; }
    decimal Projected_NonHROperational { get; set; }

    decimal Base_NonHRProgramming { get; }
    decimal PF_NonHRProgramming { get; set; }
    decimal Projected_NonHRProgramming { get; set; }
    decimal Adjusted_FTE { get; set; }
    bool Equals(FundingAmounts? other);
    bool Equals(object? obj);
    int GetHashCode();
    string ToString();
}

public record FundingAmounts : IFundingAmounts
{
    const double LOWER_LIMIT_AMOUNT = 0d;
    const double UPPER_LIMIT_AMOUNT = 100_000_000d;

    // Projected Amounts
    public decimal Projected_HRTotal { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HRWagesPaidTimeOff { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HRBenefits { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HREmployerHealthTax { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HRProfessionalDevelopmentHours { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HRProfessionalDevelopmentExpenses { get; set; } = 0m;

    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_NonHRProgramming { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_NonHRAdmistrative { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_NonHROperational { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_NonHRFacility { get; set; } = 0m;

    // Parent Fees
    public decimal PF_HRTotal => PF_HRWagesPaidTimeOff + PF_HRBenefits + PF_HREmployerHealthTax + PF_HRProfessionalDevelopmentHours + PF_HRProfessionalDevelopmentExpenses;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HRWagesPaidTimeOff { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HRBenefits { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HREmployerHealthTax { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HRProfessionalDevelopmentHours { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HRProfessionalDevelopmentExpenses { get; set; } = 0m;

    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_NonHRProgramming { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_NonHRAdmistrative { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_NonHROperational { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_NonHRFacility { get; set; } = 0m;
    public decimal Adjusted_FTE { get; set; } = 0m;
    // Base Amounts (projected - parent fees)
    public decimal Base_HRTotal => Projected_HRTotal - PF_HRTotal;
    public decimal Base_HRWagesPaidTimeOff => Projected_HRWagesPaidTimeOff - PF_HRWagesPaidTimeOff;
    public decimal Base_HRBenefits => Projected_HRBenefits - PF_HRBenefits;
    public decimal Base_HREmployerHealthTax => Projected_HREmployerHealthTax - PF_HREmployerHealthTax;
    public decimal Base_HRProfessionalDevelopmentHours => Projected_HRProfessionalDevelopmentHours - PF_HRProfessionalDevelopmentHours;
    public decimal Base_HRProfessionalDevelopmentExpenses => Projected_HRProfessionalDevelopmentExpenses - PF_HRProfessionalDevelopmentExpenses;

    public decimal Base_NonHRProgramming => Projected_NonHRProgramming - PF_NonHRProgramming;
    public decimal Base_NonHRAdmistrative => Projected_NonHRAdmistrative - PF_NonHRAdmistrative;
    public decimal Base_NonHROperational => Projected_NonHROperational - PF_NonHROperational;
    public decimal Base_NonHRFacility => Projected_NonHRFacility - PF_NonHRFacility;

    //Grand Totals
    public decimal Projected_GrandTotal { get; set; }
    public decimal PF_GrandTotal { get; set; }
    public decimal Base_GrandTotal => Projected_GrandTotal - PF_GrandTotal;

    public DateTime CalculatedOn { get; set; }
}

public record EmptyFundingAmounts : IFundingAmounts { 
    public DateTime CalculatedOn { get; set; } 
    public decimal Base_GrandTotal { get; set; }
    public decimal PF_GrandTotal { get; set; } 
    public decimal Projected_GrandTotal { get; set; } 
    public decimal Base_HRBenefits { get; set; }
    public decimal PF_HRBenefits { get; set; } 
    public decimal Projected_HRBenefits { get; set; } 
    public decimal Base_HREmployerHealthTax { get; set; }
    public decimal PF_HREmployerHealthTax { get; set; } 
    public decimal Projected_HREmployerHealthTax { get; set; } 
    public decimal Base_HRProfessionalDevelopmentExpenses { get; set; }
    public decimal PF_HRProfessionalDevelopmentExpenses { get; set; } 
    public decimal Projected_HRProfessionalDevelopmentExpenses { get; set; } 
    public decimal Base_HRProfessionalDevelopmentHours { get; set; }
    public decimal PF_HRProfessionalDevelopmentHours { get; set; } 
    public decimal Projected_HRProfessionalDevelopmentHours { get; set; } 
    public decimal Base_HRTotal { get; set; }
    public decimal PF_HRTotal { get; set; }
    public decimal Projected_HRTotal { get; set; } 
    public decimal Base_HRWagesPaidTimeOff { get; set; }
    public decimal PF_HRWagesPaidTimeOff { get; set; } 
    public decimal Projected_HRWagesPaidTimeOff { get; set; } 
    public decimal Base_NonHRAdmistrative { get; set; }
    public decimal PF_NonHRAdmistrative { get; set; } 
    public decimal Projected_NonHRAdmistrative { get; set; }
    public decimal Base_NonHRFacility { get; set; }
    public decimal PF_NonHRFacility { get; set; }
    public decimal Projected_NonHRFacility { get; set; }
    public decimal Base_NonHROperational { get; set; }
    public decimal PF_NonHROperational { get; set; } 
    public decimal Projected_NonHROperational { get; set; } 
    public decimal Base_NonHRProgramming { get; set; }
    public decimal PF_NonHRProgramming { get; set; } 
    public decimal Projected_NonHRProgramming { get; set; }
    public decimal Adjusted_FTE { get; set; }

    public bool Equals(FundingAmounts? other) { throw new NotImplementedException(); } };