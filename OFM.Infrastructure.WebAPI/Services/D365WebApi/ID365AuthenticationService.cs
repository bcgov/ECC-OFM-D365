using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

public interface ID365AuthenticationService
{
    Task<HttpClient> GetHttpClientAsync(D365ServiceType type, AZAppUser spn);
}