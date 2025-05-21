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
    Task<bool> ProcessFundingResultAsync();
}

/// <summary>
/// Funding Calculator Version 9
/// </summary>
public class FundingCalculator : IFundingCalculator
{
    public readonly ILogger _logger;
    public readonly IFundingRepository _fundingRepository;
    private readonly IEnumerable<RateSchedule> _rateSchedules;
    private readonly Funding _funding;
    private const decimal EHT_UPPER_THRESHOLD = 1_500_000m; //Todo: Load from rate schedule
    private const decimal EHT_LOWER_THRESHOLD = 500_000m; //Todo: Load from rate schedule
    private RateSchedule? _rateSchedule;
    private FundingResult? _fundingResult;
    private List<NonHRStepAction> _noneHRStepActions = [];

    private decimal _nonHRProgrammingAmount = 0m;
    private decimal _nonHRAdministrativeAmount = 0m;
    private decimal _nonHROperationalAmount = 0m;
    private decimal _nonHRFacilityAmount = 0m;

    public FundingCalculator(IFundingRepository fundingRepository, Funding funding, IEnumerable<RateSchedule> rateSchedules, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(rateSchedules);

        _funding = funding;
        _rateSchedules = rateSchedules;
        _fundingRepository = fundingRepository;
        _logger = logger;
        _rateSchedule = _rateSchedules.First(sch => sch.Id == _funding?.ofm_rate_schedule?.ofm_rate_scheduleid) ?? throw new InvalidDataException("No Rate Schedule matched.");
    }

    private DateTime ApplicationSubmittedOn => _funding.ofm_application!.ofm_summary_submittedon ?? _funding.ofm_application!.createdon ?? new DateTime();

