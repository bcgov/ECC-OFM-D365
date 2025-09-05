using OFM.Infrastructure.WebAPI.Handlers;
using OFM.Infrastructure.WebAPI.Models.ApplicationScore;
namespace OFM.Infrastructure.WebAPI.Services.Processes.ApplicationScore;
/// <summary>
/// Interface defining the contract for all scoring strategies.
/// Each strategy implements this interface and provides a unique evaluation logic.
/// </summary>

public interface IScoreStrategy
{
    /// <summary>
    /// Evaluates the scoring criteria based on various parameters.
    /// </summary>
    /// <param name="comparisonHandler">Handles comparison operations.</param>
    /// <param name="parameters">Score parameters.</param>
    /// <param name="application">Funding application details.</param>
    /// <param name="facilityData">Facility details.</param>
    /// <param name="licenseData">License spaces data.</param>
    /// <param name="incomeData">Income indicator data.</param>
    /// <param name="feeData">Approved parent fees.</param>
    /// <param name="thresholdData">Threshold fee data.</param>
    /// <param name="populationData">Population center data.</param>
    /// <param name="schoolDistrictData">School district data.</param>
    /// <returns>Returns a boolean indicating whether the criteria is met.</returns>

    Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData);
}

// Concrete Strategy: Evaluates based on ACCB - Income Indicator

public class IncomeIndicatorStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);
    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        if (incomeData == null) throw new ArgumentException($"No ACCB Income Data found for postal code: {facilityData.PostalCode}");
        var incomeIndicator = incomeData.MedianIncome;
        return Task.FromResult(comparisonHandler.Handle(_operator, incomeIndicator, comparisonValue));
    }
}

