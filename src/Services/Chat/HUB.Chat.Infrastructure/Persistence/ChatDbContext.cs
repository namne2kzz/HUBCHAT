using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace HUB.Chat.Infrastructure.Persistence;

/// <summary>EF Core context for the chat bounded context (database <c>hub_chat</c>). Hosts the MassTransit outbox tables.</summary>
/// <param name="options">Context options.</param>
public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options), IChatDbContext
{
    /// <inheritdoc />
    public DbSet<Channel> Channels => Set<Channel>();

    /// <inheritdoc />
    public DbSet<Message> Messages => Set<Message>();

    /// <inheritdoc />
    public void DiscardChanges() => ChangeTracker.Clear();

    /// <summary>Saves changes, translating a PostgreSQL unique violation into <see cref="UniqueConstraintViolationException"/>.</summary>
    /// <param name="acceptAllChangesOnSuccess">Whether to accept tracked changes after a successful save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of state entries written.</returns>
    /// <remarks>
    /// Overrides the core overload, so <c>SaveChangesAsync(ct)</c> is covered too. Other providers (SQLite in
    /// unit tests) pass their exception through unchanged — race paths are covered by the PostgreSQL
    /// integration tests.
    /// </remarks>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);

        // Domain ids are client-assigned (Entity base sets a Guid). Tell EF so that adding a child
        // (reaction/attachment/member) to an already-tracked aggregate is INSERTed, not UPDATEd —
        // otherwise the non-default store-generated key makes EF emit an UPDATE affecting 0 rows
        // → DbUpdateConcurrencyException.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType)) continue;
            var id = entityType.FindProperty(nameof(Entity.Id));
            if (id is not null) id.ValueGenerated = ValueGenerated.Never;
        }

        // Transactional outbox — messages published via IPublishEndpoint are stored here and
        // delivered only after SaveChanges commits, giving atomic "save + publish".
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}
