using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IFundingCalculator
{
    Task<bool> Calculate();
    Task<bool> CalculateDefaultSpacesAllocation();
    Task<bool> ProcessFundingResult();
}

public class FundingCalculator : IFundingCalculator
{
    private readonly ILogger _logger;
    private readonly IFundingRepository _fundingRepository;
    private readonly IEnumerable<RateSchedule> _rateSchedules;
    private readonly Funding _funding;
    private readonly decimal MAX_ANNUAL_OPERATIONAL_HOURS = 2510; //Todo: Load from rate schedule - Note: 50.2 weeks a year for 5 days a week with 10 hours per day (2510 = 50.2 * 5 * 10)
    private readonly decimal MIN_HOURS_FTE_RATIO = 0.5m; //Todo: Load from rate schedule
    private const decimal EHT_UPPER_THRESHHOLD = 1_500_000m;
    private const decimal EHT_LOWER_THRESHHOLD = 500_000m;
    private RateSchedule? _rateSchedule;
    private FundingResult? _fundingResult;

    public FundingCalculator(ILogger logger, IFundingRepository fundingRepository, Funding funding, IEnumerable<RateSchedule> rateSchedules)
    {
        if (rateSchedules is null || !rateSchedules.Any())
        {
            throw new ArgumentNullException(nameof(rateSchedules));
        }

        _funding = funding;
        _rateSchedules = rateSchedules;
        _fundingRepository = fundingRepository;
        _logger = logger;
    }

    private IEnumerable<LicenceDetail> LicenceDetails
    {
        get
        {
            var licenceDetails = _funding.ofm_facility!.ofm_facility_licence.SelectMany(licence => licence.ofm_licence_licencedetail);

            // NOTE: If a facility has duplicate care types/licence types with the same address (e.g. seasonal schedules), the AnnualHoursFTERatio (Hrs of childcare ratio/FTE ratio) needs to be applied at the combined care types level to avoid overpayments.
            if (ApplyDuplicateCareTypesCondition)
            {
                // Group the duplicate licence types in this case to resolve the over-payment issue
                //var groupedlicenceDetails = licenceDetails
                //                        .GroupBy(ltype => ltype.ofm_licence_type)
                //                        .Select(ltype => new LicenceDetail
                //                        {
                //                            ofm_licence_type = ltype.Key,
                //                            ofm_operational_spaces = ltype.Max(t => t.Spaces),
                //                            ofm_operation_hours_from = ltype.Min(t => t.ofm_operation_hours_from),
                //                            ofm_operation_hours_to = ltype.Max(t => t.ofm_operation_hours_to),
                //                            ofm_week_days = string.Join(",", ltype.Select(t => t.ofm_week_days)),
                //                            ofm_weeks_in_operation = ltype.Sum(t => t.ofm_weeks_in_operation),
                //                            ofm_care_type = ltype.First().ofm_care_type,
                //                            RateSchedule = _rateSchedule,
                //                            ApplySplitRoomCondition = ApplySplitRoomCondition,
                //                            NewSpacesAllocation = _funding.ofm_funding_spaceallocation
                //                        }).ToList();

                var groupedlicenceDetails = licenceDetails
                                            .GroupBy(ltype => ltype.ofm_licence_type, (ltype, lgroup) =>
                                            {
                                                var grouped = lgroup.First(); //Get the first occurance of the grouped licence details and override with the grouped hours and max spaces
                                                grouped.ofm_operational_spaces = lgroup.Max(t => t.Spaces);
                                                grouped.ofm_operation_hours_from = lgroup.Min(t => t.ofm_operation_hours_from);
                                                grouped.ofm_operation_hours_to = lgroup.Max(t => t.ofm_operation_hours_to);
                                                grouped.ofm_week_days = string.Join(",", lgroup.Select(t => t.ofm_week_days));
                                                grouped.ofm_weeks_in_operation = lgroup.Sum(t => t.ofm_weeks_in_operation);
                                                grouped.RateSchedule = _rateSchedule;
                                                grouped.ApplySplitRoomCondition = ApplySplitRoomCondition;
                                                grouped.NewSpacesAllocation = _funding.ofm_funding_spaceallocation;

                                                return grouped;
                                            });

                return groupedlicenceDetails;
            }

            foreach (var service in licenceDetails)
            {
                service.RateSchedule = _rateSchedule;
                service.ApplySplitRoomCondition = ApplySplitRoomCondition;
                service.NewSpacesAllocation = _funding.ofm_funding_spaceallocation;
            }

            return licenceDetails;
        }
    }

    private bool ApplySplitRoomCondition => _funding.ofm_apply_room_split_condition!.Value;
    private bool ApplyDuplicateCareTypesCondition => _funding.ofm_apply_duplicate_caretypes_condition!.Value;
    private ecc_Ownership OwnershipType => _funding.ofm_application!.ofm_summary_ownership!.Value;
    private decimal AnnualFacilityCurrentCost => _funding.ofm_application!.ofm_costs_year_facility_costs!.Value;

