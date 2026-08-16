namespace Ordering.Grains.Grains.Abstraction;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Defines the workflow operations for one order identity.
/// </summary>
/// <remarks>
/// The Orleans GUID grain key identifies the order whose workflow state is
/// addressed. The explicit aliases form part of the Orleans call contract and
/// should remain stable when the interface or its methods are refactored.
/// </remarks>
[Alias($"Ordering.Grains.Grains.Abstraction.IOrderGrain")]
public interface IOrderGrain : IGrainWithGuidKey {
    /// <summary>
    /// Places the order when the supplied idempotency key has not already been
    /// processed for this order identity.
    /// </summary>
    /// <param name="idempotencyKey">
    /// The caller-provided key used to identify repeated placement requests.
    /// </param>
    /// <param name="customerId">
    /// The identifier of the customer placing the order.
    /// </param>
    /// <param name="productId">The identifier of the ordered product.</param>
    /// <param name="quantity">The requested product quantity.</param>
    /// <param name="simulatePaymentFailure">
    /// A value indicating whether the showcase workflow should request the
    /// simulated payment-failure path.
    /// </param>
    /// <returns>
    /// A task whose result describes the completed or rejected order workflow.
    /// </returns>
    [Alias($"PlaceAsync")]
    Task<GrainOrderResult> PlaceAsync(
        string idempotencyKey,
        string customerId,
        string productId,
        int quantity,
        bool simulatePaymentFailure);

    /// <summary>
    /// Gets the current result for the order workflow.
    /// </summary>
    /// <returns>
    /// A task whose result is the current order result, or
    /// <see langword="null"/> when no result is available.
    /// </returns>
    [Alias($"GetAsync")]
    Task<GrainOrderResult?> GetAsync();
}
