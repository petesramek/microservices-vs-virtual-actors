namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Represents the result of an inventory reservation attempt.
/// </summary>
/// <param name="Reserved">
/// <see langword="true"/> when the reservation succeeded; otherwise
/// <see langword="false"/>.
/// </param>
/// <param name="Reason">
/// Optional details explaining why the reservation was rejected.
/// </param>
/// <param name="AvailableQuantity">
/// The inventory quantity available after the reservation attempt.
/// </param>
/// <remarks>
/// The Orleans alias and member identifiers form part of the serialized grain
/// contract. Existing identifiers must remain stable when this type evolves.
/// </remarks>
[GenerateSerializer]
[Alias("Ordering.Grains.Contracts.InventoryReservationResult")]
public sealed record InventoryReservationResult(
    [property: Id(0)] bool Reserved,
    [property: Id(1)] string? Reason,
    [property: Id(2)] int AvailableQuantity);
