namespace Hosting.AppHost.Observability.Topology;

using Aspire.Hosting.ApplicationModel;
using global::Observability.Topology.Definitions;

/// <summary>
/// Builds the neutral observability topology from Aspire project resources.
/// </summary>
/// <remarks>
/// Nodes, dependency edges, and visual groups are registered independently.
/// Group membership does not imply dependency direction, and dependency
/// registration does not affect Aspire grouping.
/// </remarks>
internal sealed class TopologyBuilder {
    private const string SelfHealthEntryKey = "self";

    private readonly List<TopologyNodeDefinition> nodes = [];
    private readonly List<TopologyEdgeDefinition> edges = [];
    private readonly List<TopologyGroupDefinition> groups = [];

    private readonly Dictionary<string, IResourceBuilder<ProjectResource>>
        projectResources = new(StringComparer.Ordinal);

    private readonly HashSet<string> nodeIds =
        new(StringComparer.Ordinal);

    private readonly HashSet<string> edgeKeys =
        new(StringComparer.Ordinal);

    private readonly HashSet<string> groupIds =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the registered neutral topology nodes.
    /// </summary>
    internal IReadOnlyList<TopologyNodeDefinition> Nodes => nodes;

    /// <summary>
    /// Gets the registered neutral dependency edges.
    /// </summary>
    internal IReadOnlyList<TopologyEdgeDefinition> Edges => edges;

    /// <summary>
    /// Gets the registered neutral visual groups.
    /// </summary>
    internal IReadOnlyList<TopologyGroupDefinition> Groups => groups;

    /// <summary>
    /// Gets the graph definition produced by the current registrations.
    /// </summary>
    internal TopologyDefinition Definition =>
        new(
            nodes.ToArray(),
            edges.ToArray(),
            groups.ToArray());

    /// <summary>
    /// Adds a service node backed by an Aspire project resource.
    /// </summary>
    /// <param name="resource">
    /// The Aspire project resource represented by the node.
    /// </param>
    /// <param name="displayName">
    /// The display name presented in observability views.
    /// </param>
    /// <param name="healthEntryKey">
    /// The named entry in the service health report that represents the
    /// service's own direct health.
    /// </param>
    /// <returns>The current topology builder.</returns>
    public TopologyBuilder AddService(
        IResourceBuilder<ProjectResource> resource,
        string displayName,
        string healthEntryKey = SelfHealthEntryKey) {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(healthEntryKey);

        string nodeId = resource.Resource.Name;

        RegisterNode(
            new TopologyNodeDefinition(
                nodeId,
                displayName,
                TopologyNodeKind.Service,
                new HealthSourceDefinition(
                    nodeId,
                    healthEntryKey)));

        projectResources.Add(nodeId, resource);

        return this;
    }

    /// <summary>
    /// Adds a storage node whose direct health is reported by a service.
    /// </summary>
    /// <param name="id">
    /// The stable storage-node identifier.
    /// </param>
    /// <param name="displayName">
    /// The display name presented in observability views.
    /// </param>
    /// <param name="provider">
    /// The service resource whose health report contains the storage entry.
    /// </param>
    /// <param name="healthEntryKey">
    /// The named health-report entry representing the storage resource.
    /// </param>
    /// <returns>The current topology builder.</returns>
    public TopologyBuilder AddStorage(
        string id,
        string displayName,
        IResourceBuilder<ProjectResource> provider,
        string healthEntryKey) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(healthEntryKey);

        string providerNodeId = provider.Resource.Name;

        EnsureServiceIsRegistered(
            providerNodeId,
            nameof(provider));

        RegisterNode(
            new TopologyNodeDefinition(
                id,
                displayName,
                TopologyNodeKind.Storage,
                new HealthSourceDefinition(
                    providerNodeId,
                    healthEntryKey)));

