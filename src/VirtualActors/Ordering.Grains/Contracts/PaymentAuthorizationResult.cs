namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Represents a payment authorization result returned by a payment-account
/// grain.
/// </summary>
/// <param name="Authorized">
/// <see langword="true"/> when the payment was authorized; otherwise
/// <see langword="false"/>.
/// </param>
/// <param name="Reason">
/// Optional details explaining why authorization was rejected.
/// </param>
/// <remarks>
/// The Orleans alias and member identifiers form part of the serialized grain
/// contract. Existing identifiers must remain stable when this type evolves.
/// </remarks>
[GenerateSerializer]
[Alias("Ordering.Grains.Contracts.PaymentAuthorizationResult")]
public sealed record PaymentAuthorizationResult(
    [property: Id(0)] bool Authorized,
    [property: Id(1)] string? Reason);
