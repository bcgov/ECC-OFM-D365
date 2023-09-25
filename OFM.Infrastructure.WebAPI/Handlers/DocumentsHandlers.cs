using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using OFM.Infrastructure.WebAPI.Services.Documents;
using Microsoft.Extensions.Options;

namespace OFM.Infrastructure.WebAPI.Handlers;
public static class DocumentsHandlers
{
    static readonly string _entityNameSet = "entity_name_set";

    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<JsonObject>>> GetAsync(
        ID365DocumentService documentService,
        string annotationId)
    {
        if (string.IsNullOrEmpty(annotationId)) return TypedResults.BadRequest("Invalid Query.");

        var response = await documentService.GetAsync(annotationId);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonObject>();
            return TypedResults.Ok(result);
        }
        else
        {
            return TypedResults.Problem($"Failed to Retrieve record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
    }

    public static async Task<Results<ProblemHttpResult, BadRequest<string>, Ok<string>>> PostAsync(
        IOptionsMonitor<AppSettings> appSettings,
        ID365DocumentService documentService,
        [FromBody] dynamic? jsonBody)
    {
        if (string.IsNullOrEmpty(jsonBody)) return TypedResults.BadRequest("Invalid Query.");

        JsonObject jsonDom = JsonSerializer.Deserialize<JsonObject>(jsonBody?.ToString(), CommonInfo.s_readOptions!);

        // Validate empty request
        if (jsonDom.Count == 0) return TypedResults.BadRequest("Invalid request.");

        // Validate file size limit
        if (JsonSizeCalculator.Estimate(jsonDom, true) > appSettings.CurrentValue.MaxFileSize)
        {
            return TypedResults.Problem("The file size exceeds the limit allowed.", statusCode: StatusCodes.Status500InternalServerError);
        };

        //Validate file types
        jsonDom.TryGetPropertyValue("filename", out JsonNode? fileNameNode);
        string fileName = fileNameNode?.GetValue<string>() ?? throw new InvalidEnumArgumentException("The filename is missing.");
        string[] fileNames = fileName.Split('.');
        string fileExt = fileNames[^1].ToLower();
        string[] acceptedFileFormats = appSettings.CurrentValue.FileFommats.Split(",").Select(ext => ext.Trim()).ToArray();
        if (Array.IndexOf(acceptedFileFormats, fileExt) == -1) return TypedResults.BadRequest(appSettings.CurrentValue.FileFormatErrorMessage);
      
        var response = await documentService.UploadAsync(jsonDom);

        if (response.IsSuccessStatusCode)
        {
            return TypedResults.Ok("File Uploaded.");
        }
        else
            return TypedResults.Problem($"Failed to Create record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
    }

    public static async Task<Results<ProblemHttpResult, Ok<string>>> DeleteAsync(
          ID365DocumentService documentService,
          string annotationid)
    {
        var response = await documentService.RemoveAsync(annotationid);

        if (response.IsSuccessStatusCode)
            return TypedResults.Ok($"Record was removed.");
        else
            return TypedResults.Problem($"Failed to Delete record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
    }
}