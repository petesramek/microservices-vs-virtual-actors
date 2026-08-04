namespace Hosting.AppHost.Observability.Topology;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hosting.AppHost.Resources;
using System.Text.Json;
using Workbench.Contracts.Observability.Topology;

/// <summary>
/// Provides registration methods for the observable workbench topology.
/// </summary>
internal static class TopologyResourceExtensions {
    private const string TopologyConfigurationName =
        "Observability__TopologyDefinition";

    /// <summary>
    /// Registers one topology definition for Aspire grouping and Workbench Gateway.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="root">The Workbench Gateway resource.</param>
    /// <param name="configure">Configures the topology below the Gateway.</param>
    /// <returns>The generated neutral topology definition.</returns>
    public static TopologyDefinition AddTopology(
        this IDistributedApplicationBuilder builder,
        string displayName,
        IResourceBuilder<ProjectResource> root,
        Action<TopologyBuilder> configure) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(configure);

        var topology = new TopologyBuilder();
        configure(topology);

        if (topology.Children.Count == 0) {
            throw new InvalidOperationException(
                "The workbench topology must contain at least one group.");
        }

        foreach (TopologyNodeBuilder group in topology.Children) {
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
                root.Resource.Name,
                displayName,
                TopologyNodeKind.Service,
                root.Resource.Name,
                TopologyDependencyRequirement.Required,
                topology.Children
                    .Select(group => group.BuildDefinition())
                    .ToArray()));

        root.WithEnvironment(
            TopologyConfigurationName,
            JsonSerializer.Serialize(definition));

        return definition;
    }

    private static IEnumerable<IResourceBuilder<ProjectResource>>
        EnumerateProjectResources(TopologyNodeBuilder node) {
        if (node.Resource is not null) {
            yield return node.Resource;
        }

        foreach (TopologyNodeBuilder child in node.Children) {
            foreach (IResourceBuilder<ProjectResource> resource in
                EnumerateProjectResources(child)) {
                yield return resource;
            }
        }
    }
}
