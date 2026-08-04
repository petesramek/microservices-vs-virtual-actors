namespace Comparison.Contracts.Observability.Topology;

/// <summary>
/// Defines how a child node affects the composite health of its parent.
/// </summary>
public enum TopologyDependencyRequirement {
    /// <summary>
    /// An unhealthy child makes the parent composite health unhealthy.
    /// </summary>
    Required,

    /// <summary>
    /// An unhealthy child degrades the parent without making it unavailable.
    /// </summary>
    Optional,
}
