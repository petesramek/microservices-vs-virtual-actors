namespace Observability.Topology.Definitions;

/// <summary>
/// Identifies the health-report entry that provides a topology node's direct
/// health status.
/// </summary>
/// <param name="ProviderNodeId">
/// The stable, case-sensitive identifier of the node that publishes the health
/// report.
/// </param>
/// <param name="EntryKey">
/// The key of the entry within the provider node's health report.
/// </param>
/// <remarks>
/// The provider may be the node itself or another node that reports health on
/// its behalf. Provider existence and entry-key validity are enforced by the
/// topology validator rather than by this transport contract.
/// </remarks>
public sealed record HealthSourceDefinition(
    string ProviderNodeId,
    string EntryKey);
