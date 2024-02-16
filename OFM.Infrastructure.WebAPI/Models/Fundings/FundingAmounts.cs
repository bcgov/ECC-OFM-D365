using System.Collections.ObjectModel;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public class TypedSet<T> : KeyedCollection<Type, T>
{
    protected override Type GetKeyForItem(T item)
    {
        return item.GetType();
    }
}

public enum FundingAmountType
{
    Base,
    ParentFees,
    Projected
}

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
    public decimal HRTotal_PF { get; set; } = 0m;
    public decimal HRWagesPaidTimeOff_PF { get; set; } = 0m;
    public decimal HRBenefits_PF { get; set; } = 0m;
    public decimal HREmployerHealthTax_PF { get; set; } = 0m;
    public decimal HRProfessionalDevelopmentHours_PF { get; set; } = 0m;
    public decimal HRProfessionalDevelopmentExpenses_PF{ get; set; } = 0m;

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
    public decimal GrandTotal_Projected { get { return HRTotal_Projected + NonHRProgramming_Projected + NonHRAdmistrative_Projected + NonHROperational_Projected + NonHRFacility_Projected ; } }
    public decimal GrandTotal_PF { get { return HRTotal_PF + NonHRProgramming_PF + NonHRAdmistrative_PF + NonHROperational_PF + NonHRFacility_PF; } }
    public decimal GrandTotal { get { return HRTotal + NonHRProgramming + NonHRAdmistrative + NonHROperational + NonHRFacility; } }
}
