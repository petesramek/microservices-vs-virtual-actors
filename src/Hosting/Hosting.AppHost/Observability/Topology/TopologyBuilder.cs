namespace Hosting.AppHost.Observability.Topology;

using Aspire.Hosting.ApplicationModel;
using Workbench.Contracts.Observability.Topology;

/// <summary>
/// Builds the observable application topology from Aspire project resources.
/// </summary>
internal sealed class TopologyBuilder
{
    private readonly List<TopologyNodeBuilder> children = [];
    private readonly HashSet<string> nodeIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a logical group as a top-level topology node.
    /// </summary>
    /// <param name="id">The stable identifier of the group.</param>
    /// <param name="displayName">The display name shown to users.</param>
    /// <param name="configure">Configures the nodes contained in the group.</param>
    /// <returns>The current topology builder.</returns>
    public TopologyBuilder AddGroup(
        string id,
        string displayName,
        Action<TopologyNodeBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(configure);

        RegisterNodeId(id);

        var group = new TopologyNodeBuilder(
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

    /// <summary>
    /// Adds a project service as a top-level topology node.
    /// </summary>
    /// <param name="resource">The Aspire project resource.</param>
    /// <param name="displayName">The display name shown to users.</param>
    /// <param name="configure">Optionally configures dependencies owned by the service.</param>
    /// <returns>The current topology builder.</returns>
    public TopologyBuilder AddService(
        IResourceBuilder<ProjectResource> resource,
        string displayName,
        Action<TopologyNodeBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        string id = resource.Resource.Name;
        RegisterNodeId(id);

        var service = new TopologyNodeBuilder(
            id,
            displayName,
            TopologyNodeKind.Service,
            id,
            resource,
            TopologyDependencyRequirement.Required,
            RegisterNodeId);

        configure?.Invoke(service);
        children.Add(service);

        return this;
    }

    internal IReadOnlyList<TopologyNodeBuilder> Children => children;

    internal IReadOnlyList<IResourceBuilder<ProjectResource>> ProjectResources =>
        children
            .SelectMany(child => child.EnumerateProjectResources())
            .DistinctBy(resource => resource.Resource.Name)
            .ToArray();

    private void RegisterNodeId(string id)
    {
        if (!nodeIds.Add(id))
        {
            throw new InvalidOperationException(
                $"The topology node ID '{id}' is already registered.");
        }
    }
}

/// <summary>
/// Builds a node and its direct dependencies in the observable topology.
/// </summary>
internal sealed class TopologyNodeBuilder
{
    private readonly List<TopologyNodeBuilder> children = [];
    private readonly Action<string> registerNodeId;

    internal TopologyNodeBuilder(
        string id,
        string displayName,
        TopologyNodeKind kind,
        string? healthSource,
        IResourceBuilder<ProjectResource>? resource,
        TopologyDependencyRequirement requirement,
        Action<string> registerNodeId)
    {
        Id = id;
        DisplayName = displayName;
        Kind = kind;
        HealthSource = healthSource;
        Resource = resource;
        Requirement = requirement;
        this.registerNodeId = registerNodeId;
    }

    /// <summary>
    /// Adds a logical group as a direct child of this node.
    /// </summary>
    /// <param name="id">The stable identifier of the group.</param>
    /// <param name="displayName">The display name shown to users.</param>
    /// <param name="configure">Configures the nodes contained in the group.</param>
    /// <param name="requirement">How the group affects its parent composite health.</param>
    /// <returns>The current node builder.</returns>
    public TopologyNodeBuilder AddGroup(
        string id,
        string displayName,
        Action<TopologyNodeBuilder> configure,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(configure);

        registerNodeId(id);

        var group = new TopologyNodeBuilder(
            id,
            displayName,
            TopologyNodeKind.Group,
            healthSource: null,
            resource: null,
            requirement,
            registerNodeId);

        configure(group);
        children.Add(group);

        return this;
    }

    /// <summary>
    /// Adds a project service as a direct dependency of this node.
    /// </summary>
    /// <param name="resource">The Aspire project resource.</param>
    /// <param name="displayName">The display name shown to users.</param>
    /// <param name="configure">Optionally configures dependencies owned by the service.</param>
    /// <param name="requirement">How the service affects its parent composite health.</param>
    /// <returns>The current node builder.</returns>
    public TopologyNodeBuilder AddService(
        IResourceBuilder<ProjectResource> resource,
        string displayName,
        Action<TopologyNodeBuilder>? configure = null,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        string id = resource.Resource.Name;
        registerNodeId(id);

        var service = new TopologyNodeBuilder(
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
    public TopologyNodeBuilder AddStorage(
        string id,
        string displayName,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        registerNodeId(id);
        children.Add(new TopologyNodeBuilder(
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

    internal IReadOnlyList<TopologyNodeBuilder> Children => children;

    internal TopologyNodeDefinition BuildDefinition()
    {
        return new TopologyNodeDefinition(
            Id,
            DisplayName,
            Kind,
            HealthSource,
            Requirement,
            children.Select(child => child.BuildDefinition()).ToArray());
    }

    internal IEnumerable<IResourceBuilder<ProjectResource>>
        EnumerateProjectResources()
    {
        if (Resource is not null)
        {
            yield return Resource;
        }

        foreach (TopologyNodeBuilder child in children)
        {
            foreach (IResourceBuilder<ProjectResource> resource in
                child.EnumerateProjectResources())
            {
                yield return resource;
            }
        }
    }
}
