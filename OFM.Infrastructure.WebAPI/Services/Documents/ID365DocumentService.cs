using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public interface ID365DocumentService 
{
    Task<HttpResponseMessage> GetAsync(string annotationId);
    Task<HttpResponseMessage> UploadAsync(JsonObject value);
    Task<HttpResponseMessage> RemoveAsync(string annotationId);
}