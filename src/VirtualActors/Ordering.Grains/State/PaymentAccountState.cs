namespace Ordering.Grains.State;

using Ordering.Grains.Contracts;
using Orleans;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents the persisted state of one payment account grain.
/// </summary>
[GenerateSerializer]
[Alias("Ordering.Grains.State.PaymentAccountState")]
public sealed class PaymentAccountState {
    /// <summary>
    /// Gets or sets payment authorization results by idempotency key.
    /// </summary>
    [Id(0)]
    [SuppressMessage(
        "Design",
        "MA0016:Prefer using collection abstraction instead of implementation",
        Justification = "Persistent grain state requires a concrete mutable collection.")]
    public Dictionary<string, PaymentAuthorizationResult> Authorizations {
        get;
        set;
    } = new(StringComparer.OrdinalIgnoreCase);
}