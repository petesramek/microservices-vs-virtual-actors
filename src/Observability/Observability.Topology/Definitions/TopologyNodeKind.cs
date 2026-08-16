namespace Observability.Topology.Definitions;

/// <summary>
/// Identifies the role of a node in the observability topology.
/// </summary>
public enum TopologyNodeKind {
    /// <summary>
    /// Identifies an application service that can publish health information
    /// and participate in directed dependencies.
    /// </summary>
    Service = 0,

    /// <summary>
    /// Identifies a storage resource whose health is reported by a provider
    /// service.
    /// </summary>
    Storage = 1,
}
