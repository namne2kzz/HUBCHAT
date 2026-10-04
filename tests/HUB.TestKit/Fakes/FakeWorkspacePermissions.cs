using HUB.Chat.Application.Common.Interfaces;

namespace HUB.TestKit.Fakes;

/// <summary>In-memory <see cref="IWorkspacePermissions"/>: grants exactly what a test configures.</summary>
/// <remarks>
/// <see cref="Calls"/> lets a test prove the permission source was (or was not) consulted — the role check
/// is supposed to short-circuit before paying for a dashboard-gateway hop.
/// </remarks>
public sealed class FakeWorkspacePermissions : IWorkspacePermissions
{
    private readonly HashSet<(Guid WorkspaceId, string Permission)> _granted = [];

    /// <summary>Number of times a permission was asked for.</summary>
    public int Calls { get; private set; }

    /// <summary>Grants the caller a permission in a workspace.</summary>
    /// <param name="workspaceId">Workspace id.</param>
    /// <param name="permission">Permission name.</param>
    /// <returns>This fake, for chaining.</returns>
    public FakeWorkspacePermissions Grant(Guid workspaceId, string permission)
    {
        _granted.Add((workspaceId, permission));
        return this;
    }

    /// <inheritdoc />
    public Task<bool> CallerHasPermissionAsync(Guid workspaceId, string permission, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(_granted.Contains((workspaceId, permission)));
    }
}
