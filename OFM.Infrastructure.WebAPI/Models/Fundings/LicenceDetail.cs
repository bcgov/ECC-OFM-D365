using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using System.ComponentModel;

namespace OFM.Infrastructure.WebAPI.Models.Fundings;

public class LicenceDetail : ofm_licence_detail
{
    #region Split Room & Duplicate Licence Types/Care Types
    /// <summary>
    /// 1. Split Room condition is a specific scenario where a facility has a smaller room capacity than normal. It requires more staff for the additional rooms.
    ///    The provider can claim more FTEs than the efficient space allocation ratio from the funding calculator.
    /// 2. Duplicate Care Type condition happens when a facility operates with multiple locations. The capacity and hours may differ at each location hence there are duplicate licence details.
    /// </summary>

    private readonly bool _applyRoomSplit;
    private readonly bool _applyDuplicateCareType;
    public bool ApplyRoomSplitCondition { get; set; }
    private IEnumerable<SpaceAllocation>? _newSpacesAllocationAll = [];
    public IEnumerable<SpaceAllocation>? NewSpacesAllocationAll
    {
        get { return _newSpacesAllocationAll; }
        set { _newSpacesAllocationAll = value ?? Array.Empty<SpaceAllocation>(); }
    }
    public IEnumerable<SpaceAllocation>? NewSpacesAllocationByLicenceType
    {
        get
        {
            var filteredByCareType = NewSpacesAllocationAll?.Where(space =>
            {
                var typesMapping = space.ofm_cclr_ratio?.ofm_licence_mapping!.Split(",")?.Select(int.Parse);
                bool isMapped = typesMapping!.Contains(LicenceTypeNumber);
                return isMapped;
            });

            return filteredByCareType;
        }
    }

    #endregion

    #region Funding Schedule Data

    private decimal MIN_CARE_HOURS_FTE_RATIO => _rateSchedule?.ofm_min_care_hours_per_fte_ratio ?? 0m;
    private IEnumerable<CCLRRatio> CCLRRatios => _rateSchedule?.ofm_rateschedule_cclr ?? [];
    private RateSchedule? _rateSchedule;
    public RateSchedule? RateSchedule { set { _rateSchedule = value; } }

    #endregion

    #region Licence Type Data

    private Guid LicenceTypeId => base.GetAttributeValue<Guid>(Fields.ofm_licence_detailid);
    public ecc_licence_type LicenceType => (ecc_licence_type)base.GetAttributeValue<OptionSetValue>(Fields.ofm_licence_type).Value;
    private int LicenceTypeNumber => base.GetAttributeValue<OptionSetValue>(Fields.ofm_licence_type).Value;
    public int Spaces => base.GetAttributeValue<int>(Fields.ofm_operational_spaces);
    private bool RoomSplit => base.GetAttributeValue<bool>(Fields.ofm_apply_room_split_condition);

    #endregion

    #region Operating Hours

    private DateTime TimeFrom => base.GetAttributeValue<DateTime>(Fields.ofm_operation_hours_from);
    private DateTime TimeTo => base.GetAttributeValue<DateTime>(Fields.ofm_operation_hours_to);
    private decimal HoursPerDay => (decimal)(TimeTo - TimeFrom).TotalHours; // Todo: consider Minutes ? Also to review if the dates are valid to be used for the calculation.
    public new string? ofm_week_days { get; set; } // Override and convert the default type from enum to string
    private int DaysPerWeek => ofm_week_days?.Split(",").Length ?? 0;
    private decimal WeeksPerYear => base.GetAttributeValue<int>(Fields.ofm_weeks_in_operation);
    public decimal AnnualStandardHours => HoursPerDay * DaysPerWeek * WeeksPerYear; // Example: 10 * 5 * 50.2 = 2510

    /// <summary>
    /// Note that an FTE is expected to work 1957.5 hours a year and the number of available hours for childcare is less after accounting for training, sick days, vacation, and stat days
    /// </summary>
    /// 
    public decimal AnnualAvailableHoursPerFTE => ExpectedAnnualFTEHours -
                                                    (_rateSchedule!.ofm_licensed_childcare_hours_per_fte! +
                                                    _rateSchedule!.ofm_elf_hours_per_fte!.Value +
                                                    _rateSchedule!.ofm_inclusion_hours_per_fte!.Value +
                                                    _rateSchedule!.ofm_cultural_hours_per_fte!.Value +
                                                    _rateSchedule!.ofm_vacation_hours_per_fte +
                                                    _rateSchedule!.ofm_sick_hours_per_fte +
                                                    _rateSchedule!.ofm_statutory_breaks ?? 0m); // Typically 1580

