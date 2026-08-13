using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public interface IFundingAmounts
{
    DateTime CalculatedOn { get; set; }
    decimal Base_GrandTotal { get; }
    decimal PF_GrandTotal { get; set; }
    decimal Projected_GrandTotal { get; set; }
    decimal Base_GrandTotal_Reallocation { get; }
    decimal PF_GrandTotal_Reallocation { get; set; }
    decimal Projected_GrandTotal_Reallocation { get; set; }

    decimal Base_HRBenefits { get; }
    decimal PF_HRBenefits { get; set; }
    decimal Projected_HRBenefits { get; set; }
    decimal Base_HRBenefits_Reallocation { get; }
    decimal PF_HRBenefits_Reallocation { get; set; }
    decimal Projected_HRBenefits_Reallocation { get; set; }

    decimal Base_HREmployerHealthTax { get; }
    decimal PF_HREmployerHealthTax { get; set; }
    decimal Projected_HREmployerHealthTax { get; set; }
    decimal Base_HREmployerHealthTax_Reallocation { get; }
    decimal PF_HREmployerHealthTax_Reallocation { get; set; }
    decimal Projected_HREmployerHealthTax_Reallocation { get; set; }

    decimal Base_HRProfessionalDevelopmentExpenses { get; }
    decimal PF_HRProfessionalDevelopmentExpenses { get; set; }
    decimal Projected_HRProfessionalDevelopmentExpenses { get; set; }
    decimal Base_HRProfessionalDevelopmentExpenses_Reallocation { get; }
    decimal PF_HRProfessionalDevelopmentExpenses_Reallocation { get; set; }
    decimal Projected_HRProfessionalDevelopmentExpenses_Reallocation { get; set; }

    decimal Base_HRProfessionalDevelopmentHours { get; }
    decimal PF_HRProfessionalDevelopmentHours { get; set; }
    decimal Projected_HRProfessionalDevelopmentHours { get; set; }
    decimal Base_HRProfessionalDevelopmentHours_Reallocation { get; }
    decimal PF_HRProfessionalDevelopmentHours_Reallocation { get; set; }
    decimal Projected_HRProfessionalDevelopmentHours_Reallocation { get; set; }

    decimal Base_HRTotal { get; }
    decimal PF_HRTotal { get; }
    decimal Projected_HRTotal { get; set; }
    decimal Base_HRTotal_Reallocation { get; }
    decimal PF_HRTotal_Reallocation { get; }
    decimal Projected_HRTotal_Reallocation { get; set; }

    decimal Base_HRWagesPaidTimeOff { get; }
    decimal PF_HRWagesPaidTimeOff { get; set; }
    decimal Projected_HRWagesPaidTimeOff { get; set; }
    decimal Base_HRWagesPaidTimeOff_Reallocation { get; }
    decimal PF_HRWagesPaidTimeOff_Reallocation { get; set; }
    decimal Projected_HRWagesPaidTimeOff_Reallocation { get; set; }

    decimal Base_NonHRAdmistrative { get; }
    decimal PF_NonHRAdmistrative { get; set; }
    decimal Projected_NonHRAdmistrative { get; set; }
    decimal Base_NonHRAdmistrative_Reallocation { get; }
    decimal PF_NonHRAdmistrative_Reallocation { get; set; }
    decimal Projected_NonHRAdministrative_Reallocation { get; set; }

    decimal Base_NonHRFacility { get; }
    decimal PF_NonHRFacility { get; set; }
    decimal Projected_NonHRFacility { get; set; }
    decimal Base_NonHRFacility_Reallocation { get; }
    decimal PF_NonHRFacility_Reallocation { get; set; }
    decimal Projected_NonHRFacility_Reallocation { get; set; }

    decimal Base_NonHROperational { get; }
    decimal PF_NonHROperational { get; set; }
    decimal Projected_NonHROperational { get; set; }
    decimal Base_NonHROperational_Reallocation { get; }
    decimal PF_NonHROperational_Reallocation { get; set; }
    decimal Projected_NonHROperational_Reallocation { get; set; }

    decimal Base_NonHRProgramming { get; }
    decimal PF_NonHRProgramming { get; set; }
    decimal Projected_NonHRProgramming { get; set; }
    decimal Base_NonHRProgramming_Reallocation { get; }
    decimal PF_NonHRProgramming_Reallocation { get; set; }
    decimal Projected_NonHRProgramming_Reallocation { get; set; }

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

    // Projected Reallocation Amounts
    public decimal Projected_HRTotal_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HRWagesPaidTimeOff_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HRBenefits_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HREmployerHealthTax_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HRProfessionalDevelopmentHours_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_HRProfessionalDevelopmentExpenses_Reallocation { get; set; } = 0m;

    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_NonHRProgramming_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_NonHRAdministrative_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_NonHROperational_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal Projected_NonHRFacility_Reallocation { get; set; } = 0m;

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

    // Reallocated Parent Fees
    public decimal PF_HRTotal_Reallocation => PF_HRWagesPaidTimeOff_Reallocation + PF_HRBenefits_Reallocation + PF_HREmployerHealthTax_Reallocation 
        + PF_HRProfessionalDevelopmentHours_Reallocation + PF_HRProfessionalDevelopmentExpenses_Reallocation;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HRWagesPaidTimeOff_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HRBenefits_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HREmployerHealthTax_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HRProfessionalDevelopmentHours_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_HRProfessionalDevelopmentExpenses_Reallocation { get; set; } = 0m;

    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_NonHRProgramming_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_NonHRAdmistrative_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_NonHROperational_Reallocation { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal PF_NonHRFacility_Reallocation { get; set; } = 0m;

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

    // Reallocated Base Amounts (projected reallocation - parent fees reallocation)
    public decimal Base_HRTotal_Reallocation => Projected_HRTotal_Reallocation - PF_HRTotal_Reallocation;
    public decimal Base_HRWagesPaidTimeOff_Reallocation => Projected_HRWagesPaidTimeOff_Reallocation - PF_HRWagesPaidTimeOff_Reallocation;
    public decimal Base_HRBenefits_Reallocation => Projected_HRBenefits_Reallocation - PF_HRBenefits_Reallocation;
    public decimal Base_HREmployerHealthTax_Reallocation => Projected_HREmployerHealthTax_Reallocation - PF_HREmployerHealthTax_Reallocation;
    public decimal Base_HRProfessionalDevelopmentHours_Reallocation => Projected_HRProfessionalDevelopmentHours_Reallocation - PF_HRProfessionalDevelopmentHours_Reallocation;
    public decimal Base_HRProfessionalDevelopmentExpenses_Reallocation => Projected_HRProfessionalDevelopmentExpenses_Reallocation - PF_HRProfessionalDevelopmentExpenses_Reallocation;

    public decimal Base_NonHRProgramming_Reallocation => Projected_NonHRProgramming_Reallocation - PF_NonHRProgramming_Reallocation;
    public decimal Base_NonHRAdmistrative_Reallocation => Projected_NonHRAdministrative_Reallocation - PF_NonHRAdmistrative_Reallocation;
    public decimal Base_NonHROperational_Reallocation => Projected_NonHROperational_Reallocation - PF_NonHROperational_Reallocation;
    public decimal Base_NonHRFacility_Reallocation => Projected_NonHRFacility_Reallocation - PF_NonHRFacility_Reallocation;
    
    //Grand Totals
    public decimal Projected_GrandTotal { get; set; }
    public decimal PF_GrandTotal { get; set; }
    public decimal Base_GrandTotal => Projected_GrandTotal - PF_GrandTotal;

    //Grand Totals Reallocation
    public decimal Projected_GrandTotal_Reallocation { get; set; }
    public decimal PF_GrandTotal_Reallocation { get; set; }
    public decimal Base_GrandTotal_Reallocation => Projected_GrandTotal_Reallocation - PF_GrandTotal_Reallocation;

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
    public decimal Projected_HRTotal_Reallocation { get; set; }
    public decimal Projected_HRWagesPaidTimeOff_Reallocation { get; set; }
    public decimal Projected_HRBenefits_Reallocation { get; set; }
    public decimal Projected_HREmployerHealthTax_Reallocation { get; set; }
    public decimal Projected_HRProfessionalDevelopmentHours_Reallocation { get; set; }
    public decimal Projected_HRProfessionalDevelopmentExpenses_Reallocation { get; set; }
    public decimal Projected_NonHRProgramming_Reallocation { get; set; }
    public decimal Projected_NonHRAdministrative_Reallocation { get; set; }
    public decimal Projected_NonHROperational_Reallocation { get; set; }
    public decimal Projected_NonHRFacility_Reallocation { get; set; }
    public decimal PF_HRTotal_Reallocation { get; set; }
    public decimal PF_HRWagesPaidTimeOff_Reallocation { get; set; }
    public decimal PF_HRBenefits_Reallocation { get; set; }
    public decimal PF_HREmployerHealthTax_Reallocation { get; set; }
    public decimal PF_HRProfessionalDevelopmentExpenses_Reallocation { get; set; }
    public decimal PF_HRProfessionalDevelopmentHours_Reallocation { get; set; }
    public decimal PF_NonHRProgramming_Reallocation { get; set; }
    public decimal PF_NonHRAdmistrative_Reallocation { get; set; }
    public decimal PF_NonHROperational_Reallocation { get; set; }
    public decimal PF_NonHRFacility_Reallocation { get; set; }
    public decimal Base_HRTotal_Reallocation { get; set; }
    public decimal Base_HRWagesPaidTimeOff_Reallocation { get; set; }
    public decimal Base_HRBenefits_Reallocation { get; set; }
    public decimal Base_HREmployerHealthTax_Reallocation { get; set; }
    public decimal Base_HRProfessionalDevelopmentHours_Reallocation { get; set; }
    public decimal Base_HRProfessionalDevelopmentExpenses_Reallocation { get; set; }
    public decimal Base_NonHRProgramming_Reallocation { get; set; }
    public decimal Base_NonHRAdmistrative_Reallocation { get; set; }
    public decimal Base_NonHROperational_Reallocation { get; set; }
    public decimal Base_NonHRFacility_Reallocation { get; set; }
    public decimal Projected_GrandTotal_Reallocation { get; set; }
    public decimal PF_GrandTotal_Reallocation { get; set; }
    public decimal Base_GrandTotal_Reallocation { get; set; }

    public bool Equals(FundingAmounts? other) { throw new NotImplementedException(); } };