namespace Workbench.Contracts.Inventory;

/// <summary>
/// Represents a request to reset inventory for a deterministic workbench
/// scenario.
/// </summary>
/// <param name="ProductId">
/// The identifier of the product whose inventory is reset.
/// </param>
/// <param name="Quantity">
/// The quantity that becomes available after the reset.
/// </param>
/// <remarks>
/// Reset behavior is intended for repeatable showcase scenarios and may also
/// remove active reservations for the selected product.
/// </remarks>
public sealed record ResetInventoryRequest(
    string ProductId,
    int Quantity);
