namespace Hosting.AppHost.Observability.Topology;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hosting.AppHost.Resources;
using System.Text.Json;
using Workbench.Contracts.Observability.Topology;

/// <summary>
/// Provides registration methods for the observable workbench topology.
/// </summary>
internal static class WorkbenchTopologyResourceExtensions {
    private const string TopologyConfigurationName =
        "Observability__TopologyDefinition";

    /// <summary>
    /// Registers one topology definition for Aspire grouping and Workbench Gateway.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="gateway">The Workbench Gateway resource.</param>
    /// <param name="configure">Configures the topology below the Gateway.</param>
    /// <returns>The generated neutral topology definition.</returns>
    public static TopologyDefinition AddWorkbenchTopology(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> gateway,
        Action<WorkbenchTopologyBuilder> configure) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(configure);

        var topology = new WorkbenchTopologyBuilder();
        configure(topology);

        if (topology.Children.Count == 0) {
            throw new InvalidOperationException(
                "The workbench topology must contain at least one group.");
        }

        foreach (WorkbenchTopologyNodeBuilder group in topology.Children) {
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
                "Workbench Gateway",
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
        EnumerateProjectResources(WorkbenchTopologyNodeBuilder node) {
        if (node.Resource is not null) {
            yield return node.Resource;
        }

        foreach (WorkbenchTopologyNodeBuilder child in node.Children) {
            foreach (IResourceBuilder<ProjectResource> resource in
                EnumerateProjectResources(child)) {
                yield return resource;
            }
        }
    }
}
