using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.Diagnostics;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IFundingCommand
{
    bool Success { get; set; }
    void Execute();
    bool CanExecute();
    void LogCommand();
}

//public interface ICompositeFundingCommand
//{
//    //HashSet<IFundingCommand> Commands { get; set; }
//    void AddCommand();
//    void ExecuteAll();
//}

public interface IFundingAmountsRepository
{
    void CalculateProgrammingFundingAmounts(Funding funding, decimal scheduleAmount);
    void CalculateAdministrativeFundingAmounts(Funding funding, decimal scheduleAmount);
    void CalculateOperaionalFundingAmounts(Funding funding, decimal scheduleAmount);
    void CalculateFacilityFundingAmounts(Funding funding, decimal scheduleAmount);
}

public class FundingAmountsRepository : IFundingAmountsRepository
{
    private readonly Funding _funding;
    const int MAX_OPERATIONAL_HOURS = 2510;

    public FundingAmountsRepository(Funding funding)
    {
        _funding = funding;
    }

    public void CalculateProgrammingFundingAmounts(Funding funding, decimal scheduleAmount)
    {
        // No adjustment for programming envelope at the moment.
        var parentfees = 100_000m;
        var baseAmount = scheduleAmount - parentfees;

        //_funding.ofm_envelope_programming_proj = new Microsoft.Xrm.Sdk.Money(scheduleAmount);
        //_funding.ofm_envelope_programming_pf = new Microsoft.Xrm.Sdk.Money(parentfees);
        //_funding.ofm_envelope_programming = new Microsoft.Xrm.Sdk.Money(baseAmount);
    }

    public void CalculateAdministrativeFundingAmounts(Funding funding, decimal scheduleAmount)
    {
        var parentfees = 100_000m;
        var baseAmount = scheduleAmount - parentfees;

        //var maxOperationalHours = Convert.ToDecimal(funding.LicenceDetails.Max(detail => detail.ofm_weeks_in_operation * detail.ofm_week_days.Count() * (detail.ofm_operation_hours_to - detail.ofm_operation_hours_from).Value.TotalHours).Value);
        //var adjustmentRate = MAX_OPERATIONAL_HOURS / maxOperationalHours;

        //var projected = scheduleAmount / adjustmentRate;

        //if (funding.ApplicationData.ofm_summary_ownership!.Value == ecc_Ownership.Homebased)
        //    projected = Math.Min(scheduleAmount / adjustmentRate, funding.ApplicationData.ofm_costs_year_facility_costs.Value);

        //_funding.ofm_envelope_administrative_proj = new Microsoft.Xrm.Sdk.Money(projected);
        //_funding.ofm_envelope_administrative_pf = new Microsoft.Xrm.Sdk.Money(parentfees);
        //_funding.ofm_envelope_administrative = new Microsoft.Xrm.Sdk.Money(baseAmount);
    }

    public void CalculateOperaionalFundingAmounts(Funding funding, decimal projectedAmount)
    {
        var parentfees = 100_000m;
        var baseAmount = projectedAmount - parentfees;

        //_funding.ofm_envelope_operational_proj = new Microsoft.Xrm.Sdk.Money(projectedAmount);
        //_funding.ofm_envelope_operational_pf = new Microsoft.Xrm.Sdk.Money(parentfees);
        //_funding.ofm_envelope_operational = new Microsoft.Xrm.Sdk.Money(baseAmount);
    }

    public void CalculateFacilityFundingAmounts(Funding funding, decimal projectedAmount)
    {
        var parentfees = 100_000m;
        var baseAmount = projectedAmount - parentfees;

        //_funding.ofm_envelope_facility_proj = new Microsoft.Xrm.Sdk.Money(projectedAmount);
        //_funding.ofm_envelope_facility_pf = new Microsoft.Xrm.Sdk.Money(parentfees);
        //_funding.ofm_envelope_facility = new Microsoft.Xrm.Sdk.Money(baseAmount);
    }

}

