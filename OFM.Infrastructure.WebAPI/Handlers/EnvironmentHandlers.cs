using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Models;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        _authConfig.AZAppUsers = [];

        Assembly execassembly = Assembly.GetExecutingAssembly();
        var creationtime = new FileInfo(execassembly.Location).CreationTimeUtc;

        TimeZoneInfo timeZone = TimeZoneInfo.Local;
        bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isWindows)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        }
        else
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver");
        }

        string jsonContent = JsonSerializer.Serialize<D365AuthSettings>(_authConfig, D365AuthSettingsSerializationContext.Default.D365AuthSettings);
        var jsonObject = JsonSerializer.Deserialize<JsonObject>(jsonContent, new JsonSerializerOptions(JsonSerializerDefaults.Web)!);
        jsonObject?.Add("systemDate(UTC)", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));
        jsonObject?.Add("systemDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
        //jsonObject?.Add("buildDate(UTC)", GetBuildDate(Assembly.GetExecutingAssembly()));
        jsonObject?.Add("buildDate(UTC)", creationtime);
        jsonObject?.Add("TimeZoneInfo.Local", TimeZoneInfo.Local.DisplayName);
        jsonObject?.Add("TimeZoneById", timeZone.DisplayName);
        //jsonObject?.Add("GetIanaTimeZoneId", GetIanaTimeZoneId(TimeZoneInfo.Local));
        jsonObject?.Add("isWindows", isWindows);
      
        jsonObject?.Remove("azAppUsers");

        return TypedResults.Ok(jsonObject);
    }

    static string GetIanaTimeZoneId(TimeZoneInfo tzi)
    {
        if (tzi.HasIanaId)
            return tzi.Id;  // no conversion necessary

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tzi.Id, out string ianaId))
            return ianaId;  // use the converted ID

        throw new TimeZoneNotFoundException($"No IANA time zone found for {tzi.Id}.");
    }

    //public static datetime getlinkertime(this assembly assembly, timezoneinfo target = null)
    //{
    //    var filepath = assembly.location;
    //    const int c_peheaderoffset = 60;
    //    const int c_linkertimestampoffset = 8;
    //    var buffer = new byte[2048];
    //    using (var stream = new filestream(filepath, filemode.open, fileaccess.read))
    //        stream.read(buffer, 0, 2048);

    //    var offset = bitconverter.toint32(buffer, c_peheaderoffset);
    //    var secondssince1970 = bitconverter.toint32(buffer, offset + c_linkertimestampoffset);
    //    var epoch = new datetime(1970, 1, 1, 0, 0, 0, datetimekind.utc);

    //    var linktimeutc = epoch.addseconds(secondssince1970);

    //    var tz = target ?? timezoneinfo.local;
    //    var localtime = timezoneinfo.converttimefromutc(linktimeutc, tz);

    //    return localtime;
    //}

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
                value = value[(index + BuildVersionMetadataPrefix.Length)..];
                if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }
        }

        return default;
    }
}
