namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Result returned by an inventory grain reservation attempt.
/// </summary>
/// <param name="Reserved">Indicates whether the inventory was reserved.</param>
/// <param name="Reason">The reason the reservation failed, when applicable.</param>
/// <param name="AvailableQuantity">The available quantity after the reservation attempt.</param>
[GenerateSerializer]
[Alias("Ordering.Grains.Contracts.InventoryReservationResult")]
public sealed record InventoryReservationResult(
    [property: Id(0)] bool Reserved,
    [property: Id(1)] string? Reason,
    [property: Id(2)] int AvailableQuantity);
