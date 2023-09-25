using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Services.AppUsers;

public interface ID365AppUserService
{
    AZAppUser GetAZAppUser(string userType);
    AZAppUser AZPortalAppUser { get; }
    AZAppUser AZSystemAppUser { get; }
}