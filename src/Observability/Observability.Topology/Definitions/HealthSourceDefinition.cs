namespace Observability.Topology.Definitions;

/// <summary>
/// Defines how direct health for a node is obtained.
/// </summary>
public sealed record HealthSourceDefinition(
    string ProviderNodeId,
    string EntryKey);