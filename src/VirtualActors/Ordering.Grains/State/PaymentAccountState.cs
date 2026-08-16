namespace Ordering.Grains.State;

using Ordering.Grains.Contracts;
using Orleans;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents the persisted state of one payment account grain.
/// </summary>
/// <remarks>
/// Orleans serialization member identifiers are part of the persisted-state
/// contract. Existing <see cref="IdAttribute"/> values must not be reused for
/// different members.
/// </remarks>
[GenerateSerializer]
[Alias("Ordering.Grains.State.PaymentAccountState")]
public sealed class PaymentAccountState {
    /// <summary>
    /// Gets or sets payment authorization results by idempotency key.
    /// </summary>
    /// <value>
    /// A mutable dictionary whose keys are idempotency keys and whose values
    /// are the previously produced authorization results.
    /// </value>
    /// <remarks>
    /// Idempotency keys are compared using ordinal, case-insensitive semantics.
    /// The concrete mutable collection is required because the grain records
    /// authorization results before persisting them.
    /// </remarks>
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
