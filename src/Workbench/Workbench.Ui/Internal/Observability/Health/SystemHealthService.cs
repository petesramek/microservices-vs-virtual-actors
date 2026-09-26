namespace Workbench.Ui.Internal.Observability.Health;

using global::Observability.Topology.Definitions;
using global::Observability.Topology.Snapshots;
using Workbench.Ui.Internal.Observability.Health.Builders;
using Workbench.Ui.Internal.Observability.Health.Probing;
using Workbench.Ui.Internal.Observability.Health.Probing.Results;
using Workbench.Ui.Internal.Observability.Topology;

/// <summary>
/// Collects service observations and builds the latest evaluated system health
/// snapshot.
/// </summary>
internal sealed class SystemHealthService(
    TopologyDefinitionProvider topologyDefinitionProvider,
    ServiceHealthProbe serviceHealthProbe,
    TopologySnapshotBuilder snapshotBuilder,
    TimeProvider timeProvider) {
    /// <summary>
    /// Collects current service observations and creates a topology snapshot
    /// containing evaluated node, dependency, and group health.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token used to cancel health collection.
    /// </param>
    /// <returns>
    /// A task whose result contains the latest evaluated topology snapshot.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled.
    /// </exception>
    public async Task<TopologySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) {
        TopologyDefinition definition =
            topologyDefinitionProvider.Definition;
        DateTimeOffset generatedAtUtc = timeProvider.GetUtcNow();
        IReadOnlyDictionary<string, ServiceProbeResult> services =
            await serviceHealthProbe
                .ProbeServicesAsync(
                    definition.Nodes,
                    generatedAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);

        return snapshotBuilder.Build(
            definition,
            services,
            generatedAtUtc);
    }
}
