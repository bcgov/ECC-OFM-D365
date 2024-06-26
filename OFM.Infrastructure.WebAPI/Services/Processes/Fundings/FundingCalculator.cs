using ECC.Core.DataContext;
using HandlebarsDotNet;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IFundingCalculator
{
    Task<bool> CalculateAsync();
    Task<bool> CalculateDefaultSpacesAllocationAsync();
    Task<bool> ProcessFundingResultAsync();
}

/// <summary>
/// Funding Calculator Version 9
/// </summary>
public class FundingCalculator : IFundingCalculator
{
    private readonly ILogger _logger;
    private readonly IFundingRepository _fundingRepository;
    private readonly IEnumerable<RateSchedule> _rateSchedules;
    private readonly Funding _funding;
    private decimal? MAX_ANNUAL_OPEN_HOURS => _rateSchedule?.ofm_max_annual_open_hours ?? 2510; // Note: 50.2 weeks a year for 5 days a week with 10 hours per day (2510 = 50.2 * 5 * 10)
    private decimal? MIN_CARE_HOURS_PER_FTE_RATIO => _rateSchedule?.ofm_min_care_hours_per_fte_ratio ?? 0.5m;
    private const decimal EHT_UPPER_THRESHHOLD = 1_500_000m; //Todo: Load from rate schedule
    private const decimal EHT_LOWER_THRESHHOLD = 500_000m; //Todo: Load from rate schedule
    private RateSchedule? _rateSchedule;
    private FundingResult? _fundingResult;
    private List<NonHRStepAction> _noneHRStepActions = [];

    private decimal _nonHRProgrammingAmount = 0m;
    private decimal _nonHRAdministrativeAmount = 0m;
    private decimal _nonHROperationalAmount = 0m;
    private decimal _nonHRFacilityAmount = 0m;

    public FundingCalculator(IFundingRepository fundingRepository, Funding funding, IEnumerable<RateSchedule> rateSchedules, ILogger logger)
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
            IEnumerable<Licence>? activeLicences = _funding?.ofm_facility?.ofm_facility_licence?.Where(licence => licence.statuscode == ofm_licence_StatusCode.Active &&
                                                                                                         licence.ofm_start_date.GetValueOrDefault().ToLocalPST().Date <= DateTime.UtcNow.ToLocalPST().Date &&
                                                                                                         (licence.ofm_end_date is null ||
                                                                                                         licence.ofm_end_date.GetValueOrDefault().ToLocalPST().Date >= DateTime.UtcNow.ToLocalPST().Date));

            IEnumerable<LicenceDetail>? licenceDetails = activeLicences?
                                .SelectMany(licence => licence?.ofm_licence_licencedetail!)
                                .Where(licenceDetail => licenceDetail.statuscode == ofm_licence_detail_StatusCode.Active);

            // NOTE: If a facility has duplicate care types/licence types with the same address (e.g. seasonal schedules),
            // the AnnualHoursFTERatio (Hrs of childcare ratio/FTE ratio) needs to be applied at the combined care types level to avoid overpayments.
            if (ApplyDuplicateCareTypesCondition)
            {
                var groupedlicenceDetails = licenceDetails?
                                            .GroupBy(ltype => ltype.ofm_licence_type, (ltype, licenceGroup) =>
                                            {
                                                var grouped = licenceGroup.First(); //Get the first occurence of the grouped licence details and override with the grouped hours and max spaces
                                                grouped.ofm_operational_spaces = licenceGroup.Max(t => t.Spaces);
                                                grouped.ofm_operation_hours_from = licenceGroup.Min(t => t.ofm_operation_hours_from);
                                                grouped.ofm_operation_hours_to = licenceGroup.Max(t => t.ofm_operation_hours_to);
                                                grouped.ofm_week_days = string.Join(",", licenceGroup.Select(t => t.ofm_week_days));
                                                grouped.ofm_weeks_in_operation = licenceGroup.Sum(t => t.ofm_weeks_in_operation);
                                                grouped.RateSchedule = _rateSchedule;
                                                grouped.ApplyRoomSplitCondition = ApplyRoomSplitCondition;
                                                grouped.NewSpacesAllocationAll = _funding?.ofm_funding_spaceallocation;

                                                return grouped;
                                            });

                return groupedlicenceDetails;
            }

