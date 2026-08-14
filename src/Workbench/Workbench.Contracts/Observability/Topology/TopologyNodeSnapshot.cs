namespace Observability.Topology;

using global::Observability.Health;

/// <summary>
/// Represents the observed state of a node and its dependencies.
/// </summary>
/// <param name="Id">The stable identifier of the node.</param>
/// <param name="DisplayName">The display name shown to users.</param>
/// <param name="Kind">The role of the node in the topology.</param>
/// <param name="OwnStatus">
/// The health reported directly for the node, or <see langword="null" />
/// when the node has no independent health source.
/// </param>
/// <param name="DependencyStatus">
/// The aggregate health of the node's direct dependencies, or
/// <see langword="null" /> when the node has no dependencies.
/// </param>
/// <param name="CheckedAtUtc">
/// The time at which the node health was last checked.
/// </param>
/// <param name="Duration">The duration of the node health check.</param>
/// <param name="Description">
/// A sanitized explanation of the current health state.
/// </param>
/// <param name="Children">
/// The observed states of the direct dependencies.
/// </param>
public sealed record TopologyNodeSnapshot(
    string Id,
    string DisplayName,
    TopologyNodeKind Kind,
    HealthStatus? OwnStatus,
    HealthStatus? DependencyStatus,
    DateTimeOffset? CheckedAtUtc,
    TimeSpan? Duration,
    string? Description,
    IReadOnlyList<TopologyNodeSnapshot> Children) {
    /// <summary>
    /// Gets the aggregate health of the node and its dependencies.
    /// </summary>
    public HealthStatus CompositeStatus => HealthStatusEvaluator.Instance.Evaluate(
        GetCompositeStatuses(OwnStatus, DependencyStatus));

    private static IReadOnlyCollection<HealthStatus> GetCompositeStatuses(
        HealthStatus? ownStatus,
        HealthStatus? dependencyStatus) {
        return (ownStatus, dependencyStatus) switch {
            (HealthStatus own, HealthStatus dependencies) =>
                [own, dependencies],
            (HealthStatus own, null) =>
                [own],
            (null, HealthStatus dependencies) =>
                [dependencies],
            (null, null) =>
                [],
        };
    }
}
