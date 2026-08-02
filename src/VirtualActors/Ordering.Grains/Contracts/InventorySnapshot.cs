namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Inventory state returned by an inventory grain.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="AvailableQuantity">The currently available quantity.</param>
[GenerateSerializer]
[Alias("Ordering.Grains.Contracts.InventorySnapshot")]
public sealed record InventorySnapshot(
    [property: Id(0)] string ProductId,
    [property: Id(1)] int AvailableQuantity);