public abstract class CompositeFundingCommand : IFundingCommand
{
    private readonly IFundingAmountsRepository _repository;
    private readonly Funding _funding;
    private List<IFundingCommand> _commands = new List<IFundingCommand>();

    public bool Success { get => _commands.All(cmd => cmd.Success); set => _commands.ForEach(cmd => cmd.Success = value); }

    public CompositeFundingCommand(IFundingAmountsRepository repo, Funding funding)
    {
        _repository = repo;
        _funding = funding;
    }

    public void AddCommand(FundingAmountCommand command)
    {
        if (_commands.Contains(command)) return;

        _commands.Add(command);
    }

    public bool CanExecute()
    {
        return _commands.All(_cmd => _cmd.CanExecute());
    }

    public void Execute()
    {
        if (CanExecute())
        {
            if (_funding is not null)
            {
                _commands.ForEach(cmd => cmd.Execute());
            }
            else { } //log the filenumber issue here 
        }
    }

    public void LogCommand()
    {
        _commands.ForEach(cmd => cmd.LogCommand());
    }
}

public class CalculateNonHRFundingAmountsCommand : CompositeFundingCommand
{
    public CalculateNonHRFundingAmountsCommand(IFundingAmountsRepository repository, Funding funding) : base(repository, funding)
    {
        AddCommand(new CalculateProgrammingAmountCommand(repository, funding));
        AddCommand(new CalculateAdministrativeAmountCommand(repository, funding));
        AddCommand(new CalculateOperationalAmountCommand(repository, funding));
        AddCommand(new CalculateFacilityAmountCommand(repository, funding));
    }
}

public abstract class FundingAmountCommand : IFundingCommand
{
    decimal Rate { get; set; }
    /// <summary>
    /// Total Spaces: sum of all operational spaces of all licence types from all licences that a facility is granted (Non-HR ONLY)
    /// </summary>
    public abstract int TotalSpaces { get; }
    public bool Success { get; set; } = false;

    public abstract bool CanExecute();
    public abstract void Execute();
    public abstract void LogCommand();
}

public class CalculateProgrammingAmountCommand : FundingAmountCommand
{
    private readonly IFundingAmountsRepository _fundingRepository;
    private readonly Funding _funding;
    private readonly NonHRFundingRateServiceBase _fundingRateservice;

    public override int TotalSpaces
    {
        get
        {
            //return _funding.LicenceDetails.Sum(service => service.ofm_operational_spaces!.Value);
            return 0;
        }
    }

    public CalculateProgrammingAmountCommand(IFundingAmountsRepository fundingRepository, Funding funding)
    {
        _fundingRepository = fundingRepository;
        _funding = funding;
        var factory = new ProgrammingRateFactory(TotalSpaces);
        _fundingRateservice = factory.CreateFundingRateService();
    }

    public override string ToString() => GetType().Name;

    public override bool CanExecute()
    {
        //if (!_repository.HasFundingNumber() || _funding is null)
        //{
        //    return false;
        //}

        // Add a Check if the agreement has been generated previously and can be skipped etc.

        return true;
    }

    public override void Execute()
    {
        if (_funding == null)
        {
            Success = false;
            return;
        }

        var scheduleAmount = _fundingRateservice.GetScheduleAmount();

        // Update the amount on the funding record
        _fundingRepository.CalculateProgrammingFundingAmounts(_funding, scheduleAmount);

        Success = true;
    }

    public override void LogCommand()
    {
        Debugger.Log(1, null, $"log me {ToString()}");
    }
}

public class CalculateAdministrativeAmountCommand : FundingAmountCommand
{
    private readonly IFundingAmountsRepository _repository;
    private readonly Funding _funding;
    private readonly NonHRFundingRateServiceBase _fundingRateservice;
    public override int TotalSpaces
    {
        get
        {
            //return _funding.LicenceDetails.Sum(service => service.ofm_operational_spaces!.Value);
            return 0;
        }
    }

