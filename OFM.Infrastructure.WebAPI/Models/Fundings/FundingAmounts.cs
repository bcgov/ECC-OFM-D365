using System.Collections.ObjectModel;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public record FundingAmounts
{
    // Projected Base Amounts
    public decimal HRTotal_Projected { get; set; } = 0m;
    public decimal HRWagesPaidTimeOff_Projected { get; set; } = 0m;
    public decimal HRBenefits_Projected { get; set; } = 0m;
    public decimal HREmployerHealthTax_Projected { get; set; } = 0m;
    public decimal HRProfessionalDevelopmentHours_Projected { get; set; } = 0m;
    public decimal HRProfessionalDevelopmentExpenses_Projected { get; set; } = 0m;

    public decimal NonHRProgramming_Projected { get; set; } = 0m;
    public decimal NonHRAdmistrative_Projected { get; set; } = 0m;
    public decimal NonHROperational_Projected { get; set; } = 0m;
    public decimal NonHRFacility_Projected { get; set; } = 0m;

    // Parent Fees
    public decimal HRTotal_PF => HRWagesPaidTimeOff_PF + HRBenefits_PF + HREmployerHealthTax_PF + HRProfessionalDevelopmentHours_PF + HRProfessionalDevelopmentExpenses_PF;
    public decimal HRWagesPaidTimeOff_PF { get; set; } = 0m;
    public decimal HRBenefits_PF { get; set; } = 0m;
    public decimal HREmployerHealthTax_PF { get; set; } = 0m;
    public decimal HRProfessionalDevelopmentHours_PF { get; set; } = 0m;
    public decimal HRProfessionalDevelopmentExpenses_PF { get; set; } = 0m;

    public decimal NonHRProgramming_PF { get; set; } = 0m;
    public decimal NonHRAdmistrative_PF { get; set; } = 0m;
    public decimal NonHROperational_PF { get; set; } = 0m;
    public decimal NonHRFacility_PF { get; set; } = 0m;

    // Base Amounts
    public decimal HRTotal { get; set; } = 0m;
    public decimal HRWagesPaidTimeOff { get; set; } = 0m;
    public decimal HRBenefits { get; set; } = 0m;
    public decimal HREmployerHealthTax { get; set; } = 0m;
    public decimal HRProfessionalDevelopmentHours { get; set; } = 0m;
    public decimal HRProfessionalDevelopmentExpenses { get; set; } = 0m;

    public decimal NonHRProgramming { get; set; } = 0m;
    public decimal NonHRAdmistrative { get; set; } = 0m;
    public decimal NonHROperational { get; set; } = 0m;
    public decimal NonHRFacility { get; set; } = 0m;

    //Grand Totals
    public decimal GrandTotal_Projected { get; set; }
    public decimal GrandTotal_PF { get; set; }
    public decimal GrandTotal => GrandTotal_Projected - GrandTotal_PF;

    public DateTime NewCalculationDate { get;  set; }
}
