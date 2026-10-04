using System.Net;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Infrastructure.Directory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Common;

/// <summary>
/// Covers <c>DashboardGatewayWorkspacePermissions</c>: that it asks as the caller (token relay), reads the
/// right workspace's permissions, and answers "no" whenever it cannot be sure.
/// </summary>
public sealed class DashboardGatewayWorkspacePermissionsTests
{
    private static readonly Guid Workspace = Guid.NewGuid();
    private const string CallerToken = "Bearer caller-jwt";

    /// <summary>Records the last request and answers with a scripted response.</summary>
    private sealed class Upstream(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(respond());
        }
    }

    private static HttpResponseMessage Memberships(Guid repositoryId, params string[] permissions) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            $$"""{"userId":"{{Guid.NewGuid()}}","repositories":[{"repositoryId":"{{repositoryId}}","permissions":[{{string.Join(',', permissions.Select(p => $"\"{p}\""))}}]}]}""",
            System.Text.Encoding.UTF8, "application/json"),
    };

    private static (DashboardGatewayWorkspacePermissions Sut, Upstream Upstream) Build(
        Func<HttpResponseMessage> respond, string? authorization = CallerToken)
    {
        var upstream = new Upstream(respond);
        var http     = new HttpClient(upstream);
        DashboardGatewayWorkspacePermissions.Configure(http, "http://dashboard-gateway:8080");

        var context = new DefaultHttpContext();
        if (authorization is not null) context.Request.Headers.Authorization = authorization;

        var sut = new DashboardGatewayWorkspacePermissions(
            http, new HttpContextAccessor { HttpContext = context },
            NullLogger<DashboardGatewayWorkspacePermissions>.Instance);
        return (sut, upstream);
    }

    [Fact]
    public async Task AsksForTheCallersOwnMembershipsWithTheCallersToken()
    {
        var (sut, upstream) = Build(() => Memberships(Workspace, WorkspacePermissionNames.ManageChannels));

        (await sut.CallerHasPermissionAsync(Workspace, WorkspacePermissionNames.ManageChannels, CancellationToken.None))
            .ShouldBeTrue();

        // /me + the caller's own token: chat-service can only learn the caller's privileges, never another user's.
        upstream.Last!.RequestUri!.AbsolutePath.ShouldBe("/api/v1/directory/me/memberships");
        upstream.Last.Headers.Authorization!.ToString().ShouldBe(CallerToken);
    }

    [Fact]
    public async Task APermissionInAnotherWorkspaceDoesNotCount()
    {
        var (sut, _) = Build(() => Memberships(Guid.NewGuid(), WorkspacePermissionNames.ManageChannels));

        (await sut.CallerHasPermissionAsync(Workspace, WorkspacePermissionNames.ManageChannels, CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task AMembershipWithoutThePermissionDoesNotCount()
    {
        var (sut, _) = Build(() => Memberships(Workspace, "ViewBoards"));

        (await sut.CallerHasPermissionAsync(Workspace, WorkspacePermissionNames.ManageChannels, CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task FailsClosedWhenTheGatewayErrors()
    {
        var (sut, _) = Build(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Granting on an outage would hand out member management on a guess.
        (await sut.CallerHasPermissionAsync(Workspace, WorkspacePermissionNames.ManageChannels, CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task FailsClosedWhenTheGatewayIsUnreachable()
    {
        var (sut, _) = Build(() => throw new HttpRequestException("connection refused"));

        (await sut.CallerHasPermissionAsync(Workspace, WorkspacePermissionNames.ManageChannels, CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task WithoutACallerTokenItDoesNotAskAtAll()
    {
        var (sut, upstream) = Build(() => Memberships(Workspace, WorkspacePermissionNames.ManageChannels), authorization: null);

        (await sut.CallerHasPermissionAsync(Workspace, WorkspacePermissionNames.ManageChannels, CancellationToken.None))
            .ShouldBeFalse();
        upstream.Last.ShouldBeNull();
    }
}
