namespace Ordering.Persistence.Sqlite.Internal.Infrastructure;

/// <summary>
/// Represents one serialized Orleans grain state stored in SQLite.
/// </summary>
internal sealed class GrainStateEntity {
    /// <summary>
    /// Gets or sets the Orleans service identifier.
    /// </summary>
    public required string ServiceId { get; set; }

    /// <summary>
    /// Gets or sets the registered storage provider name.
    /// </summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the persistent state name.
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
    /// Gets or sets the serialized grain state payload.
    /// </summary>
    public required byte[] Payload { get; set; }

    /// <summary>
    /// Gets or sets the optimistic concurrency version.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time of the last modification.
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}
