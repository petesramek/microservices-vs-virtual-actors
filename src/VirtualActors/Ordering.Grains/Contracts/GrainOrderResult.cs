namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Represents the terminal result returned by an order grain.
/// </summary>
/// <param name="OrderId">The identifier of the order.</param>
/// <param name="Status">
/// The terminal order status represented by its stable contract value.
/// </param>
/// <param name="Reason">
/// Optional details explaining why the order did not complete successfully.
/// </param>
/// <remarks>
/// The Orleans alias and member identifiers form part of the serialized grain
/// contract. Existing identifiers must remain stable when this type evolves.
/// </remarks>
[GenerateSerializer]
[Alias("Ordering.Grains.Contracts.GrainOrderResult")]
public sealed record GrainOrderResult(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] string Status,
    [property: Id(2)] string? Reason);