    public virtual IEnumerable<LicenceDetail> LicenceDetails
    {
        get
        {
            IEnumerable<Licence>? activeLicences = _funding?.ofm_facility?.ofm_facility_licence?.Where(licence => licence.statuscode == ofm_licence_StatusCode.Active &&
                                                                                                         licence.ofm_start_date <= ApplicationSubmittedOn.ToLocalPST().Date &&
                                                                                                         (licence.ofm_end_date is null || licence.ofm_end_date >= ApplicationSubmittedOn.ToLocalPST().Date));

            IEnumerable<LicenceDetail>? licenceDetails = activeLicences?
                                .SelectMany(licence => licence?.ofm_licence_licencedetail!)
                                .Where(licenceDetail => licenceDetail.statuscode == ofm_licence_detail_StatusCode.Active);

            var partTimeSA1Flag = licenceDetails.Where(licence => (licence.LicenceType == ecc_licence_type.GroupChildCareSchoolAgeGroup1 && licence.ofm_care_type == ecc_care_types.PartTime)).Count() >= 1;
            var partTimeSA2Flag = licenceDetails.Where(licence => (licence.LicenceType == ecc_licence_type.GroupChildCareSchoolAgeGroup2 && licence.ofm_care_type == ecc_care_types.PartTime)).Count() >= 1;
            var partTimeSA3Flag = licenceDetails.Where(licence => (licence.LicenceType == ecc_licence_type.GroupChildCareSchoolAgeGroup3 && licence.ofm_care_type == ecc_care_types.PartTime)).Count() >= 1;

            var multiplePartTimeSAFlag = (partTimeSA1Flag && partTimeSA2Flag) || (partTimeSA2Flag && partTimeSA3Flag) || (partTimeSA1Flag && partTimeSA3Flag);

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
                                                grouped.MultiplePartTimeSchoolAge = multiplePartTimeSAFlag;

                                                return grouped;
                                            });
                return groupedlicenceDetails;
            }

            foreach (var ld in licenceDetails)
            {
                ld.RateSchedule = _rateSchedule;
                ld.ApplyRoomSplitCondition = ApplyRoomSplitCondition;
                ld.NewSpacesAllocationAll = _funding?.ofm_funding_spaceallocation;
                ld.MultiplePartTimeSchoolAge = multiplePartTimeSAFlag;
            }

            return licenceDetails;
        }
    }

    private bool ApplyRoomSplitCondition => _funding.ofm_apply_room_split_condition ?? false;
    private bool ApplyDuplicateCareTypesCondition => _funding.ofm_apply_duplicate_caretypes_condition ?? false;
    private ecc_Ownership? OwnershipType => _funding.ofm_application!.ofm_summary_ownership;
    private decimal AnnualFacilityCurrentCost => (FacilityType == ofm_facility_type.ProvidedFreeofCharge) ? 0m : _funding.ofm_application!.ofm_costs_year_facility_costs ?? 0m;
    private decimal AnnualOperatingCurrentCost => _funding.ofm_application!.ofm_costs_yearly_operating_costs ?? 0m;
    private ofm_facility_type? FacilityType => _funding?.ofm_application?.ofm_costs_facility_type;

    #region HR Costs & Rates

    private decimal TotalStaffingCost => LicenceDetails.Sum(ld => ld.StaffingCost);
    private decimal TotalProjectedBenefitsCostPerYear => LicenceDetails.Sum(ld => ld.ProjectedBenefitsCostPerYear);
    private decimal TotalHRRenumeration => LicenceDetails.Sum(ld => ld.HRRenumeration);
    private decimal TotalEHTRenumeration => LicenceDetails.Sum(ld => ld.StaffingCost + ld.ProjectedBenefitsCostPerYear + ld.ProfessionalDevelopmentHours);
    private decimal AdjustedFTE => Math.Round(LicenceDetails.Sum(ld => ld.AdjustedFTEs), 2, MidpointRounding.AwayFromZero);
    private decimal TotalProfessionalDevelopmentHours => LicenceDetails.Sum(ld => ld.ProfessionalDevelopmentHours);
    private decimal TotalProfessionalDevelopmentExpenses => LicenceDetails.Sum(ld => ld.ProfessionalDevelopmentExpenses);

    /// <summary>
    ///  HR Envelopes: Step 09 - Add EHT (Employer Health Tax) *** EHT Tax is applied at the calculator level to HR Total Renumeration Only ***
    /// </summary>
    private decimal EmployerHealthTax
    {
        get
        {
            var taxData = new { OwnershipType, TotalEHTRenumeration };
            var EHTProfitLowerThreshold = _rateSchedule?.ofm_eht_minimum_cost_for_profit ?? EHT_LOWER_THRESHOLD;
            var EHTProfitUpperThreshold = _rateSchedule?.ofm_eht_maximum_cost_for_profit ?? EHT_UPPER_THRESHOLD;
            var EHTNonProfitUpperThreshold = _rateSchedule?.ofm_eht_maximum_cost_not_for_profit ?? EHT_UPPER_THRESHOLD;
            var EHTFunding = 0m;

            if (taxData == null)
            {
                throw new ArgumentNullException(nameof(FundingCalculator), "Can't calculate EHT threshold with a null value");
            }

            if (taxData.OwnershipType == ecc_Ownership.Private && (taxData.TotalEHTRenumeration <= EHTProfitUpperThreshold) && (taxData.TotalEHTRenumeration > EHTProfitLowerThreshold))
            {
                EHTFunding = ((_rateSchedule?.ofm_for_profit_eht_over_500k ?? 0m) / 100) * (TotalEHTRenumeration - EHTProfitLowerThreshold);
            }
            else if (taxData.OwnershipType == ecc_Ownership.Private && (taxData.TotalEHTRenumeration > EHTProfitUpperThreshold))
            {
                EHTFunding = ((_rateSchedule?.ofm_for_profit_eht_over_1_5m ?? 0m) / 100) * TotalEHTRenumeration;
            }
            else if (taxData.OwnershipType == ecc_Ownership.Notforprofit && (taxData.TotalEHTRenumeration > EHTNonProfitUpperThreshold))
            {
                EHTFunding = ((_rateSchedule?.ofm_not_for_profit_eht_over_1_5m ?? 0m) / 100) * (TotalEHTRenumeration - EHTNonProfitUpperThreshold);
            }

            return EHTFunding;
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
    private decimal TotalProjectedFundingCost => TotalHRRenumeration + EmployerHealthTax + TotalNonHRCost;

    #endregion

    #region Main Calculator Methods

    public async Task<bool> CalculateAsync()
    {
        var fundingRules = new MustHaveFundingNumberBaseRule();
        fundingRules.NextValidator(new MustHaveValidRateScheduleRule())
                    .NextValidator(new MustHaveValidApplicationStatusRule())
                    .NextValidator(new MustHaveValidOwnershipTypeRule())
                    .NextValidator(new MustHaveValidLicenceRule())
                    .NextValidator(new MustHaveAtLeastOneValidLicenceDetailRule())
                    .NextValidator(new MustHaveAtLeastOneOperationalSpaceRule())
                    .NextValidator(new MustHaveWeeksInOperationRule())
                    .NextValidator(new MustHaveHoursOfOperationRule())
                    .NextValidator(new MustHaveDaysOfWeekRule());
        try
        {
            fundingRules.Validate(_funding);

            _rateSchedule = _rateSchedules.First(sch => sch.Id == _funding!.ofm_rate_schedule!.ofm_rate_scheduleid);

            FundingAmounts fundingAmounts = new()
            {
                //Projected Amounts
                Projected_HRTotal = TotalHRRenumeration + EmployerHealthTax,
                Projected_HRWagesPaidTimeOff = TotalStaffingCost,
                Projected_HRBenefits = TotalProjectedBenefitsCostPerYear,
                Projected_HREmployerHealthTax = EmployerHealthTax,
                Projected_HRProfessionalDevelopmentHours = TotalProfessionalDevelopmentHours,
                Projected_HRProfessionalDevelopmentExpenses = TotalProfessionalDevelopmentExpenses,

                Projected_NonHRProgramming = AdjustedNonHRProgrammingAmount,
                Projected_NonHRAdmistrative = AdjustedNonHRAdministrativeAmount,
                Projected_NonHROperational = AdjustedNonHROperationalAmount,
                Projected_NonHRFacility = AdjustedNonHRFacilityAmount,

                //Parent Fees
                PF_HRWagesPaidTimeOff = TotalParentFees * (TotalStaffingCost / TotalProjectedFundingCost),
                PF_HRBenefits = TotalParentFees * (TotalProjectedBenefitsCostPerYear / TotalProjectedFundingCost),
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
                Adjusted_FTE = AdjustedFTE,
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

    public async Task LogProgressAsync(ID365WebApiService d365webapiservice, ID365AppUserService appUserService, ILogger logger, string titlePrefix = "")
    {
        ArgumentNullException.ThrowIfNull(_fundingResult);

        IProgressTracker tracker;
        tracker = (_fundingResult.Errors.Any()) ?
            new CalculatorErrorTracker(appUserService, d365webapiservice, logger) :
            new CalculatorProgressTracker(appUserService, d365webapiservice, _fundingResult, logger);

        await tracker.SaveProgressAsync(_funding, _fundingResult, NonHRStepActions, titlePrefix);
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
                LogStepAction(step, (step.ofm_spaces_max!.Value - step.ofm_spaces_min!.Value + 1));
                yield return (step.ofm_spaces_max!.Value - step.ofm_spaces_min!.Value + 1) * step.ofm_rate!.Value;
            }
            else
            {
                LogStepAction(step, (adjustedSpaces - step.ofm_spaces_min!.Value + 1));
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

    private void LogStepAction(FundingRate fundingRate, decimal allocatedSpace)
    {
        NonHRStepActions.Add(new NonHRStepAction(Step: fundingRate.ofm_step.GetValueOrDefault(),
                                            AllocatedSpaces: Math.Round(allocatedSpace, 2, MidpointRounding.AwayFromZero),
                                            Rate: Math.Round(fundingRate.ofm_rate.GetValueOrDefault(), 2, MidpointRounding.AwayFromZero),
                                            Cost: Math.Round((allocatedSpace * fundingRate.ofm_rate.GetValueOrDefault()), 2, MidpointRounding.AwayFromZero),
                                            Envelope: fundingRate.ofm_nonhr_funding_envelope.GetValueOrDefault().ToString(),
                                            MinSpaces: fundingRate.ofm_spaces_min.GetValueOrDefault(),
                                            MaxSpaces: fundingRate.ofm_spaces_max.GetValueOrDefault(),
                                            Ownership: _funding.ofm_application!.ofm_summary_ownership!.Value
                            ));
    }

    private List<NonHRStepAction> NonHRStepActions => _noneHRStepActions;

    #endregion
}