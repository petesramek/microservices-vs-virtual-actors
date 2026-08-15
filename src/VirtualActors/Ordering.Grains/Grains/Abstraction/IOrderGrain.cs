namespace Ordering.Grains.Grains.Abstraction;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Represents one order workflow identity.
/// </summary>
[Alias($"Ordering.Grains.Abstraction.IOrderGrain")]
public interface IOrderGrain : IGrainWithGuidKey {
    /// <summary>
    /// Places the order if it has not already been processed.
    /// </summary>
    [Alias($"PlaceAsync")]
    Task<GrainOrderResult> PlaceAsync(
        string idempotencyKey,
        string customerId,
        string productId,
        int quantity,
        bool simulatePaymentFailure);

    /// <summary>
    /// Gets the current order result, when available.
    /// </summary>
    [Alias($"GetAsync")]
    Task<GrainOrderResult?> GetAsync();
}
