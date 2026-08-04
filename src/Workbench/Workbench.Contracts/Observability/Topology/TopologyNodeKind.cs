namespace Workbench.Contracts.Observability.Topology;

/// <summary>
/// Identifies the role of a node in the observable application topology.
/// </summary>
public enum TopologyNodeKind {
    /// <summary>
    /// A logical group of related topology nodes.
    /// </summary>
    Group,

    /// <summary>
    /// An application process or network-accessible service.
    /// </summary>
    Service,

    /// <summary>
    /// A persistent storage dependency owned by a service.
    /// </summary>
    Storage,
}
