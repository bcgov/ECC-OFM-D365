using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Handlers.D365;

public static class ProcessesHandlers
{
    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<ProcessResult>>> RunProcessById(
        ID365AppUserService appUserService,
        ID365WebApiService d365WebApiService,
        ID365ProcessService processService,
        ID365BackgroundProcessHandler bgprocessHandler,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        Int16 processId,
        [FromBody] dynamic jsonBody
        )
    {
        ILogger logger = loggerFactory.CreateLogger(LogCategory.Process);
        using (logger.BeginScope("ScopeProcess: ProcessId {processId}", processId))
        {
            #region Validation

            if (jsonBody is null) return TypedResults.BadRequest("Invalid Query.");

            ProcessParameter? parameters = JsonSerializer.Deserialize<ProcessParameter>(jsonBody?.ToString(), CommonInfo.s_readOptions!);

            if (parameters is null) return TypedResults.BadRequest("Invalid request.");
            if (string.IsNullOrEmpty(parameters!.TriggeredBy)) { return TypedResults.BadRequest("The TriggeredBy is missing."); }

            #endregion

            logger.LogInformation(CustomLogEvents.Process, "Batch process started by user {TriggeredBy}", parameters.TriggeredBy);

            bgprocessHandler.Execute(async process =>
            {
                await process.RunProcessByIdAsync(processId, parameters);
            });

            //logger.LogInformation(CustomLogEvents.Process, "Process completed in {totalElapsedTime} minutes with the result {result}", timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, result);

            return TypedResults.Ok(ProcessResult.Completed(processId));
        }
    }
}