using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HUB.Chat.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

namespace HUB.Chat.Infrastructure.Directory;

/// <summary>
/// Answers "does the caller hold permission X in workspace Y" by asking dashboard-gateway for the caller's
/// own memberships, relaying the caller's bearer token.
/// </summary>
/// <remarks>
/// <para>
/// Token relay rather than a service token: the gateway's <c>/me/memberships</c> already scopes the answer
/// to whoever the token belongs to, so chat-service can only ever learn the caller's privileges — never
/// another user's. The gateway caches memberships in Redis and evicts them on DASHBOARD's
/// <c>MemberDirectoryChangedEvent</c>, so this costs one cheap hop.
/// </para>
/// <para>Fails closed: no token, gateway down, timeout or bad payload all answer "not granted".</para>
/// </remarks>
/// <param name="http">Typed client pointed at dashboard-gateway.</param>
/// <param name="httpContext">Access to the current request, for the caller's Authorization header.</param>
/// <param name="logger">Logger for upstream failures, which surface to users as 403s.</param>
public sealed class DashboardGatewayWorkspacePermissions(
    HttpClient http,
    IHttpContextAccessor httpContext,
    ILogger<DashboardGatewayWorkspacePermissions> logger) : IWorkspacePermissions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<bool> CallerHasPermissionAsync(Guid workspaceId, string permission, CancellationToken ct)
    {
        var authorization = httpContext.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
            return false; // not in a user request — nothing to ask on behalf of

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/directory/me/memberships");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.NotFound)
                return false; // user unknown to DASHBOARD — no memberships, no permissions

            response.EnsureSuccessStatusCode();
            var memberships = await response.Content.ReadFromJsonAsync<MembershipsResponse>(JsonOptions, ct);

            return memberships?.Repositories?.Any(r =>
                r.RepositoryId == workspaceId && (r.Permissions?.Contains(permission) ?? false)) ?? false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ExecutionRejectedException or JsonException)
        {
            logger.LogWarning(ex,
                "Workspace permission check failed for workspace {WorkspaceId}, permission {Permission}; denying.",
                workspaceId, permission);
            return false;
        }
    }

    /// <summary>Configures the typed client against dashboard-gateway.</summary>
    /// <param name="client">The client to configure.</param>
    /// <param name="baseUrl">dashboard-gateway base URL.</param>
    public static void Configure(HttpClient client, string baseUrl) =>
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

    /// <summary>Tunes the standard resilience handler: a user is waiting on an add/remove member click.</summary>
    /// <param name="options">The standard resilience options to adjust.</param>
    public static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout          = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout     = TimeSpan.FromSeconds(5);
        options.Retry.MaxRetryAttempts          = 1;
        options.Retry.Delay                     = TimeSpan.FromMilliseconds(200);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30); // must stay >= 2x AttemptTimeout
    }

    /// <summary>The slice of dashboard-gateway's memberships payload this check needs.</summary>
    /// <param name="Repositories">The caller's repository (= workspace) memberships.</param>
    private sealed record MembershipsResponse(IReadOnlyList<RepositoryEntry>? Repositories);

    /// <summary>One repository membership.</summary>
    /// <param name="RepositoryId">Repository (= workspace) id.</param>
    /// <param name="Permissions">Permission names granted in it.</param>
    private sealed record RepositoryEntry(Guid RepositoryId, IReadOnlyList<string>? Permissions);
}
