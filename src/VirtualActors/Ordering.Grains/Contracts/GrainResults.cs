namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Result returned by an inventory grain reservation attempt.
/// </summary>
[GenerateSerializer]
public sealed record InventoryReservationResult(
    [property: Id(0)] bool Reserved,
    [property: Id(1)] string? Reason,
    [property: Id(2)] int AvailableQuantity);

/// <summary>
/// Inventory state returned by an inventory grain.
/// </summary>
[GenerateSerializer]
public sealed record InventorySnapshot(
    [property: Id(0)] string ProductId,
    [property: Id(1)] int AvailableQuantity);

/// <summary>
/// Payment authorization result returned by a payment account grain.
/// </summary>
[GenerateSerializer]
public sealed record PaymentAuthorizationResult(
    [property: Id(0)] bool Authorized,
    [property: Id(1)] string? Reason);

/// <summary>
/// Order result returned by an order grain.
/// </summary>
[GenerateSerializer]
public sealed record GrainOrderResult(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] string Status,
    [property: Id(2)] string? Reason);
