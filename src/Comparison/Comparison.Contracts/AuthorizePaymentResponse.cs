namespace Comparison.Contracts;

/// <summary>
/// Represents the result of a payment authorization attempt.
/// </summary>
/// <param name="Authorized">A value indicating whether payment was authorized.</param>
/// <param name="Reason">The failure reason when payment was not authorized.</param>
public sealed record AuthorizePaymentResponse(
    bool Authorized,
    string? Reason);
