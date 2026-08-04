namespace Hosting.AppHost.Observability.Topology;

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Comparison.Contracts.Observability.Topology;
using Hosting.AppHost.Resources;

/// <summary>
/// Provides registration methods for the observable comparison topology.
/// </summary>
internal static class ComparisonTopologyResourceExtensions {
    private const string TopologyConfigurationName =
        "Observability__TopologyDefinition";

    /// <summary>
    /// Registers one topology definition for Aspire grouping and Comparison Gateway.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="gateway">The Comparison Gateway resource.</param>
    /// <param name="configure">Configures the topology below the Gateway.</param>
    /// <returns>The generated neutral topology definition.</returns>
    public static TopologyDefinition AddComparisonTopology(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> gateway,
        Action<ComparisonTopologyBuilder> configure) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(configure);

        var topology = new ComparisonTopologyBuilder();
        configure(topology);

        if (topology.Children.Count == 0) {
            throw new InvalidOperationException(
                "The comparison topology must contain at least one group.");
        }

        foreach (ComparisonTopologyNodeBuilder group in topology.Children) {
            IResourceBuilder<ProjectResource>[] groupResources = group.Children
                .SelectMany(EnumerateProjectResources)
                .DistinctBy(resource => resource.Resource.Name)
                .ToArray();

            builder.AddHealthGroup(
                group.Id,
                group.DisplayName,
                groupResources);
        }

        var definition = new TopologyDefinition(
            new TopologyNodeDefinition(
                gateway.Resource.Name,
                "Comparison Gateway",
                TopologyNodeKind.Service,
                gateway.Resource.Name,
                TopologyDependencyRequirement.Required,
                topology.Children
                    .Select(group => group.BuildDefinition())
                    .ToArray()));

        gateway.WithEnvironment(
            TopologyConfigurationName,
            JsonSerializer.Serialize(definition));

        return definition;
    }

    private static IEnumerable<IResourceBuilder<ProjectResource>>
        EnumerateProjectResources(ComparisonTopologyNodeBuilder node) {
        if (node.Resource is not null) {
            yield return node.Resource;
        }

        foreach (ComparisonTopologyNodeBuilder child in node.Children) {
            foreach (IResourceBuilder<ProjectResource> resource in
                EnumerateProjectResources(child)) {
                yield return resource;
            }
        }
    }
}
