using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
