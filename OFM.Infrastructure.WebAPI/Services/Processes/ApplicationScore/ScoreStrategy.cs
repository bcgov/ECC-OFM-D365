using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using OFM.Infrastructure.WebAPI.Handlers;
using OFM.Infrastructure.WebAPI.Models.ApplicationScore;
using System.ComponentModel;
using System.Text.Json.Nodes;
namespace OFM.Infrastructure.WebAPI.Services.Processes.ApplicationScore;
public interface IScoreStrategy
{
    Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData,PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData);
}

// Concrete Strategies
public class IncomeIndicatorStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);
    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        if(incomeData == null) throw new ArgumentException($"No ACCB Income Data found for postal code: {schoolData.PostalCode}");
        var incomeIndicator = incomeData.MedianIncome;
        return Task.FromResult(comparisonHandler.Handle(_operator, incomeIndicator, comparisonValue));
    }
}

public class Operational30SpacesStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        if (licenseData == null) throw new ArgumentException("No License found for the facility");
        if (licenseData.TotalChildCareSpaces <= 0 || licenseData.TotalChildCareSpaces == null) throw new ArgumentException("No Operational spaces found for the facility");
        var totalOperationalSpaces = licenseData.TotalChildCareSpaces;
        return Task.FromResult(comparisonHandler.Handle(_operator, totalOperationalSpaces, comparisonValue));
    }
}
public class IncrementalSpacesStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        if (licenseData == null) throw new ArgumentException("No License found for the facility");
        if (licenseData.TotalChildCareSpaces <= 0 || licenseData.TotalChildCareSpaces == null) throw new ArgumentException("No Operational spaces found for the facility");
        var totalOperationalSpaces = licenseData.TotalChildCareSpaces;       
        return Task.FromResult(comparisonHandler.Handle(_operator, totalOperationalSpaces, comparisonValue));
    }
}
public class PreSchoolOperationalSpacesStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        if (licenseData == null) throw new ArgumentException("No License found for the facility");
        if (licenseData.TotalChildCareSpaces <= 0 || licenseData.TotalChildCareSpaces == null) throw new ArgumentException("No Operational spaces found for the facility");

        var preschoolSpaces = licenseData.MaxPreSchoolChildCareSpaces;
        var totalOperationalSpaces = licenseData.TotalChildCareSpaces;
        var ratio = totalOperationalSpaces > 0 ? (preschoolSpaces / totalOperationalSpaces) * 100 : 0;
        return Task.FromResult(comparisonHandler.Handle(_operator, ratio, comparisonValue));
    }
}

public class IndigenousLedStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        if (schoolData.IndigenousLead == null) throw new ArgumentException("Indigenous Flag is not set"); 
        return Task.FromResult(comparisonHandler.Handle(_operator, schoolData.IndigenousLead, comparisonValue));
    }
}

public class ParentFeesStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);
    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {

        if (feeData == null) throw new ArgumentException("Approved Parent Fees are missing");
        if (!feeData.Any()) throw new ArgumentException("Approved Parent Fees are missing");
        if (thresholdData == null) throw new ArgumentException("40P fees are missing");
        if (!thresholdData.Any()) throw new ArgumentException("40P fees are missing");
        string parentFees = "No";
        foreach (var fee in feeData)
        {

            var thresholdFee = thresholdData.Where(t => t.ProgramType == fee.ProgramType).FirstOrDefault();
            if (thresholdFee!= null && fee != null && thresholdFee.MaximumFeeAmount.HasValue && fee.FeeAmount.HasValue  && thresholdFee.MaximumFeeAmount > fee.FeeAmount)
                parentFees = "Yes";
            else
                parentFees = "No";       
        
        }        
        return Task.FromResult(comparisonHandler.Handle(_operator, parentFees, comparisonValue));
    }
}

public class NotForProfitStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        if (schoolData.DateOfIncorporation == null || schoolData.DateOfIncorporation == DateTime.MinValue) throw new ArgumentException("Date of Incorporation missing on the facility");
        if (schoolData.OpenMembership == null) throw new ArgumentException("OpenMembership is not set to Yes/No");
        if (schoolData.BoardMembersBCResidents == null) throw new ArgumentException("BoardMembersBCResidents is not set to Yes/No");
        if (schoolData.BoardMembersMembership == null) throw new ArgumentException("BoardMembersEntireMembership is not set to Yes/No");
        if (schoolData.BoardMembersUnpaid == null) throw new ArgumentException("BoardMembersElectedUnpaid is not set to Yes/No");
        

        var isNotForProfit = "No";
        if (schoolData.DateOfIncorporation.Value < DateTime.UtcNow.AddYears(-4))
            if (schoolData.OrganizationBusinessType?.ToLower() == "non-profit society" && schoolData.OpenMembership.Value && schoolData.BoardMembersUnpaid.Value && schoolData.BoardMembersMembership.Value && schoolData.BoardMembersUnpaid.Value && application.LetterOfSupportExists == true)
                isNotForProfit = "Yes";
        return Task.FromResult(comparisonHandler.Handle(_operator, isNotForProfit, comparisonValue));
    }
}
public class PopulationCentreStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        if (populationData == null) throw new ArgumentException($"Poplation Centre is missing for city: '{schoolData.City}'");
        if (populationData.ProjectedPopulation == 0) throw new ArgumentException($"Projected Poplation Centre is missing for city: '{schoolData.City}'");
        return Task.FromResult(comparisonHandler.Handle(_operator, populationData.ProjectedPopulation, comparisonValue));
    }
}
public class PublicInstitutionStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        
        return Task.FromResult(comparisonHandler.Handle(_operator, string.IsNullOrEmpty(publicOrganizationData?.OrganizationName) ? "No": "Yes", comparisonValue));
    }
}
public class SchoolDistrictStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        if (schoolDistrictData == null) throw new ArgumentException("School District is missing");
        var schoolDistrict = schoolDistrictData.SchoolDistrictFullName;
        return Task.FromResult(comparisonHandler.Handle(_operator, schoolDistrict, comparisonValue));
    }
}
public class LocationStrategy(int comparisonOperator, string comparisonValue) : IScoreStrategy
{
    private string _operator => OperatorMapper.MapOperator(comparisonOperator);

    public Task<bool> EvaluateAsync(IComparisonHandler comparisonHandler, ScoreParameter parameters, FundingApplication application, Facility schoolData, LicenseSpaces licenseData, ACCBIncomeIndicator incomeData, IEnumerable<ApprovedParentFee> feeData, IEnumerable<FortyPercentileThresholdFee> thresholdData, PopulationCentre populationData, SchoolDistrict schoolDistrictData, PublicOrganization publicOrganizationData)
    {
        DateTime? leaseStartDate = application.LeaseStartDate; 
        DateTime? leaseEndDate = application.LeaseEndDate;
        return Task.FromResult(comparisonHandler.Handle(_operator, CheckLocationStability(leaseStartDate, leaseEndDate), comparisonValue));
    }
    static string CheckLocationStability(DateTime? leaseStart, DateTime? leaseEnd)
    {
        // Ensure both dates exist
        if (!leaseStart.HasValue || !leaseEnd.HasValue)
            return "No";
        // Calculate lease duration in days
        int leaseDurationDays = (leaseEnd.Value - leaseStart.Value).Days;
        // Check if lease duration is at least 730 days (2 years)
        return leaseDurationDays >= 730 && leaseEnd > new DateTime(2026, 9, 1) ? "Yes" : "No";
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