    public CalculateAdministrativeAmountCommand(IFundingAmountsRepository repo, Funding funding)
    {
        _repository = repo;
        _funding = funding;
        var factory = new ProgrammingRateFactory(TotalSpaces);
        _fundingRateservice = factory.CreateFundingRateService();
    }

    public override string ToString() => GetType().Name;

    public override bool CanExecute()
    {
        //if (!_repository.HasFundingNumber() || _funding is null)
        //{
        //    return false;
        //}

        // Add a Check if the agreement has been generated previously and can be skipped etc.

        return true;
    }

    public override void Execute()
    {
        if (_funding == null)
        {
            Success = false;
            return;
        }

        var rate = _fundingRateservice.GetScheduleAmount();
        // Save rate to funding record

        //_repository.CalculateFundingAmounts(_funding);

        Success = true;
    }

    public override void LogCommand()
    {
        Debugger.Log(1, null, $"log me {ToString()}");
    }
}

public class CalculateOperationalAmountCommand : FundingAmountCommand
{
    private readonly IFundingAmountsRepository _fundingRepository;
    private readonly Funding _funding;
    private readonly NonHRFundingRateServiceBase _fundingRateservice;

    public override int TotalSpaces
    {
        get
        {
            //return _funding.LicenceDetails.Sum(service => service.ofm_operational_spaces!.Value);
            return 0;
        }
    }

    public CalculateOperationalAmountCommand(IFundingAmountsRepository fundingRepository, Funding funding)
    {
        _fundingRepository = fundingRepository;
        _funding = funding;
        var factory = new ProgrammingRateFactory(TotalSpaces);
        _fundingRateservice = factory.CreateFundingRateService();
    }

    public override string ToString() => GetType().Name;

    public override bool CanExecute()
    {
        //if (!_repository.HasFundingNumber() || _funding is null)
        //{
        //    return false;
        //}

        // Add a Check if the agreement has been generated previously and can be skipped etc.

        return true;
    }

    public override void Execute()
    {
        if (_funding == null)
        {
            Success = false;
            return;
        }

        var scheduleAmount = _fundingRateservice.GetScheduleAmount();

        // Update the amount on the funding record
        _fundingRepository.CalculateProgrammingFundingAmounts(_funding, scheduleAmount);

        Success = true;
    }

    public override void LogCommand()
    {
        Debugger.Log(1, null, $"log me {ToString()}");
    }
}

public class CalculateFacilityAmountCommand : FundingAmountCommand
{
    private readonly IFundingAmountsRepository _repository;
    private readonly Funding _funding;
    private readonly NonHRFundingRateServiceBase _fundingRateservice;
    public override int TotalSpaces
    {
        get
        {
            //return _funding.LicenceDetails.Sum(service => service.ofm_operational_spaces!.Value);
            return 0;
        }
    }

    public CalculateFacilityAmountCommand(IFundingAmountsRepository repository, Funding funding)
    {
        _repository = repository;
        _funding = funding;
        var factory = new ProgrammingRateFactory(TotalSpaces);
        _fundingRateservice = factory.CreateFundingRateService();
    }

    public override string ToString() => GetType().Name;

    public override bool CanExecute()
    {
        //if (!_repository.HasFundingNumber() || _funding is null)
        //{
        //    return false;
        //}

        // Add a Check if the agreement has been generated previously and can be skipped etc.

        return true;
    }

    public override void Execute()
    {
        if (_funding == null)
        {
            Success = false;
            return;
        }

        var rate = _fundingRateservice.GetScheduleAmount();
        // Save rate to funding record

        //_repository.CalculateFundingAmounts(_funding);

        Success = true;
    }

    public override void LogCommand()
    {
        Debugger.Log(1, null, $"log me {ToString()}");
    }
}