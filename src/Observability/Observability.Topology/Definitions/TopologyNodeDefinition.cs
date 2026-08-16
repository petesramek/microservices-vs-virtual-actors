namespace Observability.Topology.Definitions;

/// <summary>
/// Defines a service or storage resource in the topology.
/// </summary>
/// <param name="Id">
/// The stable identifier used to reference and serialize the node.
/// </param>
/// <param name="DisplayName">
/// The user-facing name displayed in topology views.
/// </param>
/// <param name="Kind">The kind of resource represented by the node.</param>
/// <param name="HealthSource">
/// The optional source used to resolve the node's direct health.
/// </param>
/// <remarks>
/// The identifier and property names are part of the serialized topology
/// contract. Identifier uniqueness, supported node kinds, and health-source
/// references are enforced by the topology validator rather than by this
/// transport contract.
/// </remarks>
public sealed record TopologyNodeDefinition(
    string Id,
    string DisplayName,
    TopologyNodeKind Kind,
    HealthSourceDefinition? HealthSource);
