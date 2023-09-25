using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OFM.Infrastructure.WebAPI.Handlers;

    public static class EnvironmentHandlers
    {
        public static Results<ProblemHttpResult, Ok<JsonObject>> Get(
            IOptionsMonitor<D365AuthSettings> options)
        {
             var _authConfig = options.CurrentValue;
            _authConfig.AZAppUsers = new List<AZAppUser>();

            string jsonContent = JsonSerializer.Serialize<D365AuthSettings>(_authConfig, D365AuthSettingsSerializationContext.Default.D365AuthSettings);
            var jsonDom = JsonSerializer.Deserialize<JsonObject>(jsonContent, new JsonSerializerOptions(JsonSerializerDefaults.Web)!);
            jsonDom?.Add("system Date (PST)", DateTime.Now);

            return TypedResults.Ok(jsonDom);
        }
    }
