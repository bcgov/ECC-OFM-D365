using Hellang.Middleware.ProblemDetails;
namespace OFM.Infrastructure.WebAPI.Extensions;

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddCustomProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(config =>
        {
            config.IncludeExceptionDetails = (context, exception) => false;
            config.OnBeforeWriteDetails = (context, details) =>
            {
                if (details.Status == 500)
                {
                    details.Detail = "An error occurred in the custom API. Use the trace id when contacting the support team.";
                }
            };
            config.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
        });

        return services;
    }
}