    #region HR Costs & Rates

    private decimal TotalStaffingCost => LicenceDetails.Sum(cs => cs.StaffingCost);
    private decimal TotalProfessionalDevelopmentExpenses => LicenceDetails.Sum(cs => cs.ProfessionalDevelopmentExpenses);
    private decimal TotalBenefitsCostPerYear => LicenceDetails.Sum(cs => cs.BenefitsCostPerYear);
    private decimal TotalHRRenumeration => LicenceDetails.Sum(cs => cs.HRRenumeration);

    /// <summary>
    ///  HR Envelopes: Step 09 - Add EHT (Employer Health Tax) *** EHT Tax is applied at the calculator level to HR Total Renumeration Only ***
    /// </summary>
    private decimal EmployerHealthTax
    {
        get
        {
            var taxData = new { OwnershipType, Renumeration = LicenceDetails.Sum(ld => ld.HRRenumeration) };
            var ehtRate = taxData switch
            {
                { OwnershipType: ecc_Ownership.Private, Renumeration: > EHT_LOWER_THRESHHOLD, Renumeration: <= EHT_UPPER_THRESHHOLD } => _rateSchedule.ofm_for_profit_eht_over_500k!.Value,
                { OwnershipType: ecc_Ownership.Private, Renumeration: > EHT_UPPER_THRESHHOLD } => _rateSchedule.ofm_for_profit_eht_over_1_5m!.Value,
                { OwnershipType: ecc_Ownership.Notforprofit, Renumeration: > EHT_UPPER_THRESHHOLD } => _rateSchedule.ofm_not_for_profit_eht_over_1_5m!.Value,
                null => throw new ArgumentNullException(nameof(FundingCalculator), "Can't calculate EHT threshold with null value"),
                _ => 0m
            };

            return ehtRate * TotalHRRenumeration;
        }
    }

    #endregion

    #region nonHR Costs & Rates

    private decimal AdjustmentRateForNonHREnvelopes => MAX_ANNUAL_OPERATIONAL_HOURS / LicenceDetails.Max(cs => cs.AnnualStandardHours); // Todo: Logic is in review with the Ministry

