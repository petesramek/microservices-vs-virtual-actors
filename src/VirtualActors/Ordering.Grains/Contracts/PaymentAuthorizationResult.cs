namespace Ordering.Grains.Contracts;

using Orleans;

/// <summary>
/// Payment authorization result returned by a payment account grain.
/// </summary>
/// <param name="Authorized">Indicates whether the payment was authorized.</param>
/// <param name="Reason">The reason authorization failed, when applicable.</param>
[GenerateSerializer]
[Alias("Ordering.Grains.Contracts.PaymentAuthorizationResult")]
public sealed record PaymentAuthorizationResult(
    [property: Id(0)] bool Authorized,
    [property: Id(1)] string? Reason);
