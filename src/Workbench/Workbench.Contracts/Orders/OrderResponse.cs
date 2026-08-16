namespace Workbench.Contracts.Orders;

/// <summary>
/// Represents the externally visible result of an order request.
/// </summary>
/// <param name="OrderId">The unique identifier of the order.</param>
/// <param name="Status">The externally visible state of the order.</param>
/// <param name="Reason">
/// Details explaining why the order was rejected, or
/// <see langword="null"/> when no rejection reason applies.
/// </param>
/// <remarks>
/// This immutable contract is shared by the microservices and virtual-actor
/// implementations. Additive or breaking changes must therefore be coordinated
/// across all producers and consumers.
/// </remarks>
public sealed record OrderResponse(
    Guid OrderId,
    OrderStatus Status,
    string? Reason);
