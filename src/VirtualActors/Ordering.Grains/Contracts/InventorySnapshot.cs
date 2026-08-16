namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Represents a point-in-time view of the available inventory for one product.
/// </summary>
/// <param name="ProductId">
/// The stable identifier of the product represented by the snapshot.
/// </param>
/// <param name="AvailableQuantity">
/// The quantity available when the snapshot was created.
/// </param>
/// <remarks>
/// This contract describes inventory state rather than the outcome of one
/// specific operation. The Orleans alias and serialized field identifiers form
/// part of the grain-call contract and should remain stable when the CLR type or
/// its members are refactored.
/// </remarks>
[GenerateSerializer]
[Alias("Ordering.Grains.Contracts.InventorySnapshot")]
public sealed record InventorySnapshot(
    [property: Id(0)] string ProductId,
    [property: Id(1)] int AvailableQuantity);
