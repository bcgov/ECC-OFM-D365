using Microsoft.OpenApi.Models;
using OFM.Infrastructure.WebAPI.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OFM.Infrastructure.WebAPI.Extensions;

public static class SwaggerApiKeySecurity
{
    public static void AddSwaggerApiKeySecurity(this SwaggerGenOptions opt, IConfiguration config)
    {
        var authSettings = config.GetRequiredSection(nameof(AuthenticationSettings)).Get<AuthenticationSettings>() ?? throw new KeyNotFoundException(nameof(AuthenticationSettings));

        opt.AddSecurityDefinition(authSettings.Schemes.ApiKeyScheme.ApiKeyName, new OpenApiSecurityScheme
        {
            Description = "Custom",
            Type = SecuritySchemeType.ApiKey,
            Name = authSettings.Schemes.ApiKeyScheme.ApiKeyName,
            In = ParameterLocation.Header,
            Scheme = "ApiKeyScheme"
        });

        var key = new OpenApiSecurityScheme()
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = authSettings.Schemes.ApiKeyScheme.ApiKeyName
            },
            In = ParameterLocation.Header
        };

        var requirement = new OpenApiSecurityRequirement { {key, new List<string>() } };
        opt.AddSecurityRequirement(requirement);
    }
}