﻿using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Services.AppUsers;

public class D365AppUserService : ID365AppUserService
{
    readonly D365AuthSettings? _authSettings;
    public D365AppUserService(IOptions<D365AuthSettings> authSettings) => _authSettings = authSettings.Value;

    public AZAppUser AZPortalAppUser => GetAZAppUser(CommonInfo.AppUserType.Portal);

    public AZAppUser AZSystemAppUser => GetAZAppUser(CommonInfo.AppUserType.System);

    AZAppUser ID365AppUserService.AZNoticationAppUser => GetAZAppUser(CommonInfo.AppUserType.Notification);

    public AZAppUser GetAZAppUser(string userType)
    {
        return _authSettings?.AZAppUsers.First(u => u.Type == userType) ?? throw new KeyNotFoundException( $"Integration User not found for {userType} - {nameof(D365AppUserService)}");
    }
}