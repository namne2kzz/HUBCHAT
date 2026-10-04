namespace HUB.Chat.Domain.Common;

/// <summary>Marker for aggregate roots — the only entities a repository/DbSet should load and save directly.</summary>
public abstract class AggregateRoot : Entity;
