namespace Workbench.Ui.Internal.Observability.Topology;

/// <summary>
/// Defines configuration used to load the workbench topology.
/// </summary>
internal sealed class TopologyOptions {
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Observability";

    /// <summary>
    /// Gets the serialized topology definition supplied by the AppHost.
    /// </summary>
    public string? TopologyDefinition { get; init; }
}