// Concrete Strategy: Evaluates based on operational spaces above 30 on the ofm_license_detail
public class Operational30SpacesStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        if (licenseData == null) throw new ArgumentException("No License found for the facility");
        if (licenseData.TotalChildCareSpaces <= 0 || licenseData.TotalChildCareSpaces == null) throw new ArgumentException("No Operational spaces found for the facility");
        var totalOperationalSpaces = licenseData.TotalChildCareSpaces;
        return Task.FromResult(comparisonHandler.Handle(_operator, totalOperationalSpaces, comparisonValue));
    }
}
// Concrete Strategy: Evaluates based on operational spaces above the 30 spaces
public class IncrementalSpacesStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        if (licenseData == null) throw new ArgumentException("No License found for the facility");
        if (licenseData.TotalChildCareSpaces <= 0 || licenseData.TotalChildCareSpaces == null) throw new ArgumentException("No Operational spaces found for the facility");
        var totalOperationalSpaces = licenseData.TotalChildCareSpaces;
        return Task.FromResult(comparisonHandler.Handle(_operator, totalOperationalSpaces, comparisonValue));
    }
}
// Concrete Strategy: Evaluates based on preschool operational spaces ratio in the ofm_licence_detail
public class PreSchoolOperationalSpacesStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        if (licenseData == null) throw new ArgumentException("No License found for the facility");
        if (licenseData.TotalChildCareSpaces <= 0 || licenseData.TotalChildCareSpaces == null) throw new ArgumentException("No Operational spaces found for the facility");

        var preschoolSpaces = licenseData.MaxPreSchoolChildCareSpaces;
        var totalOperationalSpaces = licenseData.TotalChildCareSpaces;
        var ratio = totalOperationalSpaces > 0 ? (preschoolSpaces / totalOperationalSpaces) : 0;
        return Task.FromResult(comparisonHandler.Handle(_operator, ratio, comparisonValue));
    }
}
// Concrete Strategy: Evaluates based on Indigenous-led status of the parent organization of the facility
public class IndigenousLedStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        if (facilityData.IndigenousLead == null) throw new ArgumentException("Indigenous Flag is not set");
        return Task.FromResult(comparisonHandler.Handle(_operator, facilityData.IndigenousLead == true ? "Yes" : "No", comparisonValue));
    }
}
// Concrete Strategy: Evaluates based on Parent fees are under the 40 Percentile fees
public class ParentFeesStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);
    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        if (string.IsNullOrEmpty(facilityData.Region)) throw new ArgumentException("Facility Region is missing");
        if (feeData == null) throw new ArgumentException("Approved Parent Fees are missing");
        if (!feeData.Any()) throw new ArgumentException("Approved Parent Fees are missing");
        if (thresholdData == null) throw new ArgumentException("40P fees are missing");
        if (!thresholdData.Any()) throw new ArgumentException("40P fees are missing");
        var providerType = application.ProviderType ?? throw new ArgumentException("Application is not assigned any provider type");
        string parentFees = "No";


        if (feeData?.Where(f => f.FinancialYear == thresholdData.First().ProgramYear) == null || feeData?.Where(f => f.FinancialYear == thresholdData.First().ProgramYear)?.Any() == false)
        {
            throw new ArgumentException("No Approved Parent Fees match the Program Year specified on the 40th Percentile table");

        }

        foreach (var fee in feeData.Where(f => f.FinancialYear == thresholdData.First().ProgramYear))
        {

            if (fee.ApproveDate == null) throw new ArgumentException("Atleast 1 Approved Parent Fees is missing an Approved Date");
            var thresholdFee = thresholdData.Where(t => t.ProgramType == fee.ProgramType && t.ProviderType == providerType && t.ProgramYear == fee.FinancialYear && t.Region == facilityData.Region)?.FirstOrDefault();

            if (thresholdFee != null && fee != null && thresholdFee.MaximumFeeAmount.HasValue && fee.FeeAmount.HasValue && thresholdFee.MaximumFeeAmount >= fee.FeeAmount)
            {
                parentFees = "Yes";
            }
            else
            {
                parentFees = "No"; break;
            }

        }
        return Task.FromResult(comparisonHandler.Handle(_operator, parentFees, comparisonValue));
    }
}
// Concrete Strategy: Evaluates based on the Non-Proffit status of Parent Organization
public class NotForProfitStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        var isNotForProfit = "No";
        if (facilityData.OrganizationBusinessType?.ToLower()?.Trim() == "non-profit society")
        {
            if (facilityData.OrganizationDateOfIncorporation == null || facilityData.OrganizationDateOfIncorporation == DateTime.MinValue) throw new ArgumentException("Date of Incorporation missing on the Organization");
            if (string.IsNullOrEmpty(facilityData.OrganizationOpenMembership)) throw new ArgumentException("OpenMembership is not set to Yes/No on the Organization");
            if (string.IsNullOrEmpty(facilityData.OrganizationBoardMembersBCResidents)) throw new ArgumentException("BoardMembersBCResidents is not set to Yes/No on the Organization");
            if (string.IsNullOrEmpty(facilityData.OrganizationBoardMembersMembership)) throw new ArgumentException("BoardMembersEntireMembership is not set to Yes/No on the Organization");
            if (string.IsNullOrEmpty(facilityData.OrganizationBoardMembersUnpaid)) throw new ArgumentException("BoardMembersElectedUnpaid is not set to Yes/No on the Organization");
            if (facilityData.OrganizationDateOfIncorporation > DateTime.Now.AddYears(-4) && (facilityData.LetterOfSupportExists == null || facilityData.LetterOfSupportExists == false)) throw new ArgumentException("No Community Support letter provided as Date of Incorporation is not older than 4 years");

            
            if (facilityData.OrganizationDateOfIncorporation < DateTime.UtcNow.AddYears(-4) || facilityData.LetterOfSupportExists == true)
                if (facilityData.OrganizationOpenMembership == "Yes" && facilityData.OrganizationBoardMembersUnpaid == "Yes" && facilityData.OrganizationBoardMembersMembership == "Yes" && facilityData.OrganizationBoardMembersBCResidents == "Yes")
                    isNotForProfit = "Yes";            
        }
        return Task.FromResult(comparisonHandler.Handle(_operator, isNotForProfit, comparisonValue));

    }
}
// Concrete Strategy: Evaluates based on the if Postal code of the facility belongs to High Population centres
public class PopulationCentreStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        if (populationData == null) throw new ArgumentException($"Population Centre is missing for city: '{facilityData.City}'");
        if (populationData.ProjectedPopulation == 0) throw new ArgumentException($"Projected Population in Population Centre is missing for city: '{facilityData.City}'");
        return Task.FromResult(comparisonHandler.Handle(_operator, populationData.ProjectedPopulation, comparisonValue));
    }
}
//Concrete Strategy: Evaluates based on the Public Organization status of Parent Organization
public class PublicInstitutionStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {

        return Task.FromResult(comparisonHandler.Handle(_operator, facilityData?.OrganizationPublicSector ?? string.Empty, comparisonValue));
    }
}
//Concrete Strategy: Evaluates based on the postal code of the facility and scholl Districts data
public class SchoolDistrictStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {
        if (schoolDistrictData == null || schoolDistrictData?.SchoolDistrictFullName == null) throw new ArgumentException($"School District is missing for postal code: {facilityData.PostalCode}");
        var schoolDistrict = schoolDistrictData.SchoolDistrictFullName;
        return Task.FromResult(comparisonHandler.Handle(_operator, schoolDistrict, comparisonValue));
    }
}
/// Factory class to create appropriate strategy instances based on category name
public class LocationStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, OFMApplication application, Facility facilityData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData)
    {

        if(application.MonthToMonth == "Yes" || application.FacilityType?.ToLower()?.Trim() == "provided free of charge")
            return Task.FromResult(comparisonHandler.Handle(_operator, "No", comparisonValue));

        if (application.FacilityType?.ToLower()?.Trim() == "rent/lease")
        {
            DateTime? leaseStartDate = application.SubmittedOn;
            DateTime? leaseEndDate = application.LeaseEndDate;
            return Task.FromResult(comparisonHandler.Handle(_operator, application.FacilityType == "Rent/Lease" ? CheckLocationStability(leaseStartDate, leaseEndDate) : "No", comparisonValue));
        }
        if (application.FacilityType?.ToLower()?.Trim() == "owned with mortgage" || application.FacilityType?.ToLower()?.Trim() == "owned without mortgage")
        {
            return Task.FromResult(comparisonHandler.Handle(_operator, "Yes", comparisonValue));
        }
        return Task.FromResult(comparisonHandler.Handle(_operator, "No", comparisonValue));
    }
    static string CheckLocationStability(DateTime? leaseStart, DateTime? leaseEnd)
    {
        // Ensure both dates exist
        if (!leaseStart.HasValue || !leaseEnd.HasValue)
            return "No";
        // Calculate lease duration in days
        int leaseDurationDays = (leaseEnd.Value - leaseStart.Value).Days;
        // Check if lease duration is at least 730 days (2 years)
        return leaseDurationDays >= 730? "Yes" : "No";
    }

}

// Strategy Factory to create appropriate strategy instances
public static class ScoreStrategyFactory
{

    public static IScoreStrategy CreateStrategy(string categoryName, int comparisonOperator, string comparisonValue)
    {
        return categoryName switch
        {
            "ACCB Income Indicator" => new IncomeIndicatorStrategy(comparisonOperator, comparisonValue),
            "0-5 Age Group Spaces Ratio" => new PreSchoolOperationalSpacesStrategy(comparisonOperator, comparisonValue),
            "Incremental Operational Spaces" => new IncrementalSpacesStrategy(comparisonOperator, comparisonValue),
            "Parent Fees" => new ParentFeesStrategy(comparisonOperator, comparisonValue),
            "Not for Profit" => new NotForProfitStrategy(comparisonOperator, comparisonValue),
            "Indigenous Led" => new IndigenousLedStrategy(comparisonOperator, comparisonValue),
            "Operational Spaces Above 30" => new Operational30SpacesStrategy(comparisonOperator, comparisonValue),
            "Population Centre" => new PopulationCentreStrategy(comparisonOperator, comparisonValue),
            "Public Institution" => new PublicInstitutionStrategy(comparisonOperator, comparisonValue),
            "School District" => new SchoolDistrictStrategy(comparisonOperator, comparisonValue),
            "Location Stability" => new LocationStrategy(comparisonOperator, comparisonValue),
            _ => throw new ArgumentException($"No Score strategy found for category: {categoryName}")
        };
    }
}

