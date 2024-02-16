
using ECC.Core.DataContext;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings.NonHR;

public class ProgrammingEvaluator
{
    private readonly IProgrammingRepository _programmingRepository;

    public ProgrammingEvaluator(IProgrammingRepository programmingRepository)
    {
        _programmingRepository = programmingRepository;
    }

    public Task<decimal> EvaluateAsync(ofm_licence_detail coreService)
    {
        var result = coreService.ofm_licence_spaces switch
        {
            >= 0 and <= 20 => 932M,
            >= 21 and <= 30 => 788M,
            >= 31 and <= 50 => 713M,
            >= 51 and <= 374 => 681M,
            _ => 0M
        };

        //var scheduleResult = ProgrammingScheduleData.First(schedule =>
        //{
        //    var valueRange = new Range<int>(schedule.Item2, schedule.Item3);
        //    valueRange.WithinRange(licenceSpaces);

        //    return valueRange.WithinRange(licenceSpaces);
        //});

        _programmingRepository.EvaluateAsync(coreService);

        return Task.FromResult(result);
    }
}