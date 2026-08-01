namespace Comparison.Contracts;

/// <summary>
/// Represents a request to reset inventory for a deterministic scenario.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Quantity">The inventory quantity to set.</param>
public sealed record ResetInventoryRequest(
    string ProductId,
    int Quantity);
