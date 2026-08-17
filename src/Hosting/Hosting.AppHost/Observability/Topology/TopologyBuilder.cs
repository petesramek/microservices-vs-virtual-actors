namespace Hosting.AppHost.Observability.Topology;

using Aspire.Hosting.ApplicationModel;
using global::Observability.Topology.Definitions;

/// <summary>
/// Builds a neutral observability topology from Aspire project resources and
/// explicitly registered non-project nodes.
/// </summary>
/// <remarks>
/// Nodes, directed dependency edges, and visual groups are registered
/// independently. Group membership does not imply dependency direction, and
/// dependency registration does not affect Aspire grouping.
///
/// <para>
/// Registrations are order-dependent. Referenced nodes must already be
/// registered. Node and group identifiers are case-sensitive and must be
/// unique. At most one dependency may exist for each source-target pair.
/// </para>
/// </remarks>
internal sealed class TopologyBuilder
{
    /// <summary>
    /// Identifies the default health-report entry that represents a service's
    /// direct health.
    /// </summary>
    private const string SelfHealthEntryKey = "self";

    /// <summary>
    /// Separates source and target identifiers in an internal dependency key.
    /// </summary>
    private const string EdgeKeySeparator = "\u001f";

    /// <summary>
    /// Stores topology nodes in registration order for views and snapshots.
    /// </summary>
    private readonly List<TopologyNodeDefinition> nodes = [];

    /// <summary>
    /// Stores directed dependency edges in registration order for views and
    /// snapshots.
    /// </summary>
    private readonly List<TopologyEdgeDefinition> edges = [];

    /// <summary>
    /// Stores visual groups in registration order for views and snapshots.
    /// </summary>
    private readonly List<TopologyGroupDefinition> groups = [];

    /// <summary>
    /// Maps service-node identifiers to their backing Aspire project
    /// resources. Non-project nodes are intentionally excluded.
    /// </summary>
    private readonly Dictionary<string, IResourceBuilder<ProjectResource>>
        projectResources = new(StringComparer.Ordinal);

    /// <summary>
    /// Tracks registered node identifiers for case-sensitive uniqueness and
    /// membership checks.
    /// </summary>
    private readonly HashSet<string> nodeIds =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Tracks directional source-target pairs to prevent duplicate dependency
    /// edges.
    /// </summary>
    private readonly HashSet<string> edgeKeys =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Tracks registered group identifiers for case-sensitive uniqueness
    /// checks.
    /// </summary>
    private readonly HashSet<string> groupIds =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a read-only view of the registered neutral topology nodes.
    /// </summary>
    internal IReadOnlyList<TopologyNodeDefinition> Nodes => nodes;

    /// <summary>
    /// Gets a read-only view of the registered neutral dependency edges.
    /// </summary>
    internal IReadOnlyList<TopologyEdgeDefinition> Edges => edges;

    /// <summary>
    /// Gets a read-only view of the registered neutral visual groups.
    /// </summary>
    internal IReadOnlyList<TopologyGroupDefinition> Groups => groups;

    /// <summary>
    /// Creates a snapshot of the currently registered topology.
    /// </summary>
    /// <remarks>
    /// Each access creates a new definition and collection snapshots.
    /// Registrations added afterward are not reflected in the returned
    /// definition.
    /// </remarks>
    internal TopologyDefinition Definition =>
        new(
            nodes.ToArray(),
            edges.ToArray(),
            groups.ToArray());

