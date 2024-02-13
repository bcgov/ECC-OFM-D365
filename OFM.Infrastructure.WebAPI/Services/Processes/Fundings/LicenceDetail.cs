﻿using ECC.Core.DataContext;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using Polly.Caching;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class CoreService : ofm_licence_detail
{
    private RateSchedule? _rateSchedule;
    private readonly bool _applyDuplicateCareType;
    private readonly bool _applySplitRoom;

    private IEnumerable<Ofm_Rateschedule_Cclr> CCLRRatios
    {
        get
        {
            return _rateSchedule?.ofm_rateschedule_cclr ?? [];
        }
    }

    public new string? ofm_week_days { get; set; }
    public RateSchedule RateSchedule { set { _rateSchedule = value; } }
    private int OrderId { get; set; }
    private Guid LicenceTypeId => base.GetAttributeValue<Guid>(Fields.ofm_licence_detailid);
    private string LicenceTypeName => FormattedValues[Fields.ofm_licence_type];
    public ecc_licence_type LicenceType => (ecc_licence_type)base.GetAttributeValue<OptionSetValue>(Fields.ofm_licence_type).Value;
    private int LicenceTypeNumber => base.GetAttributeValue<OptionSetValue>(Fields.ofm_licence_type).Value;
    public int Spaces => base.GetAttributeValue<int>(Fields.ofm_operational_spaces);
    private decimal WeeksPerYear => base.GetAttributeValue<int>(Fields.ofm_weeks_in_operation);
    private int DaysPerWeek => ofm_week_days?.Split(",").Length ?? 0;
    private DateTime TimeFrom => base.GetAttributeValue<DateTime>(Fields.ofm_operation_hours_from);
    private DateTime TimeTo => base.GetAttributeValue<DateTime>(Fields.ofm_operation_hours_to);
    private decimal HoursPerDay => (TimeTo - TimeFrom).Hours; // Todo: consider Minutes?
    public decimal AnnualStandardHours => HoursPerDay * DaysPerWeek * WeeksPerYear; //Example: 10 * 5 * 50.2 = 2510
    /// <summary>
    /// Note that an FTE is expected to work 1957.5 hours a year and the number of available hours for childcare is less after accounting for training, sick days, vacation, and stat days
    /// </summary>
    /// 
    public decimal AnnualAvailableHoursPerFTE => ExpectedAnnualFTEHours -
                                                    (ProfessionalDevelopmentHours + // ToDo: Development Hours has a new logic/value
                                                    _rateSchedule.ofm_vacation_hours_per_fte!.Value +
                                                    _rateSchedule.ofm_sick_hours_per_fte!.Value +
                                                    _rateSchedule.ofm_statutory_breaks!.Value);
    public decimal AnnualHoursAdjustmentRatio => AnnualStandardHours / AnnualAvailableHoursPerFTE;
    public decimal ExpectedAnnualFTEHours => _rateSchedule.ofm_total_fte_hours_per_year!.Value; // 1957.5 
    public decimal ProfessionalDevelopmentHours => _rateSchedule.ofm_professional_development_hours!.Value;

    #region HR: Step 01 - Allocate Spaces to Efficient Group Sizes

    //public IEnumerable<CCLRType> GroupSizes => FilterCCLRByCareType(LicenceTypeNumber).Select(cclr => cclr.GroupSize);
    //private IEnumerable<CCLRRatio> FilterCCLRByCareType(int careType)
    //{
    //    var filteredByCareTypes = _CCLRRatioTable.Where(r =>
    //    {
    //        bool isEqual = r.CareTypes.Contains(careType);
    //        return isEqual;
    //    });

    //    return FilterCCLRBySpaces(filteredByCareTypes);
    //}
    //private IEnumerable<CCLRRatio> FilterCCLRBySpaces(IEnumerable<CCLRRatio> filteredList)
    //{
    //    var spacesCount = Spaces;
    //    Stack<CCLRRatio> stack = new(filteredList);

    //    while (stack.Count > 0 && spacesCount > 0)
    //    {
    //        var currentItem = stack.Peek();
    //        if (currentItem.SpacesMax <= spacesCount || spacesCount >= currentItem.SpacesMin)
    //        {
    //            spacesCount -= currentItem.SpacesMax;
    //            if (spacesCount < currentItem.SpacesMin)
    //                yield return stack.Pop();
    //            else
    //                yield return stack.Peek();
    //        }
    //        else
    //            stack.Pop();
    //    }
    //}

    public IEnumerable<ecc_group_size> GroupSizes => FilterCCLRByCareType(LicenceType).Select(cclr => cclr.ofm_group_size!.Value);
    private IEnumerable<Ofm_Rateschedule_Cclr> FilterCCLRByCareType(ecc_licence_type careType)
    {
        var filteredByCareTypes = CCLRRatios.Where(cclr =>
        {
            var caretypesMap = cclr.ofm_licence_mapping.Split(",")?.Select(Int32.Parse)?.ToList<int>();
            bool isEqual = caretypesMap.Contains<int>((int)careType);
            return isEqual;
        });

        return FilterCCLRBySpaces(filteredByCareTypes);
    }

    private IEnumerable<Ofm_Rateschedule_Cclr> FilterCCLRBySpaces(IEnumerable<Ofm_Rateschedule_Cclr> filteredList)
    {
        var spacesCount = Spaces;
        Stack<Ofm_Rateschedule_Cclr> stack = new(filteredList);

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

    private int RawITE => FilterCCLRByCareType(LicenceType).Sum(x => x.ofm_fte_min_ite!.Value);
    private int RawECE => FilterCCLRByCareType(LicenceType).Sum(x => x.ofm_fte_min_ece!.Value);
    private int RawECEA => FilterCCLRByCareType(LicenceType).Sum(x => x.ofm_fte_min_ecea!.Value);
    private int RawRA => FilterCCLRByCareType(LicenceType).Sum(x => x.ofm_fte_min_ra!.Value);
    private int TotalRawFTEs => RawITE + RawECE + RawECEA + RawRA;

    #endregion

    #region HR: Step 03 - Adjust Staffing Required by Hrs of Child Care

    private decimal AdjustedITE => RawITE * AnnualHoursAdjustmentRatio;
    private decimal AdjustedECE => RawECE * AnnualHoursAdjustmentRatio;
    private decimal AdjustedECEA => RawECEA * AnnualHoursAdjustmentRatio;
    private decimal AdjustedRA => RawRA * AnnualHoursAdjustmentRatio;
    private decimal TotalAdjustedFTEs => TotalRawFTEs * AnnualHoursAdjustmentRatio;

    #endregion

    #region  HR: Step 04 - Apply Hourly Wages to Staffing

    private Dictionary<WageType, decimal> HourlyWageGrid
    {
        get
        {
            return new Dictionary<WageType, decimal>()
            {
                [WageType.ITE] = _rateSchedule.ofm_wages_ite_cost.Value,
                [WageType.ECE] = _rateSchedule.ofm_wages_ece_cost_base.Value,
                [WageType.ECEA] = _rateSchedule.ofm_wages_ecea_cost.Value,
                [WageType.RA] = _rateSchedule.ofm_wages_ra_cost.Value
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

    private decimal RequiredSupervisors => _rateSchedule.ofm_supervisor_ratio!.Value * GroupSizes.Count();
    private decimal SupervisorRateDifference
    {
        get
        {
            return _rateSchedule.ofm_supervisor_rate switch
            {
                "ITE/SNE Supervisor" => _rateSchedule.ofm_wages_ite_sne_supervisor_differential!.Value,
                "ECE Supervisor" => _rateSchedule.ofm_wages_ece_supervisor_differential!.Value,
                "RA Supervisor" => _rateSchedule.ofm_wages_ra_supervisor_differential!.Value,
                _ => 0m
            };
        }
    }
    private decimal SupervisorCostDiffPerYear => RequiredSupervisors * SupervisorRateDifference * (AnnualStandardHours * Spaces / Spaces);
    private decimal WageGridMarkup => (1 + _rateSchedule.ofm_wage_grid_markup!.Value); // Plus 1 so that it does not zero out the calculation
    public decimal TotalStaffingCost => TotalAdjustedFTEsCostPerHour * SupervisorCostDiffPerYear * ExpectedAnnualFTEHours * WageGridMarkup; // Including Supervisors
    private decimal TotalCostPerFTEPerYear => TotalStaffingCost / TotalAdjustedFTEs;

    #endregion

    #region  HR: Step 06 - Apply Benefits

    public decimal TotalBenefitsCostPerYear => TotalStaffingCost * (_rateSchedule.ofm_average_benefit_load!.Value / 100);
    private decimal QualityEnhancementCost => (TotalStaffingCost + TotalBenefitsCostPerYear) * (_rateSchedule.ofm_quality_enhancement_factor!.Value / 100);

    #endregion

    #region  HR: Step 07 - *** (Not MVP) ***  Apply Quality Enhancement Factor – Under Consideration

    #endregion

    #region  HR: Step 08 - *** (Not MVP) *** Adjust for Staff Qualifications – Under Consideration - To be Populated for Pilot

    #endregion

    #region  HR: Step 09 - Add EHT (Employer Health Tax)

    public decimal TotalRenumeration => TotalStaffingCost + TotalBenefitsCostPerYear + QualityEnhancementCost;
    private decimal GetEHTRate(ofm_application application)
    {
        var tempData = new { Ownership = application.ofm_summary_ownership, Renumeration = TotalRenumeration };
        var threshold = tempData switch
        {
            { Ownership: ecc_Ownership.Private, Renumeration: < 1_500_000m, Renumeration: > 500_000m } => _rateSchedule.ofm_for_profit_eht_over_500k!.Value,
            { Ownership: ecc_Ownership.Private, Renumeration: > 1_500_000m } => _rateSchedule.ofm_for_profit_eht_over_1_5m!.Value,
            { Ownership: ecc_Ownership.Notforprofit, Renumeration: > 1_500_000m } => _rateSchedule.ofm_not_for_profit_eht_over_1_5m!.Value,
            null => throw new ArgumentNullException(nameof(application), "Can't calculate EHT threshold on null funding application"),
            _ => 0m
        };

        return threshold;
    }
    public decimal GetEmployerHealthTax(ofm_application application)
    {
        var rate = GetEHTRate(application);

        return TotalRenumeration * rate;
    }

    #endregion

    #region  HR: Step 10 - Add Professional Development Expenses and Professional Dues
    public decimal TotalProfessionalDevelopmentExpenses => (_rateSchedule.ofm_licenced_childcare_cap_per_fte_per_year.Value +
                                                               _rateSchedule.ofm_elf_educational_programming_cap_fte_year.Value +
                                                               _rateSchedule.ofm_pde_inclusion_training.Value +
                                                               _rateSchedule.ofm_pde_cultural_training.Value) *
                                                               TotalAdjustedFTEs
                                                           ;

    public decimal TotalProfessionalDues => _rateSchedule.ofm_standard_dues_per_fte.Value * TotalAdjustedFTEs;

    #endregion
}

public enum CCLRType
{
    TypeA,
    TypeB,
    TypeC
}

public enum WageType
{
    ITE,
    ECE,
    ECEA,
    RA
}