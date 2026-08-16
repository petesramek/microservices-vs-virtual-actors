namespace Ordering.Persistence.Sqlite.Internal.Infrastructure;

/// <summary>
/// Represents one serialized Orleans grain state stored in SQLite.
/// </summary>
/// <remarks>
/// The service, provider, state, grain type, and grain identifiers form the
/// entity's composite primary key. <see cref="Version"/> is the provider-managed
/// value exposed to Orleans as the grain state's ETag.
/// </remarks>
internal sealed class GrainStateEntity {
    /// <summary>
    /// Gets or sets the Orleans service identifier.
    /// </summary>
    public required string ServiceId { get; set; }

    /// <summary>
    /// Gets or sets the registered storage-provider name.
    /// </summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the persistent-state name.
    /// </summary>
    public required string StateName { get; set; }

    /// <summary>
    /// Gets or sets the Orleans grain type.
    /// </summary>
    public required string GrainType { get; set; }

    /// <summary>
    /// Gets or sets the Orleans grain identifier.
    /// </summary>
    public required string GrainId { get; set; }

    /// <summary>
    /// Gets or sets the serialized grain-state payload.
    /// </summary>
    /// <remarks>
    /// The payload is opaque to the persistence layer and may contain sensitive
    /// application state. It must not be written to logs.
    /// </remarks>
    public required byte[] Payload { get; set; }

    /// <summary>
    /// Gets or sets the provider-managed version used as the Orleans grain-state
    /// ETag.
    /// </summary>
    /// <remarks>
    /// <see cref="SqliteGrainStorage"/> converts this value to the grain state's
    /// string ETag and increments it after each successful state replacement.
    /// Entity Framework Core also uses the value as an optimistic concurrency
    /// token to detect a row change between reading and saving the entity.
    /// </remarks>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time at which the state was last modified.
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}
