using Microsoft.AspNetCore.Http.HttpResults;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes;

namespace OFM.Infrastructure.WebAPI.Handlers.D365;

public static class ProcessesHandlers
{
    public static async Task<Results<ProblemHttpResult, Ok<ProcessResult>>> RunProcessById(
        ID365AppUserService appUserService,
        ID365WebApiService d365WebApiService,
        ID365ProcessService processService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        int processId,
        string initiator,
        Guid? callerObjectId)
    {
        ILogger logger = loggerFactory.CreateLogger(LogCategory.Process);
        using (logger.BeginScope("ScopeProcess: ProcessId {processId}",processId))
        {
            logger.LogInformation(CustomLogEvents.Process, "Batch process started by user {initiator}", initiator);

            var startTime = timeProvider.GetTimestamp();

            ProcessResult result = await processService.RunProcessByIdAsync(processId);
            
            var endTime = timeProvider.GetTimestamp();

            logger.LogInformation(CustomLogEvents.Process, "Process completed in {totalElapsedTime} minutes with the result {result}", timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, result);

            return TypedResults.Ok(result);
        }
    }
}