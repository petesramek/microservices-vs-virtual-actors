namespace Observability.Topology.Definitions;

using System.Collections.ObjectModel;

/// <summary>
/// Defines a visual group whose health is aggregated from its member nodes.
/// </summary>
/// <remarks>
/// Group membership affects presentation and aggregate health only; it does not
/// define dependency direction. The supplied node identifiers are copied in
/// their original order so the definition remains stable after construction.
/// Graph invariants are enforced by the topology validator.
/// </remarks>
public sealed record TopologyGroupDefinition {
    /// <summary>
    /// Initializes a new instance of the <see cref="TopologyGroupDefinition"/>
    /// class.
    /// </summary>
    /// <param name="id">
    /// The stable identifier used to reference and serialize the group.
    /// </param>
    /// <param name="displayName">
    /// The user-facing name displayed in topology views.
    /// </param>
    /// <param name="nodeIds">
    /// The ordered identifiers of nodes included in the group.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="nodeIds"/> is <see langword="null"/>.
    /// </exception>
    public TopologyGroupDefinition(
        string id,
        string displayName,
        IReadOnlyList<string> nodeIds) {
        ArgumentNullException.ThrowIfNull(nodeIds);

        Id = id;
        DisplayName = displayName;
        NodeIds = new ReadOnlyCollection<string>(nodeIds.ToArray());
    }

    /// <summary>
    /// Gets the stable identifier used to reference and serialize the group.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the user-facing name displayed in topology views.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets an ordered snapshot of the node identifiers included in the group.
    /// </summary>
    public IReadOnlyList<string> NodeIds { get; }
}
