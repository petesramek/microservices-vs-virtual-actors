namespace Comparison.Contracts.Observability.Topology;

using Comparison.Contracts.Observability.Health;

/// <summary>
/// Represents the observed state of a node and its dependencies.
/// </summary>
/// <param name="Id">The stable identifier of the node.</param>
/// <param name="DisplayName">The display name shown to users.</param>
/// <param name="Kind">The role of the node in the topology.</param>
/// <param name="OwnStatus">The health reported directly for the node.</param>
/// <param name="CompositeStatus">The health of the node including its required dependencies.</param>
/// <param name="CheckedAtUtc">The time at which the node health was last checked.</param>
/// <param name="Duration">The duration of the node health check.</param>
/// <param name="Description">A sanitized explanation of the current health state.</param>
/// <param name="Children">The observed states of the direct dependencies.</param>
public sealed record TopologyNodeSnapshot(
    string Id,
    string DisplayName,
    TopologyNodeKind Kind,
    ObservabilityHealthStatus OwnStatus,
    ObservabilityHealthStatus CompositeStatus,
    DateTimeOffset? CheckedAtUtc,
    TimeSpan? Duration,
    string? Description,
    IReadOnlyList<TopologyNodeSnapshot> Children);
