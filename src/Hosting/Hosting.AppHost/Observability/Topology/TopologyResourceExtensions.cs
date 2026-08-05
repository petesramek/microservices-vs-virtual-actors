namespace Hosting.AppHost.Observability.Topology;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hosting.AppHost.Resources;
using System.Text.Json;
using Workbench.Contracts.Observability.Topology;

/// <summary>
/// Provides registration methods for observable application topologies.
/// </summary>
internal static class TopologyResourceExtensions {
    private const string TopologyConfigurationName =
        "Observability__TopologyDefinition";
    private const string HealthEndpointConfigurationPrefix =
        "Observability__HealthEndpoints";
    private const string HttpEndpointName = "http";
    private const string HealthEndpointPath = "/health";

    /// <summary>
    /// Registers one topology definition for Aspire grouping and runtime health processing.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="topologyProvider">
    /// The project resource that receives and processes the topology definition.
    /// </param>
    /// <param name="configure">Configures the top-level topology nodes.</param>
    /// <returns>The generated neutral topology definition.</returns>
    public static TopologyDefinition AddTopology(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> topologyProvider,
        Action<TopologyBuilder> configure) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(topologyProvider);
        ArgumentNullException.ThrowIfNull(configure);

        var topology = new TopologyBuilder();
        configure(topology);

        if (topology.Children.Count == 0) {
            throw new InvalidOperationException(
                "The topology must contain at least one top-level node.");
        }

        foreach (TopologyNodeBuilder group in EnumerateGroups(topology.Children)) {
            IResourceBuilder<ProjectResource>[] groupResources = group
                .EnumerateProjectResources()
                .DistinctBy(resource => resource.Resource.Name)
                .ToArray();

            builder.AddHealthGroup(
                group.Id,
                group.DisplayName,
                groupResources);
        }

        var definition = new TopologyDefinition(
            topology.Children
                .Select(node => node.BuildDefinition())
                .ToArray());

        topologyProvider.WithEnvironment(
            TopologyConfigurationName,
            JsonSerializer.Serialize(definition));

        foreach (IResourceBuilder<ProjectResource> resource in
            topology.ProjectResources) {
            string configurationName = string.Join(
                "__",
                HealthEndpointConfigurationPrefix,
                resource.Resource.Name);

            topologyProvider.WithEnvironment(
                configurationName,
                ReferenceExpression.Create(
                    $"{resource.GetEndpoint(HttpEndpointName)}{HealthEndpointPath}"));
        }

        return definition;
    }

    private static IEnumerable<TopologyNodeBuilder> EnumerateGroups(
        IEnumerable<TopologyNodeBuilder> nodes) {
        foreach (TopologyNodeBuilder node in nodes) {
            if (node.Kind == TopologyNodeKind.Group) {
                yield return node;
            }

            foreach (TopologyNodeBuilder group in EnumerateGroups(node.Children)) {
                yield return group;
            }
        }
    }
}
