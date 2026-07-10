// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Configuration;
using Open.IdentityServer.Models;
using Open.IdentityServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Open.IdentityServer.Hosting;

/// <summary>
/// Background service that periodically removes expired server-side sessions, and
/// optionally triggers back-channel logout notifications for the removed sessions.
/// </summary>
public class SessionCleanupHost : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IdentityServerOptions _options;
    private readonly ILogger<SessionCleanupHost> _logger;

    private TimeSpan CleanupInterval => _options.ServerSideSessions.RemoveExpiredSessionsFrequency;

    private CancellationTokenSource _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCleanupHost"/> class.
    /// </summary>
    /// <param name="serviceProvider">The application service provider used to create scopes for resolving the session stores.</param>
    /// <param name="options">The IdentityServer options.</param>
    /// <param name="logger">The logger.</param>
    public SessionCleanupHost(IServiceProvider serviceProvider, IdentityServerOptions options, ILogger<SessionCleanupHost> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// Starts the session cleanup polling.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.ServerSideSessions.RemoveExpiredSessions)
        {
            if (_source != null) throw new InvalidOperationException("Already started. Call Stop first.");

            _logger.LogDebug("Starting server-side session removal");

            _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _ = StartInternalAsync(_source.Token);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the session cleanup polling.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_options.ServerSideSessions.RemoveExpiredSessions)
        {
            if (_source == null) throw new InvalidOperationException("Not started. Call Start first.");

            _logger.LogDebug("Stopping server-side session removal");

            _source.Cancel();
            _source = null;
        }

        return Task.CompletedTask;
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        if (_options.ServerSideSessions.FuzzExpiredSessionRemovalStart)
        {
            // random delay reduces contention when several nodes start at once
            var delay = TimeSpan.FromSeconds(RandomNumberGenerator.GetInt32((int)Math.Max(1, CleanupInterval.TotalSeconds)));

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("TaskCanceledException. Exiting.");
                return;
            }
        }

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("CancellationRequested. Exiting.");
                break;
            }

            try
            {
                await Task.Delay(CleanupInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("TaskCanceledException. Exiting.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("Task.Delay exception: {0}. Exiting.", ex.Message);
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("CancellationRequested. Exiting.");
                break;
            }

            await RemoveExpiredSessionsAsync(cancellationToken);
        }
    }

    internal async Task RemoveExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using (var serviceScope = _serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var ticketStore = serviceScope.ServiceProvider.GetRequiredService<IServerSideTicketStore>();

                var sessions = await ticketStore.GetAndRemoveExpiredSessionsAsync(
                    _options.ServerSideSessions.RemoveExpiredSessionsBatchSize,
                    cancellationToken);

                if (sessions.Count > 0)
                {
                    _logger.LogDebug("Removed {count} expired server-side sessions", sessions.Count);
                }

                if (_options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout)
                {
                    var backChannelLogoutService = serviceScope.ServiceProvider.GetRequiredService<IBackChannelLogoutService>();

                    foreach (var session in sessions)
                    {
                        if (session.ClientIds.Count == 0) continue;

                        _logger.LogDebug("Sending back-channel logout notifications for expired session of subject {subjectId} and session id {sessionId}", session.SubjectId, session.SessionId);

                        await backChannelLogoutService.SendLogoutNotificationsAsync(new LogoutNotificationContext
                        {
                            SubjectId = session.SubjectId,
                            SessionId = session.SessionId,
                            ClientIds = session.ClientIds
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("OperationCanceledException removing expired sessions. Exiting.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception removing expired sessions: {exception}", ex.Message);
        }
    }
}
