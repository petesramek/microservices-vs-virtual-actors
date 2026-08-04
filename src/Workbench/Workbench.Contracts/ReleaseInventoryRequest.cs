namespace Workbench.Contracts;

/// <summary>
/// Represents a request to release an inventory reservation.
/// </summary>
/// <param name="ReservationId">The reservation identifier.</param>
public sealed record ReleaseInventoryRequest(Guid ReservationId);
