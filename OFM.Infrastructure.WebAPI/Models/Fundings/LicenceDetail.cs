﻿using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using System.ComponentModel;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public class LicenceDetail : ofm_licence_detail
{
    #region Split Room & Duplicate Licence Types/Care Types
    /// <summary>
    /// Split Room is a specific scenario where a facility has a smaller room capacity than normal. It requires more staff for the additional rooms.
    /// The provider can claim more FTEs than the efficient space allocation ratio from the funding calculator.
    /// </summary>

    private readonly bool _applyDuplicateCareType;
    private readonly bool _applySplitRoom;
    public bool ApplySplitRoomCondition { get; set; }
    private IEnumerable<SpaceAllocation>? _newSpacesAllocation = [];
    public IEnumerable<SpaceAllocation>? NewSpacesAllocation
    {
        get { return _newSpacesAllocation; }
        set { _newSpacesAllocation = value ?? Array.Empty<SpaceAllocation>(); }
    }
    public IEnumerable<SpaceAllocation>? NewSpacesAllocationByLicenceType
    {
        get
        {
            var filteredByCareType = NewSpacesAllocation.Where(space =>
            {
                var typesMapping = space.ofm_cclr_ratio.ofm_licence_mapping!.Split(",")?.Select(int.Parse);
                bool isMapped = typesMapping!.Contains(LicenceTypeNumber);
                return isMapped;
            });

            return filteredByCareType;
        }
    }

    #endregion

    #region Funding Schedule Data

    private readonly decimal MIN_HOURS_FTE_RATIO = 0.5m; // ToDo: Load from dataverse
    private IEnumerable<CCLRRatio> CCLRRatios => _rateSchedule?.ofm_rateschedule_cclr ?? [];
    private RateSchedule? _rateSchedule;
    public RateSchedule RateSchedule { set { _rateSchedule = value; } }

    #endregion

    #region Licence Type Data

    private Guid LicenceTypeId => base.GetAttributeValue<Guid>(Fields.ofm_licence_detailid);
    public ecc_licence_type LicenceType => (ecc_licence_type)base.GetAttributeValue<OptionSetValue>(Fields.ofm_licence_type).Value;
    private int LicenceTypeNumber => base.GetAttributeValue<OptionSetValue>(Fields.ofm_licence_type).Value;
    public int Spaces => base.GetAttributeValue<int>(Fields.ofm_operational_spaces);

    #endregion

    #region Operating Hours

    private DateTime TimeFrom => base.GetAttributeValue<DateTime>(Fields.ofm_operation_hours_from);
    private DateTime TimeTo => base.GetAttributeValue<DateTime>(Fields.ofm_operation_hours_to);
    private decimal HoursPerDay => (TimeTo - TimeFrom).Hours; // Todo: consider Minutes ? Also to review if the dates are valid to be used for the calculation.
    public new string? ofm_week_days { get; set; } // Override and convert the default type from enum to string
    private int DaysPerWeek => ofm_week_days?.Split(",").Length ?? 0;
    private decimal WeeksPerYear => base.GetAttributeValue<int>(Fields.ofm_weeks_in_operation);
    public decimal AnnualStandardHours => HoursPerDay * DaysPerWeek * WeeksPerYear; // Example: 10 * 5 * 50.2 = 2510

    /// <summary>
    /// Note that an FTE is expected to work 1957.5 hours a year and the number of available hours for childcare is less after accounting for training, sick days, vacation, and stat days
    /// </summary>
    /// 
    public decimal AnnualAvailableHoursPerFTE => ExpectedAnnualFTEHours -
                                                    (ProfessionalDevelopmentHours +
                                                    _rateSchedule!.ofm_vacation_hours_per_fte!.Value +
                                                    _rateSchedule!.ofm_sick_hours_per_fte!.Value +
                                                    _rateSchedule!.ofm_statutory_breaks!.Value); // Typically 1580

    // NOTE: If a facility has duplicate care types/licence types with the same address (seasonal schedules), the AnnualHoursFTERatio (Hrs of childcare ratio/FTE ratio) needs to be applied at the combined care types level to avoid overpayments.
    public decimal AnnualHoursFTERatio => Math.Max((AnnualStandardHours / AnnualAvailableHoursPerFTE), MIN_HOURS_FTE_RATIO);
    public decimal ExpectedAnnualFTEHours => _rateSchedule!.ofm_total_fte_hours_per_year!.Value; // The default is 1957.5

    #endregion

    #region Parent Fees

    private ecc_care_types TimeSchedule => ofm_care_type!.Value; // Full-Time or Part-Time

    private decimal ParentFeesRatePerDay => (TimeSchedule == ecc_care_types.FullTime) ? _rateSchedule!.ofm_parent_fee_per_day_ft!.Value : _rateSchedule!.ofm_parent_fee_per_day_pt!.Value;
    private decimal ParentFeesRatePerMonth => (TimeSchedule == ecc_care_types.FullTime) ? _rateSchedule!.ofm_parent_fee_per_month_ft!.Value : _rateSchedule!.ofm_parent_fee_per_month_pt!.Value;
    private decimal AnnualParentFeesPerSpaceByHours => ParentFeesRatePerDay * DaysPerWeek * WeeksPerYear;
    private decimal AnnualParentFeesPerSpaceByMonths => ParentFeesRatePerMonth * 12; // 12 months in a year
    public decimal ParentFees => Math.Min(AnnualParentFeesPerSpaceByHours, AnnualParentFeesPerSpaceByMonths) * Spaces;

    #endregion

    #region HR: Step 01 - Allocate Spaces to Efficient Group Sizes

    public IEnumerable<ecc_group_size>? AllocatedGroupSizes => FilterCCLRByCareType(LicenceType).Select(cclr => cclr.ofm_group_size!.Value);
    public IEnumerable<ecc_group_size>? DefaultGroupSizes => FilterCCLRByCareTypeDefault(LicenceType).Select(cclr => cclr.ofm_group_size!.Value);
    private IEnumerable<CCLRRatio> FilterCCLRByCareTypeDefault(ecc_licence_type careType)
    {
        IEnumerable<CCLRRatio> filteredByCareType = [];

        filteredByCareType = CCLRRatios.Where(cclr =>
        {
            var typesMapping = cclr.ofm_licence_mapping!.Split(",")?.Select(int.Parse);
            bool isMapped = typesMapping!.Contains((int)careType);
            return isMapped;
        });

        return FilterCCLRBySpaces(filteredByCareType.OrderBy(cclr => cclr.ofm_group_size));
    }
    private IEnumerable<CCLRRatio> FilterCCLRByCareType(ecc_licence_type careType)
    {
        IEnumerable<CCLRRatio> filteredByCareType = [];
        if (ApplySplitRoomCondition)
        {
            // Use the new spaces allocation    
            var adjustedAllocationOnly = NewSpacesAllocation!.Where(allo => allo.ofm_adjusted_allocation!.Value > 0);
            var localCCLRs = adjustedAllocationOnly.Select(space => space.ofm_cclr_ratio!);

            filteredByCareType = localCCLRs.Where(cclr =>
            {
                var typesMapping = cclr.ofm_licence_mapping!.Split(",")?.Select(int.Parse);
                bool isMapped = typesMapping!.Contains((int)careType);
                return isMapped;
            });

            return FilterCCLRByNewAllocation(adjustedAllocationOnly, filteredByCareType);
        }

        filteredByCareType = CCLRRatios.Where(cclr =>
        {
            var typesMapping = cclr.ofm_licence_mapping!.Split(",")?.Select(int.Parse);
            bool isMapped = typesMapping!.Contains((int)careType);
            return isMapped;
        });

        return FilterCCLRBySpaces(filteredByCareType.OrderBy(cclr => cclr.ofm_group_size));
    }
    private IEnumerable<CCLRRatio> FilterCCLRByNewAllocation(IEnumerable<SpaceAllocation> sourceList, IEnumerable<CCLRRatio> filteredList)
    {
        // Adjust the number of group sizes according to the new allocation; ofm_adjusted_allocation
        Stack<CCLRRatio> stack = new(filteredList);

        while (stack.Count > 0)
        {
            var currentItem = stack.Peek();
            var currentSource = sourceList.First(space => space.ofm_cclr_ratio.ofm_caption == currentItem.ofm_caption);
            for (int i = 0; i < currentSource.ofm_adjusted_allocation.Value; i++)
            {
                if (i + 1 == currentSource.ofm_adjusted_allocation.Value)
                    yield return stack.Pop();
                else
                    yield return stack.Peek();
            }
        }
    }
    private IEnumerable<CCLRRatio> FilterCCLRBySpaces(IEnumerable<CCLRRatio> filteredList)
    {
        var spacesCount = Spaces;
        Stack<CCLRRatio> stack = new(filteredList);

        while (stack.Count > 0 && spacesCount > 0)
        {
            var currentItem = stack.Peek();
            if (currentItem.ofm_spaces_max <= spacesCount || spacesCount >= currentItem.ofm_spaces_min)
            {
                spacesCount -= currentItem.ofm_spaces_max!.Value;
                if (spacesCount < currentItem.ofm_spaces_min)
                    yield return stack.Pop();
                else
                    yield return stack.Peek();
            }
            else
                stack.Pop();
        }
    }

    #endregion

    #region HR: Step 02 - Determine Minimum Staffing Required

    public int RawITE => FilterCCLRByCareType(LicenceType).Sum(x => x.ofm_fte_min_ite!.Value);
    public int RawECE => FilterCCLRByCareType(LicenceType).Sum(x => x.ofm_fte_min_ece!.Value);
    public int RawECEA => FilterCCLRByCareType(LicenceType).Sum(x => x.ofm_fte_min_ecea!.Value);
    public int RawRA => FilterCCLRByCareType(LicenceType).Sum(x => x.ofm_fte_min_ra!.Value);
    public int TotalRawFTEs => RawITE + RawECE + RawECEA + RawRA;

    #endregion

    #region HR: Step 03 - Adjust Staffing Required by Hrs of Child Care

    private decimal AdjustedITE => RawITE * AnnualHoursFTERatio;
    private decimal AdjustedECE => RawECE * AnnualHoursFTERatio;
    private decimal AdjustedECEA => RawECEA * AnnualHoursFTERatio;
    private decimal AdjustedRA => RawRA * AnnualHoursFTERatio;
    private decimal TotalAdjustedFTEs => TotalRawFTEs * AnnualHoursFTERatio;

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
    private decimal AdjustedITECostPerHour => AdjustedITE * HourlyWageGrid[WageType.ITE];
    private decimal AdjustedECECostPerHour => AdjustedECE * HourlyWageGrid[WageType.ECE];
    private decimal AdjustedECEACostPerHour => AdjustedECEA * HourlyWageGrid[WageType.ECEA];
    private decimal AdjustedRACostPerHour => AdjustedRA * HourlyWageGrid[WageType.RA];
    private decimal TotalAdjustedFTEsCostPerHour => AdjustedITECostPerHour + AdjustedECECostPerHour + AdjustedECEACostPerHour + AdjustedRACostPerHour;

    #endregion

    #region  HR: Step 05 - Account for Supervisor Role

    private decimal RequiredSupervisors => _rateSchedule!.ofm_supervisor_ratio!.Value * AllocatedGroupSizes.Count();
    private decimal SupervisorRateDifference
    {
        get
        {
            return _rateSchedule.ofm_supervisor_rate.Value;
        }
    }
    private decimal SupervisorCostDiffPerYear => RequiredSupervisors * SupervisorRateDifference * (AnnualStandardHours * Spaces / Spaces);
    private decimal WageGridMarkup => 1 + _rateSchedule!.ofm_wage_grid_markup!.Value; // Plus 1 so that it does not zero out the related calculation
    public decimal StaffingCost => (TotalAdjustedFTEsCostPerHour * ExpectedAnnualFTEHours * WageGridMarkup) + SupervisorCostDiffPerYear; // Including Supervisor Differentials
    private decimal TotalCostPerFTEPerYear => StaffingCost / TotalAdjustedFTEs;

    #endregion

    #region  HR: Step 06 - Apply Benefits

    public decimal BenefitsCostPerYear => StaffingCost * (_rateSchedule!.ofm_average_benefit_load!.Value / 100); // The default is 18% of the Total Wages
    private decimal QualityEnhancementCost => (StaffingCost + BenefitsCostPerYear) * (_rateSchedule!.ofm_quality_enhancement_factor!.Value / 100); // The default is 0% currently
    public decimal HRRenumeration => StaffingCost + BenefitsCostPerYear + QualityEnhancementCost;

    #endregion

    #region  HR: Step 07 - *** (Not MVP) ***  Apply Quality Enhancement Factor – Under Consideration

    #endregion

    #region  HR: Step 08 - *** (Not MVP) *** Adjust for Staff Qualifications – Under Consideration - To be Populated for Pilot

    #endregion

    #region  HR: Step 09 - Add EHT (Employer Health Tax) *** EHT Tax is applied at calculator level ***

    #endregion

    #region  HR: Step 10 - Add Professional Development Expenses and Professional Dues

    public decimal ProfessionalDevelopmentHours => _rateSchedule!.ofm_professional_development_hours!.Value; // ToDo: The Ministry will provide a new logic to be implemented

    public decimal ProfessionalDevelopmentExpenses => (_rateSchedule.ofm_licenced_childcare_cap_per_fte_per_year.Value +
                                                               _rateSchedule.ofm_elf_educational_programming_cap_fte_year.Value +
                                                               _rateSchedule.ofm_pde_inclusion_training.Value +
                                                               _rateSchedule.ofm_pde_cultural_training.Value) *
                                                               TotalAdjustedFTEs;

    public decimal ProfessionalDues => _rateSchedule!.ofm_standard_dues_per_fte!.Value * TotalAdjustedFTEs;

    #endregion

}

public enum WageType
{
    [Description("Infant Toddler Educator")]
    ITE,
    [Description("Early Childhood Educator")]
    ECE,
    [Description("Early Childhood Educator Assitance")]
    ECEA,
    [Description("Responsible Adult")]
    RA
}