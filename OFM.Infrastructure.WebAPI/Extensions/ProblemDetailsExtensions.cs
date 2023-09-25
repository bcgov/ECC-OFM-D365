using Hellang.Middleware.ProblemDetails;
namespace OFM.Infrastructure.WebAPI.Extensions;

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddCustomProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(opts =>
        {
            opts.IncludeExceptionDetails = (ctx, ex) => false;
            opts.OnBeforeWriteDetails = (ctx, dtls) =>
            {
                if (dtls.Status == 500)
                {
                    dtls.Detail = "An error occurred in the custom API. Use the trace id when contacting the support team.";
                }
            };
            opts.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
        });

        return services;
    }
}