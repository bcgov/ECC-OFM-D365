using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes;
using System.Net;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public class D365DocumentService : ID365DocumentService
{
    static readonly string _entityNameSet = "ofm_documents";
    protected readonly ID365WebApiService _d365webapiservice;
    private readonly IEnumerable<ID365DocumentProvider> _documentProviders;
    private readonly ID365AppUserService _appUserService;

    public D365DocumentService(ID365AppUserService appUserService, ID365WebApiService service, IEnumerable<ID365DocumentProvider> documentProviders)
    {
        _d365webapiservice = service;
        _documentProviders = documentProviders;
        _appUserService = appUserService;
    }

    public async Task<HttpResponseMessage> GetAsync(string documentId)
    {
        string fetchXML = $$"""
                            <fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
                                  <entity name='annotation' >
                                    <attribute name='filename' />
                                    <attribute name='filesize' />
                                    <attribute name='notetext' />
                                    <attribute name='subject' />
                                    <attribute name='documentbody' />
                                    <filter>
                                      <condition attribute='annotationid' operator='eq' value= '{{documentId}}' />
                                    </filter>
                                  </entity>
                            </fetch>
                            """;

        var statement = $"annotations?fetchXml=" + WebUtility.UrlEncode(fetchXML);

        return await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZPortalAppUser, statement);
    }

    public async Task<HttpResponseMessage> RemoveAsync(string documentId)
    {
        return await _d365webapiservice.SendDeleteRequestAsync(_appUserService.AZPortalAppUser, $"annotations({documentId})");
    }

    public async Task<ProcessResult> UploadAsync(IFormFileCollection files, IEnumerable<FileMapping> fileMappings)
    {
        //if (files.Count == 0 || fileMappings.Count() == 0 || files.Count != fileMappings.Count())
        //    throw new InvalidEnumArgumentException($"UploadAsync: {nameof(D365DocumentService)}");

        ID365DocumentProvider provider = _documentProviders.First(p => p.EntityNameSet == _entityNameSet);

        Int16 processedCount = 0;
        List<JsonObject> documentsResult = new List<JsonObject>() { };
        List<string> errors = new List<string>() { };

        foreach (var file in files)
        {
            var fileDetail = fileMappings.First(doc => doc.ofm_subject == file.FileName);
            var newDocument = await provider.CreateDocumentAsync(fileDetail, _appUserService, _d365webapiservice);

            if (newDocument is not null && newDocument.ContainsKey("ofm_documentid"))
            {
                documentsResult.Add(newDocument);

                if (file.Length > 0)
                {
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memStream);

                        // Attach the file to the new document record
                        HttpResponseMessage response = await _d365webapiservice.SendDocumentRequestAsync(_appUserService.AZPortalAppUser, _entityNameSet, new Guid(newDocument["ofm_documentid"].ToString()), memStream.ToArray(), file.FileName);

                        if (!response.IsSuccessStatusCode)
                        {
                            //log the error
                            errors.Add(file.FileName);
                            continue;
                        }
                    }
                    processedCount++;
                }
            }
            else
            {
                errors.Add(file.FileName);
            }
        }

        if (errors.Any() && processedCount == 0) { return await Task.FromResult<ProcessResult>(ProcessResult.Failure(errors, processedCount, files.Count)); }

        if (errors.Any() && processedCount < files.Count) { return await Task.FromResult<ProcessResult>(ProcessResult.PartialSuccess(documentsResult, errors, processedCount, files.Count)); }

        return await Task.FromResult<ProcessResult>(ProcessResult.Success(documentsResult, processedCount, files.Count));
    }
}