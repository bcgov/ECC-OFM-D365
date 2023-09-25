using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

public interface ID365TokenService
{
    Task<string> FetchAccessToken(string baseUrl, AZAppUser azSPN);
}
