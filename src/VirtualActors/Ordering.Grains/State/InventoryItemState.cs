namespace Ordering.Grains.State;

using Orleans;

/// <summary>
/// Represents the persisted state of one inventory item grain.
/// </summary>
[GenerateSerializer]
[Alias("Ordering.Grains.State.InventoryItemState")]
public sealed class InventoryItemState {
    /// <summary>
    /// Gets or sets the currently available quantity.
    /// </summary>
    [Id(0)]
    public int AvailableQuantity { get; set; }

    /// <summary>
    /// Gets or sets the active inventory reservations by reservation identifier.
    /// </summary>
    [Id(1)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "MA0016:Prefer using collection abstraction instead of implementation",
        Justification = "Persistent grain state requires a concrete mutable collection.")]
    public Dictionary<Guid, int> Reservations { get; set; } = [];
}
