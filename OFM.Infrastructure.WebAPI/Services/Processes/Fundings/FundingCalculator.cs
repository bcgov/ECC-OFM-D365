using ECC.Core.DataContext;
using HandlebarsDotNet;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IFundingCalculator
{
    Task<bool> CalculateAsync();
    Task<bool> CalculateDefaultSpacesAllocationAsync();
    Task<bool> ProcessFundingResultAsync();
}

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
            var licenceDetails = _funding?.ofm_facility?.ofm_facility_licence?.SelectMany(licence => licence?.ofm_licence_licencedetail);

            // NOTE: If a facility has duplicate care types/licence types with the same address (e.g. seasonal schedules),
            // the AnnualHoursFTERatio (Hrs of childcare ratio/FTE ratio) needs to be applied at the combined care types level to avoid overpayments.
            if (ApplyDuplicateCareTypesCondition)
            {
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
                                                grouped.ApplyRoomSplitCondition = ApplyRoomSplitCondition;
                                                grouped.NewSpacesAllocationAll = _funding.ofm_funding_spaceallocation;

                                                return grouped;
                                            });

                return groupedlicenceDetails;
            }

            foreach (var service in licenceDetails)
            {
                service.RateSchedule = _rateSchedule;
                service.ApplyRoomSplitCondition = ApplyRoomSplitCondition;
                service.NewSpacesAllocationAll = _funding.ofm_funding_spaceallocation;
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

    #region HR Costs & Rates

    private decimal TotalStaffingCost => LicenceDetails.Sum(cs => cs.StaffingCost);
    private decimal TotalProfessionalDevelopmentExpenses => LicenceDetails.Sum(cs => cs.ProfessionalDevelopmentExpenses);
    private decimal TotalBenefitsCostPerYear => LicenceDetails.Sum(cs => cs.BenefitsCostPerYear);
    private decimal TotalHRRenumeration => LicenceDetails.Sum(cs => cs.HRRenumeration);
    private decimal TotalProfessionalDevelopmentHours => LicenceDetails.Sum(cs => cs.ProfessionalDevelopmentHours);

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
                { OwnershipType: ecc_Ownership.Private, Renumeration: > EHT_LOWER_THRESHHOLD, Renumeration: <= EHT_UPPER_THRESHHOLD } => _rateSchedule?.ofm_for_profit_eht_over_500k ?? 0m,
                { OwnershipType: ecc_Ownership.Private, Renumeration: > EHT_UPPER_THRESHHOLD } => _rateSchedule?.ofm_for_profit_eht_over_1_5m ?? 0m,
                { OwnershipType: ecc_Ownership.Notforprofit, Renumeration: > EHT_UPPER_THRESHHOLD } => _rateSchedule?.ofm_not_for_profit_eht_over_1_5m ?? 0m,
                null => throw new ArgumentNullException(nameof(FundingCalculator), "Can't calculate EHT threshold with null value"),
                _ => 0m
            };

            return (ehtRate / 100) * TotalHRRenumeration;
        }
    }

    #endregion

    #region nonHR Costs & Rates

    private decimal AdjustmentRateForNonHREnvelopes => MAX_ANNUAL_OPEN_HOURS!.Value / LicenceDetails.Max(cs => cs.AnnualStandardHours); // Todo: Logic is in review with the Ministry

    private decimal NonHRProgrammingAmount
    {
        get
        {
            if (_nonHRProgrammingAmount == 0)
                _nonHRProgrammingAmount = GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Programming, TotalSpaces);
            return _nonHRProgrammingAmount;
        }
    }

    private decimal NonHRAdministrativeAmount
    {
        get
        {
            if (_nonHRAdministrativeAmount == 0)
                _nonHRAdministrativeAmount = GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Administration, TotalSpaces);
            return _nonHRAdministrativeAmount;
        }
    }

    private decimal NonHROperationalAmount
    {
        get
        {
            if (_nonHROperationalAmount == 0)
                _nonHROperationalAmount = GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Operational, TotalSpaces);
            return _nonHROperationalAmount;
        }
    }

    private decimal NonHRFacilityAmount
    {
        get
        {
            if (_nonHRFacilityAmount == 0)
                _nonHRFacilityAmount = GetNonHRScheduleAmount(OwnershipType, ecc_funding_envelope.Facility, TotalSpaces);
            return _nonHRFacilityAmount;
        }
    }

    private decimal AdjustedNonHRProgrammingAmount => NonHRProgrammingAmount; // Note: No adjustment is required for programming envelope.
    private decimal AdjustedNonHRAdministrativeAmount => NonHRAdministrativeAmount / AdjustmentRateForNonHREnvelopes; // Note: with adjustment rate
    private decimal AdjustedNonHROperationalAmount => OwnershipType == ecc_Ownership.Homebased ?
                                                        Math.Min(NonHROperationalAmount / AdjustmentRateForNonHREnvelopes, AnnualOperatingCurrentCost) :
                                                        NonHROperationalAmount / AdjustmentRateForNonHREnvelopes;

    private decimal AdjustedNonHRFacilityAmount => AnnualFacilityCurrentCost == 0 ? 0 : OwnershipType == ecc_Ownership.Private &&
                                                     (FacilityType == ofm_facility_type.OwnedWithMortgage || FacilityType == ofm_facility_type.OwnedWithoutMortgage) ? 0 :
                                                     Math.Min(NonHRFacilityAmount, AnnualFacilityCurrentCost);

    private decimal TotalNonHRCost => (AdjustedNonHRProgrammingAmount + AdjustedNonHRAdministrativeAmount + AdjustedNonHROperationalAmount + AdjustedNonHRFacilityAmount);

    #endregion

    #region Totals

    private int TotalSpaces => LicenceDetails.Sum(detail => detail.ofm_operational_spaces!.Value);
    private decimal TotalParentFees => LicenceDetails.Sum(cs => cs.ParentFees);
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
                HRTotal_Projected = LicenceDetails.Sum(cs => cs.HRRenumeration) + EmployerHealthTax,
                HRWagesPaidTimeOff_Projected = TotalStaffingCost,
                HRBenefits_Projected = TotalBenefitsCostPerYear,
                HREmployerHealthTax_Projected = EmployerHealthTax,
                HRProfessionalDevelopmentHours_Projected = TotalProfessionalDevelopmentHours,
                HRProfessionalDevelopmentExpenses_Projected = TotalProfessionalDevelopmentExpenses,

                NonHRProgramming_Projected = AdjustedNonHRProgrammingAmount,
                NonHRAdmistrative_Projected = AdjustedNonHRAdministrativeAmount,
                NonHROperational_Projected = AdjustedNonHROperationalAmount,
                NonHRFacility_Projected = AdjustedNonHRFacilityAmount,

                //Parent Fees
                HRWagesPaidTimeOff_PF = TotalParentFees * (TotalStaffingCost / TotalProjectedFundingCost),
                HRBenefits_PF = TotalParentFees * (TotalBenefitsCostPerYear / TotalProjectedFundingCost),
                HREmployerHealthTax_PF = TotalParentFees * (EmployerHealthTax / TotalProjectedFundingCost),
                HRProfessionalDevelopmentExpenses_PF = TotalParentFees * (TotalProfessionalDevelopmentExpenses / TotalProjectedFundingCost),
                HRProfessionalDevelopmentHours_PF = TotalParentFees * (TotalProfessionalDevelopmentHours / TotalProjectedFundingCost),

                NonHRProgramming_PF = TotalParentFees * (AdjustedNonHRProgrammingAmount / TotalProjectedFundingCost),
                NonHRAdmistrative_PF = TotalParentFees * (AdjustedNonHRAdministrativeAmount / TotalProjectedFundingCost),
                NonHROperational_PF = TotalParentFees * (AdjustedNonHROperationalAmount / TotalProjectedFundingCost),
                NonHRFacility_PF = TotalParentFees * (AdjustedNonHRFacilityAmount / TotalProjectedFundingCost),

                //Base Amounts Column: auto calculated fields (Base = Projected Amount - Parent Fees)

                //Grand Totals
                GrandTotal_Projected = TotalProjectedFundingCost,
                GrandTotal_PF = TotalParentFees,

                CalculatedOn = DateTime.UtcNow
            };

            _fundingResult = FundingResult.Success(_funding.ofm_funding_number, fundingAmounts, LicenceDetails);
        }
        catch (ValidationException exp)
        {
            _fundingResult = FundingResult.InvalidData(_funding?.ofm_funding_number ?? string.Empty, new[] { exp.Message, exp.StackTrace ?? string.Empty });
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
                space.ofm_default_allocation = groupedByGSize.FirstOrDefault(grp => grp.GroupSize == space.ofm_cclr_ratio.ofm_group_size)?.Count ?? 0;
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
        //ToDo
    }

    #endregion

    #region Supporting Methods

    private IEnumerable<decimal> ComputeRate(IEnumerable<FundingRate> fundingRates, int spaces)
    {
        foreach (var step in fundingRates)
        {
            if (spaces >= step.ofm_spaces_max!.Value)
            {
                LogStepAction(step, (step.ofm_spaces_max!.Value - step.ofm_spaces_min!.Value + 1), spaces);
                yield return (step.ofm_spaces_max!.Value - step.ofm_spaces_min!.Value + 1) * step.ofm_rate!.Value;
            }
            else
            {
                LogStepAction(step, (spaces - step.ofm_spaces_min!.Value + 1), spaces);
                yield return (spaces - step.ofm_spaces_min!.Value + 1) * step.ofm_rate!.Value;
            }
        }
    }

    private decimal GetNonHRScheduleAmount(ecc_Ownership? ownershipType, ecc_funding_envelope envelope, int spaces)
    {
        FundingRate[] fundingRates =
        [
            .. _rateSchedule?.ofm_rateschedule_fundingrate?
                        .Where(rate => rate.ofm_ownership == ownershipType
                                && rate.ofm_nonhr_funding_envelope == envelope
                                && rate.ofm_spaces_min <= spaces)
                        .OrderBy(rate => rate.ofm_step)
        ];

        return ComputeRate(fundingRates, spaces).Sum();
    }

    private void LogStepAction(FundingRate fundingRate, int allocatedSpaces, int totalSpace)
    {
        NonHRStepActions.Add(new NonHRStepAction(fundingRate.ofm_step.Value,
                                            allocatedSpaces, Math.Round(fundingRate.ofm_rate.Value, 2, MidpointRounding.AwayFromZero),
                                            Math.Round((allocatedSpaces * fundingRate.ofm_rate).Value, 2, MidpointRounding.AwayFromZero),
                                            fundingRate.ofm_nonhr_funding_envelope.Value.ToString(),
                                            fundingRate.ofm_spaces_min.Value,
                                            fundingRate.ofm_spaces_max.Value,
                                            _funding.ofm_application.ofm_summary_ownership.ToString()
                            ));
    }

    private List<NonHRStepAction> NonHRStepActions => _noneHRStepActions;

    #endregion
}