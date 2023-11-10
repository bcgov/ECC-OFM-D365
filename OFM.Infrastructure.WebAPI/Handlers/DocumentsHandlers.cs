using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using OFM.Infrastructure.WebAPI.Services.Documents;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

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

        if (!response.IsSuccessStatusCode)
        {
            return TypedResults.Problem($"Failed to Retrieve record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<JsonObject>();
        return TypedResults.Ok(result);
    }

    public static async Task<Results<ProblemHttpResult, BadRequest<string>, Ok<string>>> PostAsync(
        IOptions<AppSettings> appSettings,
        ID365DocumentService documentService,
        ILoggerFactory loggerFactory,
        IFormFileCollection documents,
       [FromForm] string metadata = """
        [
        {"filename": "f1.docx","subject": "subject 1","notetext": "note 1","entity_name_set":"ofm_applications","regarding":"00000000-0000-0000-0000-000000000000"},
        {"filename": "f2.pdf","subject": "subject 2","notetext": "note 2","entity_name_set":"ofm_assistance_request","regarding":"00000000-0000-0000-0000-000000000000"}]
        """)
    {
        if (documents is null || metadata == null) return TypedResults.BadRequest("Invalid Query.");

        foreach (var document in documents!)
        {
            var fn = document.FileName;
        }

        JsonObject jsonDom = JsonSerializer.Deserialize<JsonObject>(metadata?.ToString(), CommonInfo.s_readOptions!);

        // Validate empty request
        if (jsonDom.Count == 0) return TypedResults.BadRequest("Invalid request.");

        // Validate file size limit
        if (JsonSizeCalculator.Estimate(jsonDom, true) > appSettings.Value.MaxFileSize)
        {
            return TypedResults.Problem("The file size exceeds the limit allowed.", statusCode: StatusCodes.Status500InternalServerError);
        };

        //Validate file types
        jsonDom.TryGetPropertyValue("filename", out JsonNode? fileNameNode);
        string fileName = fileNameNode?.GetValue<string>() ?? throw new InvalidEnumArgumentException("The filename is missing.");
        string[] fileNames = fileName.Split('.');
        string fileExt = fileNames[^1].ToLower();
        string[] acceptedFileFormats = appSettings.Value.FileFommats.Split(",").Select(ext => ext.Trim()).ToArray();
        if (Array.IndexOf(acceptedFileFormats, fileExt) == -1) return TypedResults.BadRequest(appSettings.Value.FileFormatErrorMessage);

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