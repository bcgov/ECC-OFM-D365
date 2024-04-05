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
    public static Results<BadRequest<string>, ProblemHttpResult, Ok<JsonObject>> RunProcessById(
        ID365BackgroundProcessHandler bgprocessHandler,
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


            ProcessParameter? parameters = JsonSerializer.Deserialize<ProcessParameter>(jsonBody?.ToString());

            if (parameters is null) return TypedResults.BadRequest("Invalid request.");
            if (string.IsNullOrEmpty(parameters!.TriggeredBy)) { return TypedResults.BadRequest("The TriggeredBy is required."); }

            #endregion

            logger.LogInformation(CustomLogEvent.Process, "Batch process started by user {TriggeredBy}", parameters.TriggeredBy);

            bgprocessHandler.Execute(async process =>
            {
                await process.RunProcessByIdAsync(processId, parameters);
            });

            return TypedResults.Ok(ProcessResult.Completed(processId).SimpleProcessResult);
        }
    }
}