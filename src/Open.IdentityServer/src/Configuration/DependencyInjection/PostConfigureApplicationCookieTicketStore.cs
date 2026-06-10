// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Hosting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Open.IdentityServer.Configuration;

/// <summary>
/// Configures the cookie authentication handler used by IdentityServer to keep its
/// authentication tickets in the server-side session store rather than in the cookie itself.
/// </summary>
internal class PostConfigureApplicationCookieTicketStore : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _scheme;

    public PostConfigureApplicationCookieTicketStore(
        IHttpContextAccessor httpContextAccessor,
        IdentityServerOptions identityServerOptions,
        IOptions<Microsoft.AspNetCore.Authentication.AuthenticationOptions> authOptions)
    {
        _httpContextAccessor = httpContextAccessor;

        _scheme = identityServerOptions.Authentication.CookieAuthenticationScheme ??
                  authOptions.Value.DefaultAuthenticateScheme ??
                  authOptions.Value.DefaultScheme;
    }

    public void PostConfigure(string name, CookieAuthenticationOptions options)
    {
        if (name == _scheme)
        {
            options.SessionStore = new TicketStoreShim(_httpContextAccessor);
        }
    }
}