        return this;
    }

    /// <summary>
    /// Adds a directed dependency edge between two service resources.
    /// </summary>
    /// <param name="source">
    /// The service that owns and reports the dependency.
    /// </param>
    /// <param name="target">
    /// The service on which the source depends.
    /// </param>
    /// <param name="healthEntryKey">
    /// The optional named health-report entry emitted by the source for this
    /// dependency.
    /// </param>
    /// <param name="requirement">
    /// Specifies whether the dependency is required or optional.
    /// </param>
    /// <returns>The current topology builder.</returns>
    public TopologyBuilder AddDependency(
        IResourceBuilder<ProjectResource> source,
        IResourceBuilder<ProjectResource> target,
        string? healthEntryKey = null,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (healthEntryKey is not null) {
            ArgumentException.ThrowIfNullOrWhiteSpace(healthEntryKey);
        }

        string sourceNodeId = source.Resource.Name;
        string targetNodeId = target.Resource.Name;

        EnsureServiceIsRegistered(
            sourceNodeId,
            nameof(source));

        EnsureServiceIsRegistered(
            targetNodeId,
            nameof(target));

        RegisterEdge(
            new TopologyEdgeDefinition(
                sourceNodeId,
                targetNodeId,
                requirement,
                healthEntryKey));

        return this;
    }

    /// <summary>
    /// Adds a directed dependency edge from a service to a registered
    /// non-project node, such as storage.
    /// </summary>
    /// <param name="source">
    /// The service that depends on the registered target node.
    /// </param>
    /// <param name="targetNodeId">
    /// The stable identifier of the registered target node.
    /// </param>
    /// <param name="healthEntryKey">
    /// The optional named health-report entry emitted by the source for this
    /// dependency.
    /// </param>
    /// <param name="requirement">
    /// Specifies whether the dependency is required or optional.
    /// </param>
    /// <returns>The current topology builder.</returns>
    public TopologyBuilder AddDependency(
        IResourceBuilder<ProjectResource> source,
        string targetNodeId,
        string? healthEntryKey = null,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);

        if (healthEntryKey is not null) {
            ArgumentException.ThrowIfNullOrWhiteSpace(healthEntryKey);
        }

        string sourceNodeId = source.Resource.Name;

        EnsureServiceIsRegistered(
            sourceNodeId,
            nameof(source));

        EnsureNodeIsRegistered(
            targetNodeId,
            nameof(targetNodeId));

        RegisterEdge(
            new TopologyEdgeDefinition(
                sourceNodeId,
                targetNodeId,
                requirement,
                healthEntryKey));

        return this;
    }

    /// <summary>
    /// Adds a visual group containing Aspire project resources.
    /// </summary>
    /// <param name="id">
    /// The stable group identifier.
    /// </param>
    /// <param name="displayName">
    /// The display name presented in observability views.
    /// </param>
    /// <param name="members">
    /// The registered service resources belonging to the group.
    /// </param>
    /// <returns>The current topology builder.</returns>
    public TopologyBuilder AddGroup(
        string id,
        string displayName,
        params IResourceBuilder<ProjectResource>[] members) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(members);

        if (members.Length == 0) {
            throw new ArgumentException(
                "A topology group must contain at least one member.",
                nameof(members));
        }

        string[] memberNodeIds = members
            .Select(member => {
                ArgumentNullException.ThrowIfNull(member);

                string nodeId = member.Resource.Name;

                EnsureServiceIsRegistered(
                    nodeId,
                    nameof(members));

                return nodeId;
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        RegisterGroup(
            new TopologyGroupDefinition(
                id,
                displayName,
                memberNodeIds));

        return this;
    }

    /// <summary>
    /// Adds a visual group containing registered nodes identified by their
    /// stable IDs.
    /// </summary>
    /// <param name="id">
    /// The stable group identifier.
    /// </param>
    /// <param name="displayName">
    /// The display name presented in observability views.
    /// </param>
    /// <param name="memberNodeIds">
    /// The stable identifiers of registered group members.
    /// </param>
    /// <returns>The current topology builder.</returns>
    public TopologyBuilder AddGroup(
        string id,
        string displayName,
        IReadOnlyCollection<string> memberNodeIds) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(memberNodeIds);

        if (memberNodeIds.Count == 0) {
            throw new ArgumentException(
                "A topology group must contain at least one member.",
                nameof(memberNodeIds));
        }

        string[] normalizedMemberNodeIds = memberNodeIds
            .Select(nodeId => {
                ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

                EnsureNodeIsRegistered(
                    nodeId,
                    nameof(memberNodeIds));

                return nodeId;
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        RegisterGroup(
            new TopologyGroupDefinition(
                id,
                displayName,
                normalizedMemberNodeIds));

        return this;
    }

    /// <summary>
    /// Gets a registered Aspire project resource by topology node ID.
    /// </summary>
    /// <param name="nodeId">
    /// The stable service-node identifier.
    /// </param>
    /// <returns>The corresponding Aspire project resource.</returns>
    /// <exception cref="InvalidOperationException">
    /// The node does not represent a registered Aspire project resource.
    /// </exception>
    internal IResourceBuilder<ProjectResource> GetProjectResource(
        string nodeId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        if (!projectResources.TryGetValue(
                nodeId,
                out IResourceBuilder<ProjectResource>? resource)) {
            throw new InvalidOperationException(
                $"Topology node '{nodeId}' does not represent a registered " +
                "Aspire project resource.");
        }

        return resource;
    }

    /// <summary>
    /// Attempts to get the Aspire project resource associated with a topology
    /// node.
    /// </summary>
    /// <param name="nodeId">
    /// The stable topology node identifier.
    /// </param>
    /// <returns>
    /// The associated Aspire project resource, or <see langword="null"/> when
    /// the node is not backed by an Aspire project resource.
    /// </returns>
    internal IResourceBuilder<ProjectResource>? TryGetProjectResource(
        string nodeId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        return projectResources.GetValueOrDefault(nodeId);
    }

    private void RegisterNode(
        TopologyNodeDefinition node) {
        if (!nodeIds.Add(node.Id)) {
            throw new InvalidOperationException(
                $"The topology node ID '{node.Id}' is already registered.");
        }

        nodes.Add(node);
    }

    private void RegisterEdge(
        TopologyEdgeDefinition edge) {
        if (string.Equals(
                edge.SourceNodeId,
                edge.TargetNodeId,
                StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                $"Topology node '{edge.SourceNodeId}' cannot depend on itself.");
        }

        string edgeKey = string.Concat(
            edge.SourceNodeId,
            "\u001f",
            edge.TargetNodeId);

        if (!edgeKeys.Add(edgeKey)) {
            throw new InvalidOperationException(
                $"The topology dependency '{edge.SourceNodeId}' to " +
                $"'{edge.TargetNodeId}' is already registered.");
        }

        edges.Add(edge);
    }

    private void RegisterGroup(
        TopologyGroupDefinition group) {
        if (!groupIds.Add(group.Id)) {
            throw new InvalidOperationException(
                $"The topology group ID '{group.Id}' is already registered.");
        }

        groups.Add(group);
    }

    private void EnsureNodeIsRegistered(
        string nodeId,
        string parameterName) {
        if (!nodeIds.Contains(nodeId)) {
            throw new ArgumentException(
                $"Topology node '{nodeId}' must be registered before it can " +
                "be referenced.",
                parameterName);
        }
    }

    private void EnsureServiceIsRegistered(
        string nodeId,
        string parameterName) {
        TopologyNodeDefinition? node = nodes.FirstOrDefault(
            candidate => string.Equals(
                candidate.Id,
                nodeId,
                StringComparison.Ordinal));

        if (node is null) {
            throw new ArgumentException(
                $"Topology service '{nodeId}' must be registered before it " +
                "can be referenced.",
                parameterName);
        }

        if (node.Kind != TopologyNodeKind.Service) {
            throw new ArgumentException(
                $"Topology node '{nodeId}' is not a service node.",
                parameterName);
        }
    }
}