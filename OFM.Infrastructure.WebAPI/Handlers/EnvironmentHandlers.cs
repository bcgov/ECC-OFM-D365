using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OFM.Infrastructure.WebAPI.Handlers;

public static class EnvironmentHandlers
{
    /// <summary>
    /// Returns the current environment information including the server timestamp
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public static Results<ProblemHttpResult, Ok<JsonObject>> Get(
        IOptionsSnapshot<D365AuthSettings> options)
    {
        var _authConfig = options.Value;
        _authConfig.AZAppUsers = new List<AZAppUser>();

        string jsonContent = JsonSerializer.Serialize<D365AuthSettings>(_authConfig, D365AuthSettingsSerializationContext.Default.D365AuthSettings);
        var jsonObject = JsonSerializer.Deserialize<JsonObject>(jsonContent, new JsonSerializerOptions(JsonSerializerDefaults.Web)!);
        jsonObject?.Add("systemDate(UTC)", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));
        jsonObject?.Add("systemDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
        jsonObject?.Add("buildDate(UTC)", GetBuildDate(Assembly.GetExecutingAssembly()));
        jsonObject?.Remove("azAppUsers");

        return TypedResults.Ok(jsonObject);
    }

    private static DateTime GetBuildDate(Assembly assembly)
    {
        const string BuildVersionMetadataPrefix = "+build";

        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute?.InformationalVersion != null)
        {
            var value = attribute.InformationalVersion;
            var index = value.IndexOf(BuildVersionMetadataPrefix);
            if (index > 0)
            {
                value = value.Substring(index + BuildVersionMetadataPrefix.Length);
                if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }
        }

        return default;
    }
}
