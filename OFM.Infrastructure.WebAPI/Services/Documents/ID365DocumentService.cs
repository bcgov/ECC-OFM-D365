using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.Processes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public interface ID365DocumentService 
{
    Task<HttpResponseMessage> GetAsync(string documentId);
    Task<ProcessResult> UploadAsync(IFormFileCollection files, IEnumerable<FileMapping> fileMappings);
    Task<HttpResponseMessage> RemoveAsync(string documentId);
}