using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings.Validators;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class FundingCalculator
{
    private readonly IFundingRepository _fundingRepository;
    private readonly IProviderProfileRepository _providerRepository;
    private readonly ID365DataService _d365dataService;
    private FundingResult? _fundingResult;
    private readonly RateSchedule _rateSchedule;
    private readonly Funding _funding;

    public FundingCalculator(Funding funding, IEnumerable<RateSchedule> rateSchedules, IFundingRepository fundingRepository)
    {
        _funding = funding;
        _rateSchedule = rateSchedules.First(sch => sch.Id == _funding?.ofm_rate_schedule?.ofm_rate_scheduleid);
        _fundingRepository = fundingRepository;
    }

    private IEnumerable<LicenceDetail> LicenceDetails
    {
        get
        {
            var licenceDetails = _funding.ofm_facility!.ofm_facility_licence.SelectMany(licence => licence.ofm_licence_licencedetail);
            foreach (var service in licenceDetails)
            {
                service.RateSchedule = _rateSchedule;
                service.ApplySplitRoomCondition = ApplySplitRoomCondition;
                service.NewSpacesAllocation = _funding.ofm_funding_spaceallocation;
            }

            return licenceDetails;
        }
    }
    private int TotalSpaces => LicenceDetails.Sum(detail => detail.ofm_operational_spaces!.Value);
    private ecc_Ownership Ownership => _funding.ofm_application!.ofm_summary_ownership!.Value;
    private bool ApplySplitRoomCondition => _funding.ofm_apply_room_split_condition!.Value;

    /// <summary>
    /// Apply the employer health tax at calculator level
    /// </summary>
    private decimal EmployerHealthTax
    {
        get
        {
            var tempData = new { Ownership = Ownership, Renumeration = LicenceDetails.Sum(ld => ld.TotalRenumeration) };
            var threshold = tempData switch
            {
                { Ownership: ecc_Ownership.Private, Renumeration: < 1_500_000m, Renumeration: > 500_000m } => _rateSchedule.ofm_for_profit_eht_over_500k!.Value,
                { Ownership: ecc_Ownership.Private, Renumeration: > 1_500_000m } => _rateSchedule.ofm_for_profit_eht_over_1_5m!.Value,
                { Ownership: ecc_Ownership.Notforprofit, Renumeration: > 1_500_000m } => _rateSchedule.ofm_not_for_profit_eht_over_1_5m!.Value,
                null => throw new ArgumentNullException(nameof(FundingCalculator), "Can't calculate EHT threshold with null value"),
                _ => 0m
            };

            return threshold * LicenceDetails.Sum(ld => ld.TotalRenumeration);
        }
    }

    public async Task<bool> Evaluate()
    {
        // NOTE: For HR envelopes, apply the FTE minimum (0.5) at the combined care type level for duplicates.(Decision made on Feb 07, 2024 with the Ministry)

        #region Validation

        var fundingRules = new MustHaveFundingNumberBaseRule();
        fundingRules.NextValidator(new MustHaveValidApplicationStatusRule())
                    .NextValidator(new MustHaveValidRateScheduleRule());
        try
        {
            var validationResult = fundingRules.Validate(_funding);
        }
        catch (ValidationException exp)
        {
            _fundingResult = FundingResult.AutoRejected(_funding.ofm_funding_number, null, null, new[] { exp.Message });
        }

        #endregion

        FundingAmounts fundingAmounts = new()
        {
            //Projected Base Amounts
            HRTotal_Projected = LicenceDetails.Sum(cs => cs.TotalRenumeration),
            HRWagesPaidTimeOff_Projected = LicenceDetails.Sum(cs => cs.TotalStaffingCost),
            HRBenefits_Projected = LicenceDetails.Sum(cs => cs.TotalBenefitsCostPerYear),
            HREmployerHealthTax_Projected = EmployerHealthTax,
            HRProfessionalDevelopmentHours_Projected = LicenceDetails.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),
            HRProfessionalDevelopmentExpenses_Projected = LicenceDetails.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),

            //NonHRProgramming_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),
            //NonHRAdmistrative_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),
            //NonHROperational_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses),
            //NonHRFacility_Projected = CoreServices.Sum(cs => cs.TotalProfessionalDevelopmentExpenses)

            ////Parent Fees
            //ofm_envelope_hr_total_pf = fm.HRTotal_PF,
            //ofm_envelope_hr_wages_paidtimeoff_pf = fm.HRWagesPaidTimeOff_PF,
            //ofm_envelope_hr_benefits_pf = fm.HRBenefits_PF,
            //ofm_envelope_hr_employerhealthtax_pf = fm.HREmployerHealthTax_PF,
            //ofm_envelope_hr_prodevexpenses_pf = fm.HRProfessionalDevelopmentExpenses_PF,

            //ofm_envelope_programming_pf = fm.NonHRProgramming_PF,
            //ofm_envelope_administrative_pf = fm.NonHRAdmistrative_PF,
            //ofm_envelope_operational_pf = fm.NonHROperational_PF,
            //ofm_envelope_facility_pf = fm.NonHRFacility_PF,

            ////Base Amounts Column
            //ofm_envelope_hr_total = fm.HRTotal,
            //ofm_envelope_hr_wages_paidtimeoff = fm.HRWagesPaidTimeOff,
            //ofm_envelope_hr_benefits = fm.HRBenefits,
            //ofm_envelope_hr_employerhealthtax = fm.HREmployerHealthTax,
            //ofm_envelope_hr_prodevexpenses = fm.HRProfessionalDevelopmentExpenses,

            //ofm_envelope_programming = fm.NonHRProgramming,
            //ofm_envelope_administrative = fm.NonHRAdmistrative,
            //ofm_envelope_operational = fm.NonHROperational,
            //ofm_envelope_facility = fm.NonHRFacility,

            ////Grand Totals
            //ofm_envelope_grand_total_proj = fm.GrandTotal_Projected,
            //ofm_envelope_grand_total_pf = fm.GrandTotal_PF,
            //ofm_envelope_grand_total = fm.GrandTotal
        };

        #region Non-HR Calculation

        //CalculateNonHRFundingAmountsCommand fundingCalculator = new(_fundingAmountsRepository, _funding);
        //fundingCalculator.Execute();

        #endregion

        _fundingResult = FundingResult.AutoApproved(_funding.ofm_funding_number, fundingAmounts, null);

        return await Task.FromResult(true);
    }

    public async Task<bool> ProcessFundingResult()
    {
        if (_fundingResult is null || !_fundingResult.IsValidFundingResult())
            return await Task.FromResult(false); //Log the message

        await _fundingRepository.SaveFundingAmounts(_fundingResult);

        // Generate the PDF Funding Agreement & Save it to the funding record

        // Send Funding Agreement Notifications to the provider

        // Other tasks

        return await Task.FromResult(false);
    }

    public async Task<bool> CalculateDefaultSpacesAllocation(IEnumerable<SpaceAllocation> spacesAllocation)
    {
        // Do validation as needed here
        //if (_funding.ofm_apply_room_split_condition.Value)
        //    throw new InvalidOperationException("Invalid data - Apply Room Split Condition should not be True before calculating the default allocation.");

        // Update the default spaces allocation

        foreach (var licenceDetail in LicenceDetails)
        {
            var projected = licenceDetail.AllocatedGroupSizes!.GroupBy(gs1 => gs1, gs2 => gs2, (s1, s2) => new { GroupSize = s1, Count = s2.Count() });

            foreach (var space in licenceDetail.NewSpacesAllocationByLicenceType)
            {
                space.ofm_default_allocation = projected.FirstOrDefault(p => p.GroupSize == space.ofm_cclr_ratio.ofm_group_size)?.Count ?? 0;
            }
        }

        // Save the default values in dataverse
        await _fundingRepository.SaveDefaultSpacesAllocation(spacesAllocation);

        return await Task.FromResult(true);
    }
}

public enum ValidationMode
{
    Simple,
    Comprehensive,
    HROnly,
    NonHROnly,
    PrivateOnly,
    NonProfitOnly
}