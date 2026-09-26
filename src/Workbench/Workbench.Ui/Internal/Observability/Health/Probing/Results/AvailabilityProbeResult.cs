namespace Workbench.Ui.Internal.Observability.Health.Probing.Results;

using global::Observability.Topology.Snapshots;

/// <summary>
/// Represents the result of probing a service alive endpoint.
/// </summary>
/// <param name="Availability">The observed resource availability.</param>
/// <param name="CheckedAtUtc">The observation timestamp.</param>
/// <param name="Description">The optional explanatory description.</param>
internal sealed record AvailabilityProbeResult(
    ResourceAvailability Availability,
    DateTimeOffset CheckedAtUtc,
    string? Description);