    private decimal NonHRProgrammingAmount => GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Programming, TotalSpaces);
    private decimal NonHRAdministrativeAmount => GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Administration, TotalSpaces);
    private decimal NonHROperationalAmount => GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Operational, TotalSpaces);
    private decimal NonHRFacilityAmount => GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Facility, TotalSpaces);

    private decimal AdjustedNonHRProgrammingAmount => NonHRProgrammingAmount; // Note: No adjustment is required for programming envelope.
    private decimal AdjustedNonHRAdministrativeAmount => NonHRAdministrativeAmount / AdjustmentRateForNonHREnvelopes; // Note: with adjustment rate
    private decimal AdjustedNonHROperationalAmount => NonHROperationalAmount; // Todo:Complete the logic
    private decimal AdjustedNonHRFacilityAmount => Math.Min(NonHRFacilityAmount, AnnualFacilityCurrentCost); // ToDo:Complete the logic

    private decimal TotalNonHRCost => (AdjustedNonHRProgrammingAmount + AdjustedNonHRAdministrativeAmount + AdjustedNonHROperationalAmount + AdjustedNonHRFacilityAmount);

    #endregion

    #region Totals

    private int TotalSpaces => LicenceDetails.Sum(detail => detail.ofm_operational_spaces!.Value);
    private decimal TotalParentFees => LicenceDetails.Sum(cs => cs.ParentFees);
    private decimal TotalProjectedFundingCost => TotalHRRenumeration + TotalNonHRCost;

    #endregion

    #region Main Calculator Methods

    public async Task<bool> Calculate()
    {
        var fundingRules = new MustHaveFundingNumberBaseRule();
        fundingRules.NextValidator(new MustHaveValidApplicationStatusRule())
                    .NextValidator(new MustHaveValidRateScheduleRule());
        try
        {
            fundingRules.Validate(_funding);

            _rateSchedule = _rateSchedules.First(sch => sch.Id == _funding!.ofm_rate_schedule!.ofm_rate_scheduleid);

            FundingAmounts fundingAmounts = new()
            {
                //Projected Amounts
                HRTotal_Projected = LicenceDetails.Sum(cs => cs.HRRenumeration) + EmployerHealthTax,
                HRWagesPaidTimeOff_Projected = LicenceDetails.Sum(cs => cs.StaffingCost),
                HRBenefits_Projected = LicenceDetails.Sum(cs => cs.BenefitsCostPerYear),
                HREmployerHealthTax_Projected = EmployerHealthTax,
                HRProfessionalDevelopmentHours_Projected = LicenceDetails.Sum(cs => cs.ProfessionalDevelopmentHours),
                HRProfessionalDevelopmentExpenses_Projected = LicenceDetails.Sum(cs => cs.ProfessionalDevelopmentExpenses),

                NonHRProgramming_Projected = AdjustedNonHRProgrammingAmount,
                NonHRAdmistrative_Projected = AdjustedNonHRAdministrativeAmount,
                NonHROperational_Projected = AdjustedNonHROperationalAmount,
                NonHRFacility_Projected = AdjustedNonHRFacilityAmount,

                //Parent Fees
                HRWagesPaidTimeOff_PF = TotalParentFees * ((TotalHRRenumeration - TotalBenefitsCostPerYear - EmployerHealthTax - TotalProfessionalDevelopmentExpenses) / TotalProjectedFundingCost),
                HRBenefits_PF = TotalParentFees * (TotalBenefitsCostPerYear / TotalProjectedFundingCost),
                HREmployerHealthTax_PF = TotalParentFees * (EmployerHealthTax / TotalProjectedFundingCost),
                HRProfessionalDevelopmentExpenses_PF = TotalParentFees * (TotalProfessionalDevelopmentExpenses / TotalProjectedFundingCost),

                NonHRProgramming_PF = TotalParentFees * (AdjustedNonHRProgrammingAmount / TotalProjectedFundingCost),
                NonHRAdmistrative_PF = TotalParentFees * (AdjustedNonHRAdministrativeAmount / TotalProjectedFundingCost),
                NonHROperational_PF = TotalParentFees * (AdjustedNonHROperationalAmount / TotalProjectedFundingCost),
                NonHRFacility_PF = TotalParentFees * (AdjustedNonHRFacilityAmount / TotalProjectedFundingCost),

                //Base Amounts Column: auto calculated fields (Base = Projected Amount - Parent Fees)

                //Grand Totals
                GrandTotal_Projected = TotalProjectedFundingCost,
                GrandTotal_PF = TotalParentFees,

                //Calculation Date
                NewCalculationDate = DateTime.UtcNow
            };

            _fundingResult = FundingResult.AutoCalculated(_funding.ofm_funding_number, fundingAmounts, null);

        }
        catch (ValidationException exp)
        {
            _fundingResult = FundingResult.InvalidData(_funding?.ofm_funding_number ?? string.Empty, new[] { exp.Message, exp.StackTrace ?? string.Empty });
        }

        return await Task.FromResult(true);
    }

    public async Task<bool> ProcessFundingResult()
    {
        if (_fundingResult is null || !_fundingResult.IsValidFundingResult())
        {
            _logger.LogWarning(CustomLogEvent.Process, "Failed to calculate funding amounts with the result: {fundingResult}", _fundingResult);

            return await Task.FromResult(false);
        }

        _ = await _fundingRepository.SaveFundingAmounts(_fundingResult);

        // Generate the PDF Funding Agreement & Save it to the funding record

        // Send Funding Agreement Notifications to the provider

        // Other tasks

        return await Task.FromResult(true);
    }

    /// <summary>
    /// Only used for split room scenario. It will calculate and set the default allocation values for each applicable space allocation record.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> CalculateDefaultSpacesAllocation()
    {
        foreach (LicenceDetail licenceDetail in LicenceDetails)
        {
            //Note: For each licence detail, calculate the default group sizes based on the cclr ratios table and grouped by the group size
            var groupedByGSize = licenceDetail.DefaultGroupSizes!.GroupBy(grp1 => grp1, grp2 => grp2, (gs1, gs2) => new { GroupSize = gs1, Count = gs2.Count() });

            foreach (var space in licenceDetail.NewSpacesAllocationByLicenceType)
            {
                space.ofm_default_allocation = groupedByGSize.FirstOrDefault(grp => grp.GroupSize == space.ofm_cclr_ratio.ofm_group_size)?.Count ?? 0;
            }
        }

        var newSpaces = LicenceDetails.SelectMany(s => s.NewSpacesAllocationByLicenceType)
                        .Where(s => s.ofm_default_allocation.Value > 0);

        await _fundingRepository.SaveDefaultSpacesAllocation(newSpaces);

        return await Task.FromResult(true);
    }

    #endregion

    #region Supporting Methods

    private static IEnumerable<decimal> ComputeRate(IEnumerable<FundingRate> fundingRates, int spaces)
    {
        foreach (var step in fundingRates)
        {
            if (spaces >= step.ofm_spaces_max!.Value)
                yield return (step.ofm_spaces_max!.Value - step.ofm_spaces_min!.Value + 1) * step.ofm_rate!.Value;
            else
                yield return (spaces - step.ofm_spaces_min!.Value + 1) * step.ofm_rate!.Value;
        }
    }

    private decimal GetNonHRScheduleAmount(ecc_Ownership ownershipType, ecc_funding_envelope envelope, int spaces)
    {
        FundingRate[] fundingRates =
        [
            .. _rateSchedule.ofm_rateschedule_fundingrate!
                        .Where(rate => rate.ofm_ownership == ownershipType
                                && rate.ofm_nonhr_funding_envelope == envelope
                                && rate.ofm_spaces_min <= spaces)
                        .OrderBy(rate => rate.ofm_step)
        ];

        return ComputeRate(fundingRates, spaces).Sum();
    }

    #endregion
}