            foreach (var service in licenceDetails)
            {
                service.RateSchedule = _rateSchedule;
                service.ApplyRoomSplitCondition = ApplyRoomSplitCondition;
                service.NewSpacesAllocationAll = _funding?.ofm_funding_spaceallocation;
            }

            return licenceDetails;
        }
    }

    private bool ApplyRoomSplitCondition => _funding.ofm_apply_room_split_condition ?? false;
    private bool ApplyDuplicateCareTypesCondition => _funding.ofm_apply_duplicate_caretypes_condition ?? false;
    private ecc_Ownership? OwnershipType => _funding.ofm_application!.ofm_summary_ownership;
    private decimal AnnualFacilityCurrentCost => _funding.ofm_application!.ofm_costs_year_facility_costs ?? 0m;
    private decimal AnnualOperatingCurrentCost => _funding.ofm_application!.ofm_costs_yearly_operating_costs ?? 0m;
    private ofm_facility_type FacilityType => _funding.ofm_application!.ofm_costs_facility_type!.Value;

    #region HR: Step 03 - Adjust Staffing Required by Hrs of Child Care

    private decimal SumOfAdjustedITE => LicenceDetails.Sum(ld => ld.AdjustedITE);
    private decimal SumOfAdjustedECE => LicenceDetails.Sum(ld => ld.AdjustedECE);
    private decimal SumOfAdjustedECEA => LicenceDetails.Sum(ld => ld.AdjustedECEA);
    private decimal SumOfAdjustedRA => LicenceDetails.Sum(ld => ld.AdjustedRA);

    private decimal TotalAdjustedITE => SumOfAdjustedITE > 0 ? Math.Max(SumOfAdjustedITE, 0.5m) : 0m;
    private decimal TotalAdjustedECE => SumOfAdjustedECE > 0 ? Math.Max(SumOfAdjustedECE, 0.5m) : 0m;
    private decimal TotalAdjustedECEA => SumOfAdjustedECEA > 0 ? Math.Max(SumOfAdjustedECEA, 0.5m) : 0m;
    private decimal TotalAdjustedRA => SumOfAdjustedRA > 0 ? Math.Max(SumOfAdjustedRA, 0.5m) : 0m;
    private decimal TotalAdjustedFTEs => TotalAdjustedITE + TotalAdjustedECE + TotalAdjustedECEA + TotalAdjustedRA;

    #endregion

    #region  HR: Step 04 - Apply Hourly Wages to Staffing

    private Dictionary<WageType, decimal> HourlyWageGrid
    {
        get
        {
            return new Dictionary<WageType, decimal>()
            {
                [WageType.ITE] = _rateSchedule!.ofm_wages_ite_cost!.Value,
                [WageType.ECE] = _rateSchedule.ofm_wages_ece_cost!.Value,
                [WageType.ECEA] = _rateSchedule.ofm_wages_ecea_cost!.Value,
                [WageType.RA] = _rateSchedule.ofm_wages_ra_cost!.Value
            };
        }
    }
    private decimal AdjustedITECostPerHour => TotalAdjustedITE * HourlyWageGrid[WageType.ITE];
    private decimal AdjustedECECostPerHour => TotalAdjustedECE * HourlyWageGrid[WageType.ECE];
    private decimal AdjustedECEACostPerHour => TotalAdjustedECEA * HourlyWageGrid[WageType.ECEA];
    private decimal AdjustedRACostPerHour => TotalAdjustedRA * HourlyWageGrid[WageType.RA];
    private decimal TotalAdjustedFTEsCostPerHour => AdjustedITECostPerHour + AdjustedECECostPerHour + AdjustedECEACostPerHour + AdjustedRACostPerHour;

    #endregion

    #region  HR: Step 05 - Account for Supervisor Role

    private decimal RequiredSupervisors => _rateSchedule!.ofm_supervisor_ratio!.Value * (LicenceDetails.Sum(ld => ld.AllocatedGroupSizes.Count()));
    private decimal SupervisorRateDifference
    {
        get
        {
            return _rateSchedule!.ofm_supervisor_rate!.Value;
        }
    }
    public decimal SupervisorCostDiffPerYear => LicenceDetails.Sum(ld => ld.SupervisorCostDiffPerYear);
    private decimal WageGridMarkup => 1 + _rateSchedule!.ofm_wage_grid_markup!.Value; // Plus 1 so that it does not zero out the related calculation
    private decimal TotalCostPerYear => (TotalAdjustedFTEsCostPerHour * LicenceDetails.First().ExpectedAnnualFTEHours * WageGridMarkup) + SupervisorCostDiffPerYear;
    public decimal StaffingCost => LicenceDetails.Sum(ld => ld.StaffingCost); // Including Supervisor Differentials
    private decimal TotalCostPerFTEPerYear => StaffingCost / TotalAdjustedFTEs;

    #endregion

    #region  HR: Step 10 - Add Professional Development Expenses and Professional Dues

    private decimal TotalProfessionalDevelopment_Wages => (_rateSchedule!.ofm_licensed_childcare_hours_per_fte ?? 0m + _rateSchedule.ofm_elf_hours_per_fte ?? 0m
                                                        + _rateSchedule.ofm_inclusion_hours_per_fte ?? 0m + _rateSchedule.ofm_cultural_hours_per_fte ?? 0m)
                                                        * WageGridMarkup * TotalAdjustedFTEsCostPerHour;

    private decimal TotalProfessionalDevelopment_Benefits => TotalProfessionalDevelopment_Wages * (_rateSchedule!.ofm_average_benefit_load ?? 0m / 100);

    //private decimal TotalProfessionalDevelopmentHours => TotalProfessionalDevelopment_Wages + TotalProfessionalDevelopment_Benefits;

    //private decimal TotalProfessionalDevelopmentExpenses => (_rateSchedule!.ofm_licenced_childcare_cap_per_fte_per_year ?? 0m +
    //                                                           _rateSchedule.ofm_elf_educational_programming_cap_fte_year ?? 0m +
    //                                                           _rateSchedule.ofm_pde_inclusion_training ?? 0m +
    //                                                           _rateSchedule.ofm_pde_cultural_training ?? 0m) *
    //                                                           TotalAdjustedFTEs;

    public decimal ProfessionalDues => _rateSchedule!.ofm_standard_dues_per_fte ?? 0m * TotalAdjustedFTEs;

    #endregion

    #region HR Costs & Rates

    private decimal TotalStaffingCost => LicenceDetails.Sum(ld => ld.StaffingCost);
    private decimal TotalBenefitsCostPerYear => LicenceDetails.Sum(ld => ld.ProjectedBenefitsCostPerYear);
    private decimal TotalHRRenumeration => LicenceDetails.Sum(ld => ld.HRRenumeration);

    private decimal TotalProfessionalDevelopmentHours => LicenceDetails.Sum(ld => ld.ProfessionalDevelopmentHours);
    private decimal TotalProfessionalDevelopmentExpenses => LicenceDetails.Sum(ld => ld.ProfessionalDevelopmentExpenses);

    /// <summary>
    ///  HR Envelopes: Step 09 - Add EHT (Employer Health Tax) *** EHT Tax is applied at the calculator level to HR Total Renumeration Only ***
    /// </summary>
    private decimal EmployerHealthTax
    {
        get
        {
            var taxData = new { OwnershipType, TotalHRRenumeration };
            var ehtRate = taxData switch
            {
                { OwnershipType: ecc_Ownership.Private, TotalHRRenumeration: > EHT_LOWER_THRESHHOLD, TotalHRRenumeration: <= EHT_UPPER_THRESHHOLD } => _rateSchedule?.ofm_for_profit_eht_over_500k ?? 0m,
                { OwnershipType: ecc_Ownership.Private, TotalHRRenumeration: > EHT_UPPER_THRESHHOLD } => _rateSchedule?.ofm_for_profit_eht_over_1_5m ?? 0m,
                { OwnershipType: ecc_Ownership.Notforprofit, TotalHRRenumeration: > EHT_UPPER_THRESHHOLD } => _rateSchedule?.ofm_not_for_profit_eht_over_1_5m ?? 0m,
                null => throw new ArgumentNullException(nameof(FundingCalculator), "Can't calculate EHT threshold with a null value"),
                _ => 0m
            };

            return (ehtRate / 100) * TotalHRRenumeration;
        }
    }

    #endregion

    #region nonHR Costs & Rates

    private const decimal AdjustmentRateForNonHREnvelopes = 1;
    private decimal NonHRProgrammingAmount
    {
        get
        {
            if (_nonHRProgrammingAmount == 0)
                _nonHRProgrammingAmount = GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Programming, TotalAdjustedNonHRSpaces);
            return _nonHRProgrammingAmount;
        }
    }

    private decimal NonHRAdministrativeAmount
    {
        get
        {
            if (_nonHRAdministrativeAmount == 0)
                _nonHRAdministrativeAmount = GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Administration, TotalAdjustedNonHRSpaces);
            return _nonHRAdministrativeAmount;
        }
    }

    private decimal NonHROperationalAmount
    {
        get
        {
            if (_nonHROperationalAmount == 0)
                _nonHROperationalAmount = GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Operational, TotalAdjustedNonHRSpaces);
            return _nonHROperationalAmount;
        }
    }

    private decimal NonHRFacilityAmount
    {
        get
        {
            if (_nonHRFacilityAmount == 0)
                _nonHRFacilityAmount = GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Facility, TotalAdjustedNonHRSpaces);
            return _nonHRFacilityAmount;
        }
    }

    private decimal AdjustedNonHRProgrammingAmount => NonHRProgrammingAmount; // Note: No adjustment is required for programming envelope in Caculator V9
    private decimal AdjustedNonHRAdministrativeAmount => NonHRAdministrativeAmount / AdjustmentRateForNonHREnvelopes;
    private decimal AdjustedNonHROperationalAmount => OwnershipType == ecc_Ownership.Homebased ?
                                                        Math.Min(NonHROperationalAmount / AdjustmentRateForNonHREnvelopes, AnnualOperatingCurrentCost) :
                                                        NonHROperationalAmount / AdjustmentRateForNonHREnvelopes;

    private decimal AdjustedNonHRFacilityAmount => AnnualFacilityCurrentCost == 0 ? 0 : OwnershipType == ecc_Ownership.Private &&
                                                     (FacilityType == ofm_facility_type.OwnedWithMortgage || FacilityType == ofm_facility_type.OwnedWithoutMortgage) ? 0 :
                                                     Math.Min(NonHRFacilityAmount, AnnualFacilityCurrentCost);

    private decimal TotalNonHRCost => (AdjustedNonHRProgrammingAmount + AdjustedNonHRAdministrativeAmount + AdjustedNonHROperationalAmount + AdjustedNonHRFacilityAmount);

    #endregion

    #region Totals

    //private int TotalSpaces => LicenceDetails.Sum(ld => ld.ofm_operational_spaces!.Value);
    private decimal TotalAdjustedNonHRSpaces => LicenceDetails.Sum(ld => ld.AdjustedNonHRSpaces);
    private decimal TotalParentFees => LicenceDetails.Sum(ld => ld.ParentFees);
    private decimal TotalProjectedFundingCost => TotalHRRenumeration + TotalNonHRCost;

    #endregion

    #region Main Calculator Methods

    public async Task<bool> CalculateAsync()
    {
        var fundingRules = new MustHaveFundingNumberBaseRule();
        fundingRules.NextValidator(new MustHaveValidRateScheduleRule())
                    .NextValidator(new MustHaveValidApplicationStatusRule())
                    .NextValidator(new MustHaveValidOwnershipTypeRule())
                    .NextValidator(new MustHaveValidLicenceRule())
                    .NextValidator(new MustHaveAtLeastOneValidLicenceDetailRule());
        try
        {
            fundingRules.Validate(_funding);

            _rateSchedule = _rateSchedules.First(sch => sch.Id == _funding!.ofm_rate_schedule!.ofm_rate_scheduleid);

            FundingAmounts fundingAmounts = new()
            {
                //Projected Amounts
                Projected_HRTotal = TotalHRRenumeration + EmployerHealthTax,
                Projected_HRWagesPaidTimeOff = TotalStaffingCost,
                Projected_HRBenefits = TotalBenefitsCostPerYear,
                Projected_HREmployerHealthTax = EmployerHealthTax,
                Projected_HRProfessionalDevelopmentHours = TotalProfessionalDevelopmentHours,
                Projected_HRProfessionalDevelopmentExpenses = TotalProfessionalDevelopmentExpenses,

                Projected_NonHRProgramming = AdjustedNonHRProgrammingAmount,
                Projected_NonHRAdmistrative = AdjustedNonHRAdministrativeAmount,
                Projected_NonHROperational = AdjustedNonHROperationalAmount,
                Projected_NonHRFacility = AdjustedNonHRFacilityAmount,

                //Parent Fees
                PF_HRWagesPaidTimeOff = TotalParentFees * (TotalStaffingCost / TotalProjectedFundingCost),
                PF_HRBenefits = TotalParentFees * (TotalBenefitsCostPerYear / TotalProjectedFundingCost),
                PF_HREmployerHealthTax = TotalParentFees * (EmployerHealthTax / TotalProjectedFundingCost),
                PF_HRProfessionalDevelopmentExpenses = TotalParentFees * (TotalProfessionalDevelopmentExpenses / TotalProjectedFundingCost),
                PF_HRProfessionalDevelopmentHours = TotalParentFees * (TotalProfessionalDevelopmentHours / TotalProjectedFundingCost),

                PF_NonHRProgramming = TotalParentFees * (AdjustedNonHRProgrammingAmount / TotalProjectedFundingCost),
                PF_NonHRAdmistrative = TotalParentFees * (AdjustedNonHRAdministrativeAmount / TotalProjectedFundingCost),
                PF_NonHROperational = TotalParentFees * (AdjustedNonHROperationalAmount / TotalProjectedFundingCost),
                PF_NonHRFacility = TotalParentFees * (AdjustedNonHRFacilityAmount / TotalProjectedFundingCost),

                //Base Amounts Column: auto calculated fields (Base = Projected Amount - Parent Fees)

                //Grand Totals
                Projected_GrandTotal = TotalProjectedFundingCost,
                PF_GrandTotal = TotalParentFees,

                CalculatedOn = DateTime.UtcNow
            };

            _fundingResult = FundingResult.Success(_funding.ofm_funding_number, fundingAmounts, LicenceDetails);
        }
        catch (ValidationException exp)
        {
            _fundingResult = FundingResult.InvalidData(_funding?.ofm_funding_number ?? string.Empty, [exp.Message, exp.StackTrace ?? string.Empty]);
        }

        return await Task.FromResult(true);
    }

    public async Task<bool> ProcessFundingResultAsync()
    {
        if (_fundingResult is null || !_fundingResult.IsValidFundingResult())
        {
            _logger.LogError(CustomLogEvent.Process, "Failed to calculate funding amounts with the reason(s): {errors}", JsonValue.Create(_fundingResult?.Errors)!.ToString());

            return await Task.FromResult(false);
        }

        _ = await _fundingRepository.SaveFundingAmountsAsync(_fundingResult);

        return await Task.FromResult(true);
    }

    /// <summary>
    /// Only used for split room scenario. It will calculate and set the default allocation values for each applicable space allocation record.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> CalculateDefaultSpacesAllocationAsync()
    {
        _rateSchedule = _rateSchedules.First(sch => sch.Id == _funding!.ofm_rate_schedule!.ofm_rate_scheduleid);

        foreach (LicenceDetail licenceDetail in LicenceDetails)
        {
            //Note: For each licence detail, calculate the default group sizes based on the cclr ratios table and grouped by the group size
            var groupedByGSize = licenceDetail.DefaultGroupSizes!.GroupBy(grp1 => grp1, grp2 => grp2, (g1, g2) => new { GroupSize = g1, Count = g2.Count() });

            foreach (var space in licenceDetail.NewSpacesAllocationByLicenceType)
            {
                space.ofm_default_allocation = groupedByGSize.FirstOrDefault(grp => grp.GroupSize == space.ofm_cclr_ratio?.ofm_group_size)?.Count ?? 0;
            }
        }

        var newSpaces = LicenceDetails.SelectMany(s => s.NewSpacesAllocationByLicenceType)
                        .Where(s => s.ofm_default_allocation.Value > 0);

        if (!newSpaces.Any())
        {
            _logger.LogWarning("No new spaces allocation records to update.");
            return await Task.FromResult(false);
        }

        var result = await _fundingRepository.SaveDefaultSpacesAllocationAsync(newSpaces);

        return await Task.FromResult(result);
    }

    public async Task LogProgressAsync(ID365WebApiService d365webapiservice, ID365AppUserService appUserService, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(_fundingResult);

        IProgressTracker tracker;
        tracker = (_fundingResult.Errors.Any()) ?
            new CalculatorErrorTracker(appUserService, d365webapiservice, logger) :
            new CalculatorProgressTracker(appUserService, d365webapiservice, _fundingResult, logger);

        await tracker.SaveProgressAsync(_funding, _fundingResult, NonHRStepActions);
    }

    #endregion

    #region Supporting Methods

    /// <summary>
    /// This is a dynamic and simplified version, but also works with the excel calculator V9
    /// </summary>
    /// <param name="fundingRates"></param>
    /// <param name="adjustedSpaces"></param>
    /// <returns></returns>
    private IEnumerable<decimal> ComputeRate(IEnumerable<FundingRate> fundingRates, decimal adjustedSpaces)
    {
        foreach (var step in fundingRates)
        {
            if (adjustedSpaces >= step.ofm_spaces_max!.Value)
            {
                LogStepAction(step, (step.ofm_spaces_max!.Value - step.ofm_spaces_min!.Value + 1), adjustedSpaces);
                yield return (step.ofm_spaces_max!.Value - step.ofm_spaces_min!.Value + 1) * step.ofm_rate!.Value;
            }
            else
            {
                LogStepAction(step, (adjustedSpaces - step.ofm_spaces_min!.Value + 1), adjustedSpaces);
                yield return (adjustedSpaces - step.ofm_spaces_min!.Value + 1) * step.ofm_rate!.Value;
            }
        }
    }

    /// <summary>
    /// This version is coded with the same exact logic in the excel calculator V9
    /// </summary>
    /// <param name="fundingRates"></param>
    /// <param name="adjustedSpaces"></param>
    /// <returns></returns>
    private IEnumerable<decimal> ComputeRateV9(IEnumerable<FundingRate> fundingRates, decimal adjustedSpaces)
    {
        var previousMax = 0;
        foreach (var step in fundingRates)
        {
            switch (step.ofm_step!.Value)
            {
                case 1:
                    yield return Math.Min(adjustedSpaces, step.ofm_spaces_max!.Value) * step.ofm_rate!.Value;
                    break;
                case 2:
                case 3:
                    yield return Math.Max(0, Math.Min(adjustedSpaces - step.ofm_spaces_min!.Value + 1,
                        step.ofm_spaces_max!.Value - previousMax)) * step.ofm_rate!.Value;
                    break;
                case 4:
                    yield return Math.Max(0, adjustedSpaces - step.ofm_spaces_min!.Value + 1) * step.ofm_rate!.Value;
                    break;
                default:
                    break;
            }
            previousMax = step.ofm_spaces_max!.Value;
        }
    }

    private decimal GetNonHRScheduleAmount(ecc_Ownership? ownershipType, ecc_funding_envelope envelope, decimal adjustedSpacesNonHR)
    {
        FundingRate[] fundingRates =
        [
            .. _rateSchedule?.ofm_rateschedule_fundingrate?
                        .Where(rate => rate.ofm_ownership == ownershipType
                                && rate.ofm_nonhr_funding_envelope == envelope
                                && rate.ofm_spaces_min <= Math.Ceiling(adjustedSpacesNonHR))
                        .OrderBy(rate => rate.ofm_step)
        ];

        return ComputeRate(fundingRates, adjustedSpacesNonHR).Sum();
    }

    private void LogStepAction(FundingRate fundingRate, decimal allocatedSpaces, decimal totalSpace)
    {
        NonHRStepActions.Add(new NonHRStepAction(Step: fundingRate.ofm_step.GetValueOrDefault(),
                                            AllocatedSpaces: Math.Round(allocatedSpaces, 6, MidpointRounding.AwayFromZero),
                                            Rate: Math.Round(fundingRate.ofm_rate.GetValueOrDefault(), 2, MidpointRounding.AwayFromZero),
                                            Cost: Math.Round((allocatedSpaces * fundingRate.ofm_rate.GetValueOrDefault()), 2, MidpointRounding.AwayFromZero),
                                            Envelope: fundingRate.ofm_nonhr_funding_envelope.GetValueOrDefault().ToString(),
                                            MinSpaces: fundingRate.ofm_spaces_min.GetValueOrDefault(),
                                            MaxSpaces: fundingRate.ofm_spaces_max.GetValueOrDefault(),
                                            Ownership: _funding.ofm_application!.ofm_summary_ownership!.Value
                            ));
    }

    private List<NonHRStepAction> NonHRStepActions => _noneHRStepActions;

    #endregion
}