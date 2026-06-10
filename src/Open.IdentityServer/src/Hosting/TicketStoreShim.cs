// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Open.IdentityServer.Hosting;

/// <summary>
/// Shim for the cookie authentication handler's <see cref="ITicketStore"/> that forwards
/// to the <see cref="IServerSideTicketStore"/> resolved from the current request's
/// service provider. This is needed because <see cref="CookieAuthenticationOptions.SessionStore"/>
/// is a singleton, while the ticket store has scoped dependencies (e.g. a database context).
/// </summary>
internal class TicketStoreShim : ITicketStore
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketStoreShim(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private IServerSideTicketStore Inner
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext ??
                              throw new InvalidOperationException("No HttpContext available to resolve the server-side ticket store.");

            return httpContext.RequestServices.GetRequiredService<IServerSideTicketStore>();
        }
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        return Inner.StoreAsync(ticket);
    }

    public Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        return Inner.RetrieveAsync(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        return Inner.RenewAsync(key, ticket);
    }

    public Task RemoveAsync(string key)
    {
        return Inner.RemoveAsync(key);
    }
}
