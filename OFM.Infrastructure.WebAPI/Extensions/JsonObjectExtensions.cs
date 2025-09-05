using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static OFM.Infrastructure.WebAPI.Models.BCRegistrySearchResult;

namespace OFM.Infrastructure.WebAPI.Extensions;
public static class JsonObjectExtensions
{

    public static T? GetPropertyValue<T>(this JsonObject obj, string propertyName)
    {
        if (obj == null) throw new ArgumentNullException("obj");
        if (propertyName == null) throw new ArgumentNullException("propertyName");
        if (obj[propertyName] is JsonObject && typeof(T) == typeof(JsonObject))
            return (T)Convert.ChangeType(obj[propertyName], typeof(T)); ;
        if (obj[propertyName] is JsonArray && typeof(T) == typeof(JsonArray))
            return (T)Convert.ChangeType(obj[propertyName], typeof(T));
        if (obj[propertyName] != null && obj.ContainsKey(propertyName))
            return obj[propertyName].GetValue<T>();
        return default(T);


    }

    public static string? GetFormattedValue(this JsonObject obj, string propertyName)
    {
        var v = $"{propertyName}@OData.Community.Display.V1.FormattedValue";
        if (obj == null) throw new ArgumentNullException("obj");
        if (v == null) throw new ArgumentNullException("propertyName");
        if (obj.ContainsKey(v) && obj[v] != null)
            return obj[v].GetValue<string>();
        return null;
    }

    public static JsonObject MergeJsonObjects(this JsonObject json1, IEnumerable<JsonObject> jsonObjects)
    {
        JsonObject result = new JsonObject(json1);
        foreach (var json in jsonObjects)
        {
            foreach (var property in json)
            {
                if (!result.ContainsKey(property.Key))
                {
                    result.Add(property.Key, property.Value?.DeepClone()); // Deep clone to avoid reference issues
                }
            }
        }
        return result;
    }
}