    // NOTE: If a facility has duplicate care types/licence types with the same address (seasonal schedules), the AnnualHoursFTERatio (Hrs of childcare ratio/FTE ratio) needs to be applied at the combined care types level to avoid overpayments.
    public decimal AnnualCareHoursFTERatio => Math.Max(AnnualStandardHours / AnnualAvailableHoursPerFTE, MIN_CARE_HOURS_FTE_RATIO);
    public decimal ExpectedAnnualFTEHours => _rateSchedule!.ofm_total_fte_hours_per_year!.Value; // The default is 1957.5

    #endregion

    #region Non-HR Adjusted Spaces

    public decimal AdjustedNonHRSpaces => (Spaces * AnnualStandardHours) / (decimal)_rateSchedule!.ofm_max_annual_open_hours!;

    #endregion

    #region Parent Fees

    private ecc_care_types TimeSchedule => ofm_care_type ?? throw new NullReferenceException($"{nameof(LicenceDetail)}: ofm_care_type is empty. Value must be full-time or part-time."); // Full-Time or Part-Time

    private decimal ParentFeesRatePerDay => (TimeSchedule == ecc_care_types.FullTime) ? _rateSchedule!.ofm_parent_fee_per_day_ft!.Value : _rateSchedule!.ofm_parent_fee_per_day_pt!.Value;
    private decimal ParentFeesRatePerMonth => (TimeSchedule == ecc_care_types.FullTime) ? _rateSchedule!.ofm_parent_fee_per_month_ft!.Value : _rateSchedule!.ofm_parent_fee_per_month_pt!.Value;
    private decimal AnnualParentFeesPerSpaceByHours => ParentFeesRatePerDay * DaysPerWeek * WeeksPerYear;
    private decimal AnnualParentFeesPerSpaceByMonths => ParentFeesRatePerMonth * 12; // 12 months in a year
    public decimal ParentFees => Math.Min(AnnualParentFeesPerSpaceByHours, AnnualParentFeesPerSpaceByMonths) * Spaces;

    #endregion

    #region HR: Step 01 - Allocate Spaces to Efficient Group Sizes

