namespace Observability.Topology.Snapshots;

using System.Collections.ObjectModel;

/// <summary>
/// Represents an immutable point-in-time snapshot of the evaluated topology.
/// </summary>
/// <remarks>
/// Node, edge, and group order is preserved for deterministic serialization
/// and presentation. The supplied collections are copied during construction
/// so later changes to the source collections do not affect the snapshot.
/// </remarks>
public sealed record TopologySnapshot {
    /// <summary>
    /// Initializes a new instance of the <see cref="TopologySnapshot"/> class.
    /// </summary>
    /// <param name="generatedAtUtc">
    /// The UTC timestamp at which the topology snapshot was generated.
    /// </param>
    /// <param name="nodes">
    /// The evaluated topology node snapshots in presentation order.
    /// </param>
    /// <param name="edges">
    /// The evaluated topology edge snapshots in presentation order.
    /// </param>
    /// <param name="groups">
    /// The evaluated topology group snapshots in presentation order.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="nodes"/>, <paramref name="edges"/>, or
    /// <paramref name="groups"/> is <see langword="null"/>.
    /// </exception>
    public TopologySnapshot(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<TopologyNodeSnapshot> nodes,
        IReadOnlyList<TopologyEdgeSnapshot> edges,
        IReadOnlyList<TopologyGroupSnapshot> groups) {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(groups);

        GeneratedAtUtc = generatedAtUtc;
        Nodes = Snapshot(nodes);
        Edges = Snapshot(edges);
        Groups = Snapshot(groups);
    }

    /// <summary>
    /// Gets the UTC timestamp at which the topology snapshot was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary>
    /// Gets an ordered snapshot of the evaluated topology nodes.
    /// </summary>
    public IReadOnlyList<TopologyNodeSnapshot> Nodes { get; }

    /// <summary>
    /// Gets an ordered snapshot of the evaluated topology edges.
    /// </summary>
    public IReadOnlyList<TopologyEdgeSnapshot> Edges { get; }

    /// <summary>
    /// Gets an ordered snapshot of the evaluated topology groups.
    /// </summary>
    public IReadOnlyList<TopologyGroupSnapshot> Groups { get; }

    /// <summary>
    /// Creates an ordered, read-only snapshot of a collection.
    /// </summary>
    /// <typeparam name="T">The snapshot element type.</typeparam>
    /// <param name="items">The source items to copy.</param>
    /// <returns>An ordered, read-only copy of <paramref name="items"/>.</returns>
    private static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> items) {
        return new ReadOnlyCollection<T>(items.ToArray());
    }
}
