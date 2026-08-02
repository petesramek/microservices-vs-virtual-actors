namespace Ordering.Grains.State;

using Ordering.Grains.Contracts;
using Orleans;

/// <summary>
/// Represents the persisted state of one order grain.
/// </summary>
[GenerateSerializer]
[Alias("Ordering.Grains.State.OrderState")]
public sealed class OrderState {
    /// <summary>
    /// Gets or sets the final order result.
    /// </summary>
    [Id(0)]
    public GrainOrderResult? Result { get; set; }
}