    public IEnumerable<ecc_group_size>? AllocatedGroupSizes => FilterCCLRByCareType(LicenceType).Select(cclr => cclr.ofm_group_size!.Value);
    public IEnumerable<ecc_group_size>? DefaultGroupSizes => FilterCCLRByCareTypeDefault(LicenceType).Select(cclr => cclr.ofm_group_size!.Value);
    public IEnumerable<dynamic>? RawGroupedSizesByType
    {
        get
        {
            var groupSizesByLicenceType = FilterCCLRByCareType(LicenceType);
            var groupedByGroupType = groupSizesByLicenceType!.GroupBy(grp1 => grp1, grp2 => grp2, (g1, g2) => new
            {
                GroupSize = g1,
                GroupCount = g2.Count(),
                GroupType = g1.ofm_group_size,
                RawITE = g2.Max(g => g.ofm_fte_min_ite),
                RawECE = g2.Max(g => g.ofm_fte_min_ece),
                RawECEA = g2.Max(g => g.ofm_fte_min_ecea),
                RawRA = g2.Max(g => g.ofm_fte_min_ra)
            });

            return groupedByGroupType;
        }
    }
    public IEnumerable<dynamic>? AdjustedGroupedSizesByType
    {
        get
        {
            var groupSizesByLicenceType = FilterCCLRByCareType(LicenceType);
            var groupedByGroupType = groupSizesByLicenceType!.GroupBy(grp1 => grp1, grp2 => grp2, (g1, g2) => new
            {
                GroupSize = g1,
                GroupCount = g2.Count(),
                GroupType = g1.ofm_group_size,
                AdjustedITE = g2.Max(g => g.ofm_fte_min_ite) * AnnualCareHoursFTERatio,
                AdjustedECE = g2.Max(g => g.ofm_fte_min_ece) * AnnualCareHoursFTERatio,
                AdjustedECEA = g2.Max(g => g.ofm_fte_min_ecea) * AnnualCareHoursFTERatio,
                AdjustedRA = g2.Max(g => g.ofm_fte_min_ra) * AnnualCareHoursFTERatio
            });

            return groupedByGroupType;
        }
    }
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
        if (RoomSplit)
        {
            // Use the new spaces allocation    
            var adjustedAllocationOnly = NewSpacesAllocationAll!.Where(allo => allo.ofm_adjusted_allocation!.Value > 0);
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

    private decimal AdjustedITE => RawITE > 0 ? Math.Max((RawITE * AnnualCareHoursFTERatio), 0.5m) : 0;
    private decimal AdjustedECE => RawECE > 0 ? Math.Max((RawECE * AnnualCareHoursFTERatio), 0.5m) : 0;
    private decimal AdjustedECEA => RawECEA > 0 ? Math.Max((RawECEA * AnnualCareHoursFTERatio), 0.5m) : 0;
    private decimal AdjustedRA => RawRA > 0 ? Math.Max((RawRA * AnnualCareHoursFTERatio), 0.5m) : 0;
    private decimal TotalAdjustedFTEs => AdjustedITE + AdjustedECE + AdjustedECEA + AdjustedRA;

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

    private decimal RequiredSupervisors => _rateSchedule!.ofm_supervisor_ratio!.Value * AllocatedGroupSizes!.Count();
    private decimal SupervisorRateDifference
    {
        get
        {
            return _rateSchedule!.ofm_supervisor_rate!.Value;
        }
    }
    private decimal SupervisorCostDiffPerYear => RequiredSupervisors * SupervisorRateDifference * (AnnualStandardHours * Spaces / Spaces);
    private decimal WageGridMarkup => 1 + _rateSchedule!.ofm_wage_grid_markup!.Value; // Plus 1 so that it does not zero out the related calculation
    private decimal TotalCostPerYear=>(TotalAdjustedFTEsCostPerHour* ExpectedAnnualFTEHours * WageGridMarkup) + SupervisorCostDiffPerYear;
    public decimal StaffingCost => TotalCostPerYear - TotalPD_Wages; // Including Supervisor Differentials
    private decimal TotalCostPerFTEPerYear => StaffingCost / TotalAdjustedFTEs;

    #endregion

    #region  HR: Step 06 - Apply Benefits

    public decimal BenefitsCostPerYear => (TotalCostPerYear * (_rateSchedule!.ofm_average_benefit_load!.Value / 100)) - TotalPD_Benefits; // The default is 18% of the Total Wages
    private decimal QualityEnhancementCost => (StaffingCost + BenefitsCostPerYear) * (_rateSchedule!.ofm_quality_enhancement_factor!.Value / 100); // The default is 0% currently
    public decimal HRRenumeration => StaffingCost + BenefitsCostPerYear + QualityEnhancementCost + ProfessionalDevelopmentExpenses + ProfessionalDevelopmentHours;

    #endregion

    #region  HR: Step 07 - *** (Not MVP) ***  Apply Quality Enhancement Factor – Under Consideration

    #endregion

    #region  HR: Step 08 - *** (Not MVP) *** Adjust for Staff Qualifications – Under Consideration - To be Populated for Pilot

    #endregion

    #region  HR: Step 09 - *** EHT Tax is applied at the calculator level *** Add EHT (Employer Health Tax) 

    #endregion

    #region  HR: Step 10 - Add Professional Development Expenses and Professional Dues
    private decimal TotalPD_Wages => (_rateSchedule!.ofm_licensed_childcare_hours_per_fte ?? 0m + _rateSchedule!.ofm_elf_hours_per_fte!.Value
        + _rateSchedule!.ofm_inclusion_hours_per_fte!.Value + _rateSchedule!.ofm_cultural_hours_per_fte!.Value) * WageGridMarkup * TotalAdjustedFTEsCostPerHour;
    private decimal TotalPD_Benefits => TotalPD_Wages * (_rateSchedule!.ofm_average_benefit_load!.Value / 100);
    public decimal ProfessionalDevelopmentHours => TotalPD_Wages + TotalPD_Benefits;

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
    ITE = 1,
    [Description("Early Childhood Educator")]
    ECE = 2,
    [Description("Early Childhood Educator Assitance")]
    ECEA = 3,
    [Description("Responsible Adult")]
    RA = 4
}