    /// <summary>
    /// Registers an Aspire project resource as a service node.
    /// </summary>
    /// <param name="resource">
    /// The project resource whose Aspire resource name becomes the node
    /// identifier.
    /// </param>
    /// <param name="displayName">
    /// The name displayed in observability views.
    /// </param>
    /// <param name="healthEntryKey">
    /// The entry in the service health report that represents the service's
    /// direct health.
    /// </param>
    /// <returns>The current topology builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="resource"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="displayName"/> or <paramref name="healthEntryKey"/> is
    /// empty or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A node with the resource's name is already registered.
    /// </exception>
    public TopologyBuilder AddService(
        IResourceBuilder<ProjectResource> resource,
        string displayName,
        string healthEntryKey = SelfHealthEntryKey)
    {
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
    /// Registers a storage node whose direct health is reported by a service.
    /// </summary>
    /// <param name="id">
    /// The stable, unique storage-node identifier.
    /// </param>
    /// <param name="displayName">
    /// The name displayed in observability views.
    /// </param>
    /// <param name="provider">
    /// The registered service whose health report contains the storage entry.
    /// </param>
    /// <param name="healthEntryKey">
    /// The health-report entry that represents the storage resource.
    /// </param>
    /// <returns>The current topology builder.</returns>
    /// <remarks>
    /// The provider must already be registered as a service node.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="provider"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/>, <paramref name="displayName"/>, or
    /// <paramref name="healthEntryKey"/> is empty or whitespace, or
    /// <paramref name="provider"/> is not registered as a service node.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A node with <paramref name="id"/> is already registered.
    /// </exception>
    public TopologyBuilder AddStorage(
        string id,
        string displayName,
        IResourceBuilder<ProjectResource> provider,
        string healthEntryKey)
    {
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
    /// Adds a directed dependency from one registered service to another.
    /// </summary>
    /// <param name="source">
    /// The registered service that owns and reports the dependency.
    /// </param>
    /// <param name="target">
    /// The registered service on which <paramref name="source"/> depends.
    /// </param>
    /// <param name="healthEntryKey">
    /// The optional health-report entry emitted by the source for this
    /// dependency.
    /// </param>
    /// <param name="requirement">
    /// Specifies whether the dependency is required or optional.
    /// </param>
    /// <returns>The current topology builder.</returns>
    /// <remarks>
    /// Both services must already be registered. Only one dependency may exist
    /// for a given source-target pair, regardless of its health entry or
    /// requirement.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="target"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A referenced service is not registered, or
    /// <paramref name="healthEntryKey"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The dependency is a self-reference or its source-target pair is already
    /// registered.
    /// </exception>
    public TopologyBuilder AddDependency(
        IResourceBuilder<ProjectResource> source,
        IResourceBuilder<ProjectResource> target,
        string? healthEntryKey = null,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (healthEntryKey is not null)
        {
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
    /// Adds a directed dependency from a registered service to a registered
    /// non-project node, such as storage.
    /// </summary>
    /// <param name="source">
    /// The registered service that depends on the target node.
    /// </param>
    /// <param name="targetNodeId">
    /// The stable identifier of the registered target node.
    /// </param>
    /// <param name="healthEntryKey">
    /// The optional health-report entry emitted by the source for this
    /// dependency.
    /// </param>
    /// <param name="requirement">
    /// Specifies whether the dependency is required or optional.
    /// </param>
    /// <returns>The current topology builder.</returns>
    /// <remarks>
    /// The source and target must already be registered. Only one dependency
    /// may exist for a given source-target pair, regardless of its health entry
    /// or requirement.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="targetNodeId"/> is empty or whitespace, the source or
    /// target is not registered, or <paramref name="healthEntryKey"/> is empty
    /// or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The dependency is a self-reference or its source-target pair is already
    /// registered.
    /// </exception>
    public TopologyBuilder AddDependency(
        IResourceBuilder<ProjectResource> source,
        string targetNodeId,
        string? healthEntryKey = null,
        TopologyDependencyRequirement requirement =
            TopologyDependencyRequirement.Required)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);

        if (healthEntryKey is not null)
        {
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
    /// Adds a visual group containing registered Aspire project resources.
    /// </summary>
    /// <param name="id">
    /// The stable, unique group identifier.
    /// </param>
    /// <param name="displayName">
    /// The name displayed in observability views.
    /// </param>
    /// <param name="members">
    /// The registered service resources that belong to the group.
    /// </param>
    /// <returns>The current topology builder.</returns>
    /// <remarks>
    /// The group must contain at least one member. Every member must already be
    /// registered as a service node. Duplicate members are included only once.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="members"/> or one of its elements is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="displayName"/> is empty or
    /// whitespace, the group has no members, or a member is not a registered
    /// service.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A group with <paramref name="id"/> is already registered.
    /// </exception>
    public TopologyBuilder AddGroup(
        string id,
        string displayName,
        params IResourceBuilder<ProjectResource>[] members)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(members);

        if (members.Length == 0)
        {
            throw new ArgumentException(
                "A topology group must contain at least one member.",
                nameof(members));
        }

        string[] memberNodeIds = members
            .Select(member =>
            {
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
    /// stable identifiers.
    /// </summary>
    /// <param name="id">
    /// The stable, unique group identifier.
    /// </param>
    /// <param name="displayName">
    /// The name displayed in observability views.
    /// </param>
    /// <param name="memberNodeIds">
    /// The stable identifiers of the registered group members.
    /// </param>
    /// <returns>The current topology builder.</returns>
    /// <remarks>
    /// The group must contain at least one member. Every member must already be
    /// registered. Duplicate member identifiers are included only once.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="memberNodeIds"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/>, <paramref name="displayName"/>, or a member
    /// identifier is empty or whitespace, the group has no members, or a
    /// member identifier is not registered.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A group with <paramref name="id"/> is already registered.
    /// </exception>
    public TopologyBuilder AddGroup(
        string id,
        string displayName,
        IReadOnlyCollection<string> memberNodeIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(memberNodeIds);

        if (memberNodeIds.Count == 0)
        {
            throw new ArgumentException(
                "A topology group must contain at least one member.",
                nameof(memberNodeIds));
        }

        string[] normalizedMemberNodeIds = memberNodeIds
            .Select(nodeId =>
            {
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
    /// Gets the Aspire project resource associated with a topology node.
    /// </summary>
    /// <param name="nodeId">
    /// The stable service-node identifier.
    /// </param>
    /// <returns>The associated Aspire project resource.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="nodeId"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The node is not backed by a registered Aspire project resource.
    /// </exception>
    internal IResourceBuilder<ProjectResource> GetProjectResource(
        string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        if (!projectResources.TryGetValue(
                nodeId,
                out IResourceBuilder<ProjectResource>? resource))
        {
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
        string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        return projectResources.GetValueOrDefault(nodeId);
    }

    /// <summary>
    /// Registers a node while enforcing case-sensitive identifier uniqueness.
    /// </summary>
    /// <param name="node">The topology node to register.</param>
    /// <exception cref="InvalidOperationException">
    /// A node with the same identifier is already registered.
    /// </exception>
    private void RegisterNode(
        TopologyNodeDefinition node)
    {
        if (!nodeIds.Add(node.Id))
        {
            throw new InvalidOperationException(
                $"The topology node ID '{node.Id}' is already registered.");
        }

        nodes.Add(node);
    }

    /// <summary>
    /// Registers a directed dependency while enforcing edge invariants.
    /// </summary>
    /// <param name="edge">The dependency edge to register.</param>
    /// <remarks>
    /// Edge identity consists only of the case-sensitive source-target pair.
    /// Health metadata and requirement do not distinguish otherwise identical
    /// edges.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The edge is a self-dependency or its source-target pair is already
    /// registered.
    /// </exception>
    private void RegisterEdge(
        TopologyEdgeDefinition edge)
    {
        if (string.Equals(
                edge.SourceNodeId,
                edge.TargetNodeId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Topology node '{edge.SourceNodeId}' cannot depend on itself.");
        }

        string edgeKey = string.Concat(
            edge.SourceNodeId,
            EdgeKeySeparator,
            edge.TargetNodeId);

        if (!edgeKeys.Add(edgeKey))
        {
            throw new InvalidOperationException(
                $"The topology dependency '{edge.SourceNodeId}' to " +
                $"'{edge.TargetNodeId}' is already registered.");
        }

        edges.Add(edge);
    }

    /// <summary>
    /// Registers a visual group while enforcing case-sensitive identifier
    /// uniqueness.
    /// </summary>
    /// <param name="group">The topology group to register.</param>
    /// <exception cref="InvalidOperationException">
    /// A group with the same identifier is already registered.
    /// </exception>
    private void RegisterGroup(
        TopologyGroupDefinition group)
    {
        if (!groupIds.Add(group.Id))
        {
            throw new InvalidOperationException(
                $"The topology group ID '{group.Id}' is already registered.");
        }

        groups.Add(group);
    }

    /// <summary>
    /// Verifies that a topology node has already been registered.
    /// </summary>
    /// <param name="nodeId">The case-sensitive node identifier.</param>
    /// <param name="parameterName">
    /// The caller parameter to associate with a validation failure.
    /// </param>
    /// <exception cref="ArgumentException">
    /// No node with <paramref name="nodeId"/> is registered.
    /// </exception>
    private void EnsureNodeIsRegistered(
        string nodeId,
        string parameterName)
    {
        if (!nodeIds.Contains(nodeId))
        {
            throw new ArgumentException(
                $"Topology node '{nodeId}' must be registered before it can " +
                "be referenced.",
                parameterName);
        }
    }

    /// <summary>
    /// Verifies that a service node has already been registered.
    /// </summary>
    /// <param name="nodeId">The case-sensitive service-node identifier.</param>
    /// <param name="parameterName">
    /// The caller parameter to associate with a validation failure.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The node is not registered or is not a service node.
    /// </exception>
    private void EnsureServiceIsRegistered(
        string nodeId,
        string parameterName)
    {
        TopologyNodeDefinition? node = nodes.FirstOrDefault(
            candidate => string.Equals(
                candidate.Id,
                nodeId,
                StringComparison.Ordinal)) ?? throw new ArgumentException(
                $"Topology service '{nodeId}' must be registered before it " +
                "can be referenced.",
                parameterName);

        if (node.Kind != TopologyNodeKind.Service)
        {
            throw new ArgumentException(
                $"Topology node '{nodeId}' is not a service node.",
                parameterName);
        }
    }
}
