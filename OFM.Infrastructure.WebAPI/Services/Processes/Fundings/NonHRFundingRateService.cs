using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public abstract class NonHRFundingRateServiceBase
{
    public abstract decimal GetScheduleAmount();
    public override string ToString() => GetType().Name;
    protected static IEnumerable<decimal> ComputeRate(IEnumerable<dynamic> steps, int totalSpaces)
    {
        foreach (var step in steps.OrderBy(s => s.step))
        {
            if (Convert.ToInt32(step.max) < totalSpaces)
                yield return Convert.ToInt32(step.max) * Convert.ToDecimal(step.rate);
            else
                yield return (Convert.ToInt32(step.max) - totalSpaces) * Convert.ToDecimal(step.rate);
        }
    }
}

public class ProgrammingRateService : NonHRFundingRateServiceBase
{
    private readonly int _totalSpaces;

    public ProgrammingRateService(int totalSpaces)
    {
        _totalSpaces = totalSpaces;
    }

    public override decimal GetScheduleAmount()
    {
        List<dynamic> rateSchedule = new List<dynamic>() {
                new { step = 1, min = 0,  max = 20, rate = 932m},
                new { step = 2, min = 21, max = 30, rate = 788m},
                new { step = 3, min = 31, max = 50, rate = 713m},
                new { step = 4,  min = 51, max = 374, rate = 681m},
            };

        var steps = rateSchedule?.Where(x => x.min <= _totalSpaces).ToList();

        return ComputeRate(steps, _totalSpaces).Sum();
    }
}
public class AdministrativeRateService : NonHRFundingRateServiceBase
{
    private readonly int _totalspace;

    public AdministrativeRateService(int totalSpaces)
    {
        _totalspace = totalSpaces;
    }

    public override decimal GetScheduleAmount()
    {
        List<dynamic> rateSchedule = new List<dynamic>() {
                new { step = 1, min = 0,  max = 20, rate = 1557m},
                new { step = 2, min = 21, max = 30, rate = 1192m},
                new { step = 3, min = 31, max = 50, rate = 659m},
                new { step = 4,  min = 51, max = 374, rate = 429m},
            };

        var steps = rateSchedule?.Where(x => x.min <= _totalspace).ToList();

        return ComputeRate(steps, _totalspace).Sum();
    }
}
public class OperationalRateService : NonHRFundingRateServiceBase
{
    private readonly int _totalspace;

    public OperationalRateService(int totalSpaces)
    {
        _totalspace = totalSpaces;
    }

    public override decimal GetScheduleAmount()
    {
        List<dynamic> rateSchedule = new List<dynamic>() {
                new { step = 1, min = 0,  max = 20, rate = 1557m},
                new { step = 2, min = 21, max = 30, rate = 1192m},
                new { step = 3, min = 31, max = 50, rate = 659m},
                new { step = 4,  min = 51, max = 374, rate = 429m},
            };

        var steps = rateSchedule?.Where(x => x.min <= _totalspace).ToList();

        return ComputeRate(steps, _totalspace).Sum();
    }
}
public class FacilityRateService : NonHRFundingRateServiceBase
{
    private readonly int _totalspace;

    public FacilityRateService(int totalSpaces)
    {
        _totalspace = totalSpaces;
    }

    public override decimal GetScheduleAmount()
    {
        List<dynamic> rateSchedule = new List<dynamic>() {
                new { step = 1, min = 0,  max = 20, rate = 1557m},
                new { step = 2, min = 21, max = 30, rate = 1192m},
                new { step = 3, min = 31, max = 50, rate = 659m},
                new { step = 4,  min = 51, max = 374, rate = 429m},
            };

        var steps = rateSchedule?.Where(x => x.min <= _totalspace).ToList();

        return ComputeRate(steps, _totalspace).Sum();
    }
}

public abstract class FundingRateFactory
{
    public abstract NonHRFundingRateServiceBase CreateFundingRateService();
}

public class ProgrammingRateFactory : FundingRateFactory
{
    private readonly int _totalSpace;
    public ProgrammingRateFactory(int totalSpace)
    {
        _totalSpace = totalSpace;
    }
    public override NonHRFundingRateServiceBase CreateFundingRateService()
    {
        return new ProgrammingRateService(_totalSpace);
    }
}
public class AdministrativeRateFactory : FundingRateFactory
{
    private readonly int _totalSpace;
    public AdministrativeRateFactory(int totalSpace)
    {
        _totalSpace = totalSpace;
    }
    public override NonHRFundingRateServiceBase CreateFundingRateService()
    {
        return new AdministrativeRateService(_totalSpace);
    }
}
public class OperationalRateFactory : FundingRateFactory
{
    private readonly int _totalSpace;
    public OperationalRateFactory(int totalSpace)
    {
        _totalSpace = totalSpace;
    }
    public override OperationalRateService CreateFundingRateService()
    {
        return new OperationalRateService(_totalSpace);
    }
}
public class FacilityRateFactory : FundingRateFactory
{
    private readonly int _totalSpace;
    public FacilityRateFactory(int totalSpace)
    {
        _totalSpace = totalSpace;
    }
    public override NonHRFundingRateServiceBase CreateFundingRateService()
    {
        return new FacilityRateService(_totalSpace);
    }
}