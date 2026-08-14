namespace Hosting.AppHost.Observability.Topology;

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using global::Observability.Topology.Definitions;
using Hosting.AppHost.Resources;

/// <summary>
/// Provides registration methods for observable application topologies.
/// </summary>
internal static class TopologyResourceExtensions {
    private const string TopologyConfigurationName =
        "Observability__TopologyDefinition";

    private const string HealthEndpointConfigurationPrefix =
        "Observability__HealthEndpoints";

    private const string AliveEndpointConfigurationPrefix =
        "Observability__AliveEndpoints";

    private const string HttpEndpointName = "http";

    /// <summary>
    /// Registers one graph topology for Aspire grouping and Workbench
    /// observability collection.
    /// </summary>
    /// <param name="builder">
    /// The distributed application builder.
    /// </param>
    /// <param name="topologyProvider">
    /// The project resource that receives the serialized topology and
    /// service endpoint configuration.
    /// </param>
    /// <param name="configure">
    /// Configures topology nodes, dependency edges, and visual groups.
    /// </param>
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

        if (topology.Nodes.Count == 0) {
            throw new InvalidOperationException(
                "The topology must contain at least one node.");
        }

        TopologyDefinition definition = topology.Definition;

        AddAspireHealthGroups(
            builder,
            topology,
            definition.Groups);

        topologyProvider.WithEnvironment(
            TopologyConfigurationName,
            JsonSerializer.Serialize(definition));

        AddServiceEndpointConfiguration(
            topologyProvider,
            topology,
            definition.Nodes);

        return definition;
    }

    private static void AddAspireHealthGroups(
        IDistributedApplicationBuilder builder,
        TopologyBuilder topology,
        IReadOnlyCollection<TopologyGroupDefinition> groups) {
        foreach (TopologyGroupDefinition group in groups) {
            IResourceBuilder<ProjectResource>[] resources = group.NodeIds
                .Select(topology.TryGetProjectResource)
                .Where(resource => resource is not null)
                .Cast<IResourceBuilder<ProjectResource>>()
                .DistinctBy(
                    resource => resource.Resource.Name,
                    StringComparer.Ordinal)
                .ToArray();

            if (resources.Length == 0) {
                continue;
            }

            builder.AddHealthGroup(
                group.Id,
                group.DisplayName,
                resources);
        }
    }

    private static void AddServiceEndpointConfiguration(
        IResourceBuilder<ProjectResource> topologyProvider,
        TopologyBuilder topology,
        IReadOnlyCollection<TopologyNodeDefinition> nodes) {
        foreach (TopologyNodeDefinition node in nodes) {
            if (node.Kind != TopologyNodeKind.Service) {
                continue;
            }

            IResourceBuilder<ProjectResource> resource =
                topology.GetProjectResource(node.Id);

            topologyProvider.WithEnvironment(
                CreateConfigurationName(
                    HealthEndpointConfigurationPrefix,
                    node.Id),
                ReferenceExpression.Create(
                    $"{resource.GetEndpoint(HttpEndpointName)}/health"));

            topologyProvider.WithEnvironment(
                CreateConfigurationName(
                    AliveEndpointConfigurationPrefix,
                    node.Id),
                ReferenceExpression.Create(
                    $"{resource.GetEndpoint(HttpEndpointName)}/alive"));
        }
    }

    private static string CreateConfigurationName(
        string prefix,
        string nodeId) {
        return string.Join(
            "__",
            prefix,
            nodeId);
    }
}