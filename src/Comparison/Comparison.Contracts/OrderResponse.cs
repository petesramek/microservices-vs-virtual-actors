namespace Comparison.Contracts;

/// <summary>
/// Represents the externally visible result of an order request.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="Status">The order status.</param>
/// <param name="Reason">The rejection reason when the order was not completed.</param>
public sealed record OrderResponse(
    Guid OrderId,
    OrderStatus Status,
    string? Reason);
