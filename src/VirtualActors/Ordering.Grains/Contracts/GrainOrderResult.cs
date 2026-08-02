namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Order result returned by an order grain.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="Status">The final order status.</param>
/// <param name="Reason">The reason the order failed, when applicable.</param>
[GenerateSerializer]
[Alias("Ordering.Grains.Contracts.GrainOrderResult")]
public sealed record GrainOrderResult(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] string Status,
    [property: Id(2)] string? Reason);
