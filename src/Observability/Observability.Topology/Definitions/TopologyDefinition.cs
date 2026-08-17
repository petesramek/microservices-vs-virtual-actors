namespace Observability.Topology.Definitions;

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines an immutable snapshot of the complete static observability topology.
/// </summary>
/// <remarks>
/// Node, edge, and group order is preserved for deterministic serialization and
/// presentation. Group membership contributes to visual organization and
/// aggregate health but does not imply dependency direction. Graph invariants
/// are enforced by the topology validator rather than by this transport
/// contract.
/// </remarks>
public sealed record TopologyDefinition {
    /// <summary>
    /// Initializes a new instance of the <see cref="TopologyDefinition"/> class.
    /// </summary>
    /// <param name="nodes">The topology nodes in definition order.</param>
    /// <param name="edges">
    /// The directed dependency edges in definition order.
    /// </param>
    /// <param name="groups">
    /// The visual and health-aggregation groups in definition order.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="nodes"/>, <paramref name="edges"/>, or
    /// <paramref name="groups"/> is <see langword="null"/>.
    /// </exception>
    public TopologyDefinition(
        IReadOnlyList<TopologyNodeDefinition> nodes,
        IReadOnlyList<TopologyEdgeDefinition> edges,
        IReadOnlyList<TopologyGroupDefinition> groups) {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(groups);

        Nodes = Snapshot(nodes);
        Edges = Snapshot(edges);
        Groups = Snapshot(groups);
    }

    /// <summary>
    /// Gets an ordered snapshot of the topology nodes.
    /// </summary>
    public IReadOnlyList<TopologyNodeDefinition> Nodes { get; }

    /// <summary>
    /// Gets an ordered snapshot of the directed dependency edges.
    /// </summary>
    public IReadOnlyList<TopologyEdgeDefinition> Edges { get; }

    /// <summary>
    /// Gets an ordered snapshot of the visual and health-aggregation groups.
    /// </summary>
    public IReadOnlyList<TopologyGroupDefinition> Groups { get; }

    /// <summary>
    /// Creates an ordered, read-only snapshot of a definition collection.
    /// </summary>
    /// <typeparam name="T">The definition element type.</typeparam>
    /// <param name="items">The source items to copy.</param>
    /// <returns>An ordered, read-only snapshot of <paramref name="items"/>.</returns>
    [SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification = "Prioritizing design clarity, encapsulation, and abstractions over micro-optimization.")]
    private static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> items) {
        return new ReadOnlyCollection<T>(items.ToArray());
    }
}
