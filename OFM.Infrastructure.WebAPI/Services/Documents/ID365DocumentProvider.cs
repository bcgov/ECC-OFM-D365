using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public interface ID365DocumentProvider
{
    string EntityNameSet { get; } //e.g. annotations, ccof_application_facility_documents
    Task<string> PrepareDocumentBodyAsync(JsonObject jsonData, ID365AppUserService appUserService, ID365WebApiService d365WebApiService);
}