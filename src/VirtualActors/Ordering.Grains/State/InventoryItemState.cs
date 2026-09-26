namespace Ordering.Grains.State;

using Orleans;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents the persisted state of one inventory item grain.
/// </summary>
/// <remarks>
/// Orleans serialization member identifiers are part of the persisted-state
/// contract. Existing <see cref="IdAttribute"/> values must not be reused for
/// different members.
/// </remarks>
[GenerateSerializer]
[Alias("Ordering.Grains.State.InventoryItemState")]
public sealed class InventoryItemState {
    /// <summary>
    /// Gets or sets the quantity currently available for new reservations.
    /// </summary>
    /// <value>The current unreserved inventory quantity.</value>
    [Id(0)]
    public int AvailableQuantity { get; set; }

    /// <summary>
    /// Gets or sets active inventory reservations by reservation identifier.
    /// </summary>
    /// <value>
    /// A mutable dictionary whose keys are reservation identifiers and whose
    /// values are reserved quantities.
    /// </value>
    /// <remarks>
    /// The concrete mutable collection is required because the grain updates
    /// reservation state before persisting it.
    /// </remarks>
    [Id(1)]
    [SuppressMessage(
        "Design",
        "MA0016:Prefer using collection abstraction instead of implementation",
        Justification = "Persistent grain state requires a concrete mutable collection.")]
    public Dictionary<Guid, int> Reservations { get; set; } = [];
}
