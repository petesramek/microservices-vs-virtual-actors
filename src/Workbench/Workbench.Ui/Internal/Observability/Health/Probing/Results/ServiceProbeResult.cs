namespace Workbench.Ui.Internal.Observability.Health.Probing.Results;
/// <summary>
/// Represents the availability and health observations collected for a service
/// node.
/// </summary>
/// <param name="NodeId">The stable topology node identifier.</param>
/// <param name="Availability">The service availability observation.</param>
/// <param name="Health">The service health observation.</param>
internal sealed record ServiceProbeResult(
    string NodeId,
    AvailabilityProbeResult Availability,
    HealthProbeResult Health);



