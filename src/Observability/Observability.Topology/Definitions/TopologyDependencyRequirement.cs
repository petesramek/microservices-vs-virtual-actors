namespace Observability.Topology.Definitions;

/// <summary>
/// Specifies whether a dependency is required when evaluating topology health.
/// </summary>
public enum TopologyDependencyRequirement {
    /// <summary>
    /// Indicates that dependency health contributes to the dependent node's
    /// aggregate health.
    /// </summary>
    Required = 0,

    /// <summary>
    /// Indicates that the dependency is represented in the topology but is not
    /// required for the dependent node to remain available.
    /// </summary>
    Optional = 1,
}
