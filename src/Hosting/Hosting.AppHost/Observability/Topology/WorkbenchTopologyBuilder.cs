namespace Hosting.AppHost.Observability.Topology;

using Aspire.Hosting.ApplicationModel;
using Workbench.Contracts.Observability.Topology;

/// <summary>
/// Builds the observable application topology from Aspire project resources.
/// </summary>
internal sealed class WorkbenchTopologyBuilder {
    private readonly List<WorkbenchTopologyNodeBuilder> children = [];
    private readonly HashSet<string> nodeIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a logical group below the topology root.
    /// </summary>
    /// <param name="id">The stable identifier of the group.</param>
    /// <param name="displayName">The display name shown to users.</param>
    /// <param name="configure">Configures the nodes contained in the group.</param>
    /// <returns>The current topology builder.</returns>
    public WorkbenchTopologyBuilder AddGroup(
        string id,
        string displayName,
        Action<WorkbenchTopologyNodeBuilder> configure) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(configure);

        RegisterNodeId(id);

        var group = new WorkbenchTopologyNodeBuilder(
            id,
            displayName,
            TopologyNodeKind.Group,
            healthSource: null,
            resource: null,
            TopologyDependencyRequirement.Required,
            RegisterNodeId);

        configure(group);
        children.Add(group);

        return this;
    }

    internal IReadOnlyList<WorkbenchTopologyNodeBuilder> Children => children;

    private void RegisterNodeId(string id) {
        if (!nodeIds.Add(id)) {
            throw new InvalidOperationException(
                $"The topology node ID '{id}' is already registered.");
        }
    }
}

/// <summary>
/// Builds a node and its direct dependencies in the observable topology.
/// </summary>
internal sealed class WorkbenchTopologyNodeBuilder {
    private readonly List<WorkbenchTopologyNodeBuilder> children = [];
    private readonly Action<string> registerNodeId;

    internal WorkbenchTopologyNodeBuilder(
        string id,
        string displayName,
        TopologyNodeKind kind,
        string? healthSource,
        IResourceBuilder<ProjectResource>? resource,
        TopologyDependencyRequirement requirement,
        Action<string> registerNodeId) {
        Id = id;
        DisplayName = displayName;
        Kind = kind;
        HealthSource = healthSource;
        Resource = resource;
        Requirement = requirement;
        this.registerNodeId = registerNodeId;
    }

    /// <summary>
    /// Adds a project service as a direct dependency of this node.
    /// </summary>
    /// <param name="resource">The Aspire project resource.</param>
    /// <param name="displayName">The display name shown to users.</param>
    /// <param name="configure">Optionally configures dependencies owned by the service.</param>
    /// <param name="requirement">How the service affects its parent composite health.</param>
    /// <returns>The current node builder.</returns>
    public WorkbenchTopologyNodeBuilder AddService(
        IResourceBuilder<ProjectResource> resource,
        string displayName,
        Action<WorkbenchTopologyNodeBuilder>? configure = null,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required) {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        string id = resource.Resource.Name;
        registerNodeId(id);

        var service = new WorkbenchTopologyNodeBuilder(
            id,
            displayName,
            TopologyNodeKind.Service,
            id,
            resource,
            requirement,
            registerNodeId);

        configure?.Invoke(service);
        children.Add(service);

        return this;
    }

    /// <summary>
    /// Adds storage reported by its owning service as a direct dependency.
    /// </summary>
    /// <param name="id">The stable storage identifier and health-report key.</param>
    /// <param name="displayName">The display name shown to users.</param>
    /// <param name="requirement">How the storage affects its parent composite health.</param>
    /// <returns>The current node builder.</returns>
    public WorkbenchTopologyNodeBuilder AddStorage(
        string id,
        string displayName,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        registerNodeId(id);
        children.Add(new WorkbenchTopologyNodeBuilder(
            id,
            displayName,
            TopologyNodeKind.Storage,
            id,
            resource: null,
            requirement,
            registerNodeId));

        return this;
    }

    internal string Id { get; }

    internal string DisplayName { get; }

    internal TopologyNodeKind Kind { get; }

    internal string? HealthSource { get; }

    internal IResourceBuilder<ProjectResource>? Resource { get; }

    internal TopologyDependencyRequirement Requirement { get; }

    internal IReadOnlyList<WorkbenchTopologyNodeBuilder> Children => children;

    internal TopologyNodeDefinition BuildDefinition() {
        return new TopologyNodeDefinition(
            Id,
            DisplayName,
            Kind,
            HealthSource,
            Requirement,
            children.Select(child => child.BuildDefinition()).ToArray());
    }
}
