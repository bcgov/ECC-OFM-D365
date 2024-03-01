using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public record FundingAmounts
{
    const double LOWER_LIMIT_AMOUNT = 0d;
    const double UPPER_LIMIT_AMOUNT = 100_000_000d;

    // Projected Base Amounts
    public decimal HRTotal_Projected { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HRWagesPaidTimeOff_Projected { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HRBenefits_Projected { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HREmployerHealthTax_Projected { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HRProfessionalDevelopmentHours_Projected { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HRProfessionalDevelopmentExpenses_Projected { get; set; } = 0m;

    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal NonHRProgramming_Projected { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal NonHRAdmistrative_Projected { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal NonHROperational_Projected { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal NonHRFacility_Projected { get; set; } = 0m;

    // Parent Fees
    public decimal HRTotal_PF => HRWagesPaidTimeOff_PF + HRBenefits_PF + HREmployerHealthTax_PF + HRProfessionalDevelopmentHours_PF + HRProfessionalDevelopmentExpenses_PF;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HRWagesPaidTimeOff_PF { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HRBenefits_PF { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HREmployerHealthTax_PF { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HRProfessionalDevelopmentHours_PF { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal HRProfessionalDevelopmentExpenses_PF { get; set; } = 0m;

    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal NonHRProgramming_PF { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal NonHRAdmistrative_PF { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal NonHROperational_PF { get; set; } = 0m;
    [Required(ErrorMessage = "Required")]
    [Range(LOWER_LIMIT_AMOUNT, UPPER_LIMIT_AMOUNT, ErrorMessage = "The value must be greater than or equal to 0 or less than 100_000_000")]
    public decimal NonHRFacility_PF { get; set; } = 0m;

    // Base Amounts (projected - parent fees)
    public decimal HRTotal => HRTotal_Projected - HRTotal_PF;
    public decimal HRWagesPaidTimeOff => HRWagesPaidTimeOff_Projected - HRWagesPaidTimeOff_PF;
    public decimal HRBenefits => HRBenefits_Projected - HRBenefits_PF;
    public decimal HREmployerHealthTax => HREmployerHealthTax_Projected - HREmployerHealthTax_PF;
    public decimal HRProfessionalDevelopmentHours => HRProfessionalDevelopmentHours_Projected - HRProfessionalDevelopmentHours_PF;
    public decimal HRProfessionalDevelopmentExpenses => HRProfessionalDevelopmentExpenses_Projected - HRProfessionalDevelopmentExpenses_PF;

    public decimal NonHRProgramming => NonHRProgramming_Projected - NonHRProgramming_PF;
    public decimal NonHRAdmistrative => NonHRAdmistrative_Projected - NonHRAdmistrative_PF;
    public decimal NonHROperational => NonHROperational_Projected - NonHROperational_PF;
    public decimal NonHRFacility => NonHRFacility_Projected - NonHRFacility_PF;

    //Grand Totals
    public decimal GrandTotal_Projected { get; set; }
    public decimal GrandTotal_PF { get; set; }
    public decimal GrandTotal => GrandTotal_Projected - GrandTotal_PF;

    public DateTime NewCalculationDate { get;  set; }
}
