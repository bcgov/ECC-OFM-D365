using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public class D365DocumentService : ID365DocumentService
{
    static readonly string _entityNameSet = "entity_name_set";
    protected readonly ID365WebApiService _d365webapiservice;
    private readonly IEnumerable<ID365DocumentProvider> _documentProviders;
    private readonly ID365AppUserService _appUserService;

    public D365DocumentService(ID365AppUserService appUserService, ID365WebApiService service, IEnumerable<ID365DocumentProvider> documentProviders)
    {
        _d365webapiservice = service;
        _documentProviders = documentProviders;
        _appUserService = appUserService;
    }

    public async Task<HttpResponseMessage> GetAsync(string annotationId)
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
                                      <condition attribute='annotationid' operator='eq' value= '{{annotationId}}' />
                                    </filter>
                                  </entity>
                            </fetch>
                            """;

        var statement = $"annotations?fetchXml=" + WebUtility.UrlEncode(fetchXML);

        return await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZPortalAppUser, statement);
    }
    public async Task<HttpResponseMessage> UploadAsync(JsonObject jsonData)
    {
        if (!jsonData.TryGetPropertyValue(_entityNameSet, out JsonNode? entityNameValue))
        {
            throw new KeyNotFoundException(_entityNameSet);
        }

        ID365DocumentProvider provider = _documentProviders.First(p => p.EntityNameSet == entityNameValue?.GetValue<string>()) ?? throw new NotImplementedException(nameof(ID365DocumentProvider));
        var processingDocument = await provider.PrepareDocumentBodyAsync(jsonData, _appUserService, _d365webapiservice);
        string entitySetName = entityNameValue!.GetValue<string>();

        return await _d365webapiservice.SendCreateRequestAsync(_appUserService.AZPortalAppUser, entitySetName, processingDocument); 
    }
    
    public async Task<HttpResponseMessage> RemoveAsync(string annotationId)
    {
        return await _d365webapiservice.SendDeleteRequestAsync(_appUserService.AZPortalAppUser, $"annotations({annotationId})");
    }
}