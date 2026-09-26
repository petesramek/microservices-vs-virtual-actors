namespace Ordering.Grains.State;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Represents the persisted state of one order grain.
/// </summary>
/// <remarks>
/// Orleans serialization member identifiers are part of the persisted-state
/// contract. Existing <see cref="IdAttribute"/> values must not be reused for
/// different members.
/// </remarks>
[GenerateSerializer]
[Alias("Ordering.Grains.State.OrderState")]
public sealed class OrderState {
    /// <summary>
    /// Gets or sets the terminal result produced for the order.
    /// </summary>
    /// <value>
    /// The terminal order result, or <see langword="null"/> when the order has
    /// not yet produced a persisted result.
    /// </value>
    [Id(0)]
    public GrainOrderResult? Result { get; set; }
}
