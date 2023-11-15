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
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Reflection.Metadata;
using System.Xml;
using System;

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

    //public static async Task<Results<ProblemHttpResult, BadRequest<string>, Ok<string>>> PostAsync(
    //    IOptions<AppSettings> appSettings,
    //    ID365DocumentService documentService,
    //    ILoggerFactory loggerFactory,
    //    IFormFileCollection documents,
    //   [FromForm] string metadata = """
    //    [
    //    {"filename": "f1.docx","subject": "subject 1","ofm_description": "note 1","entity_name_set":"ofm_assistance_request","regarding":"07b69ea1-ff7f-ee11-8179-000d3a09d499"},
    //    {"filename": "f2.pdf","subject": "subject 2","notetext": "note 2","entity_name_set":"ofm_assistance_request","regarding":"00000000-0000-0000-0000-000000000000"}]
    //    """)
    //{
    //    if (documents is null || metadata == null) return TypedResults.BadRequest("Invalid Query.");

    //    foreach (var document in documents!)
    //    {
    //        var fn = document.FileName;


    //        JsonObject jsonDom = JsonSerializer.Deserialize<JsonObject>(metadata?.ToString(), CommonInfo.s_readOptions!);

    //        // Validate empty request
    //        //if (jsonDom.Count == 0) return TypedResults.BadRequest("Invalid request.");

    //        //// Validate file size limit
    //        //if (JsonSizeCalculator.Estimate(jsonDom, true) > appSettings.Value.MaxFileSize)
    //        //{
    //        //    return TypedResults.Problem("The file size exceeds the limit allowed.", statusCode: StatusCodes.Status500InternalServerError);
    //        //};

    //        ////Validate file types
    //        //jsonDom.TryGetPropertyValue("filename", out JsonNode? fileNameNode);
    //        //string fileName = fileNameNode?.GetValue<string>() ?? throw new InvalidEnumArgumentException("The filename is missing.");
    //        //string[] fileNames = fileName.Split('.');
    //        //string fileExt = fileNames[^1].ToLower();
    //        //string[] acceptedFileFormats = appSettings.Value.FileFommats.Split(",").Select(ext => ext.Trim()).ToArray();
    //        //if (Array.IndexOf(acceptedFileFormats, fileExt) == -1) return TypedResults.BadRequest(appSettings.Value.FileFormatErrorMessage);

    //        if (document.Length > appSettings.Value.MaxFileSize)
    //        {
    //            return TypedResults.Problem("The file size exceeds the limit allowed.", statusCode: StatusCodes.Status500InternalServerError);
    //        };
    //        string filename = document.FileName;
    //        long fileSize = document.Length;
    //        string[] fileNames = filename.Split('.');
    //        string fileExt = fileNames[^1].ToLower();
    //        string[] acceptedFileFormats = appSettings.Value.FileFommats.Split(",").Select(ext => ext.Trim()).ToArray();
    //        if (Array.IndexOf(acceptedFileFormats, fileExt) == -1) return TypedResults.BadRequest(appSettings.Value.FileFormatErrorMessage);

    //        var response = await documentService.UploadAsync(jsonDom);

    //        if (response.IsSuccessStatusCode)
    //        {
    //            return TypedResults.Ok("File Uploaded.");
    //        }
    //        else
    //            return TypedResults.Problem($"Failed to Create record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
    //    }
    //    return TypedResults.Ok("Done");
    //}

    public static async Task<Results<ProblemHttpResult, BadRequest<string>, Ok<string>>> PostAsync(
        IOptions<AppSettings> appSettings,
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILoggerFactory loggerFactory,
        IFormFileCollection documents,
        string statement,
        [FromForm] string jsonBody)
    {
        var logger = loggerFactory.CreateLogger(LogCategory.Operations);
        using (logger.BeginScope("POST"))
        {
            foreach (var document in documents)
            {
                var fileName = document.FileName;
                string fileSize = document.Length.ToString();
                string[] fileNames = fileName.Split('.');
                string fileExt = fileNames[^1].ToLower();
                string[] acceptedFileFormats = appSettings.Value.FileFommats.Split(",").Select(ext => ext.Trim()).ToArray();
                if (Array.IndexOf(acceptedFileFormats, fileExt) == -1) return TypedResults.BadRequest(appSettings.Value.FileFormatErrorMessage);
                var list = JsonSerializer.Deserialize<JsonObject>(jsonBody);
                list.Add("ofm_filename", fileNames[0]);
                list.Add("ofm_sizeoffile", fileSize);
                list.Add("ofm_extension", fileExt);
                dynamic convertedJson = JsonSerializer.Serialize<JsonObject>(list);

                logger.LogDebug(CustomLogEvents.Operations, "Creating record(s) with the statement {statement}", statement);

                HttpResponseMessage response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZPortalAppUser, "ofm_documentses", convertedJson.ToString());

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError(CustomLogEvents.Operations, "Failed to Create a record with the statement {statement}", statement);

                    return TypedResults.Problem($"Failed to Create a record with a reason {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
                }

                var result = response.Content.ReadAsStringAsync().Result;

                logger.LogInformation(CustomLogEvents.Operations, "Created record(s) successfully with the result {result}", result);

                var documentId = JsonSerializer.Deserialize<JsonObject>(result)["ofm_documentsid"];
                //PatchAsync(d365WebApiService, appUserService, "statement=ofm_documentses(" + documentId.ToString() + ")/ofm_file", documents);
            }
            return TypedResults.Ok("");
        }
    }

    public static async Task<Results<ProblemHttpResult, Ok<string>>> PatchAsync(
          ID365WebApiService d365WebApiService, ID365AppUserService appUserService,
          string statement, IFormFileCollection binaryFile)
    {
        byte[] bytes = null;
        string fileName = "";
        foreach (var item in binaryFile)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                item.CopyTo(ms);
                bytes = ms.ToArray();
            }
            fileName = item.FileName;
        }

        HttpResponseMessage response = await d365WebApiService.SendDocumentPatchRequestAsync(appUserService.AZPortalAppUser, statement, bytes, fileName);

        if (response.IsSuccessStatusCode)
            return TypedResults.Ok($"File uploaded.");
        else
            return TypedResults.Problem($"Failed to upload file: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
    }

    //statement=ofm_documentses(<ofm_documentsid>)/ofm_file
    public static async Task<Results<ProblemHttpResult, Ok<string>>> DeleteAsync(
          ID365DocumentService documentService,
          string statement)
    {
        var response = await documentService.RemoveAsync(statement);

        if (response.IsSuccessStatusCode)
            return TypedResults.Ok($"Record was removed.");
        else
            return TypedResults.Problem($"Failed to Delete record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
    }
}