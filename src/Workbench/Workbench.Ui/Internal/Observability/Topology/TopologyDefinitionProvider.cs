namespace Workbench.Ui.Internal.Observability.Topology;

using global::Observability.Topology.Definitions;
using Microsoft.Extensions.Options;
using System.Text.Json;

/// <summary>
/// Provides the graph topology definition supplied through application
/// configuration.
/// </summary>
internal sealed class TopologyDefinitionProvider {
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="TopologyDefinitionProvider"/> class.
    /// </summary>
    /// <param name="options">
    /// The configured topology options.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The topology definition is missing or cannot be deserialized.
    /// </exception>
    public TopologyDefinitionProvider(
        IOptions<TopologyOptions> options) {
        ArgumentNullException.ThrowIfNull(options);

        string serializedDefinition = options.Value.TopologyDefinition
            ?? throw new InvalidOperationException(
                "The observability topology definition is not configured.");

        Definition = JsonSerializer.Deserialize<TopologyDefinition>(
            serializedDefinition)
            ?? throw new InvalidOperationException(
                "The observability topology definition is invalid.");
    }

    /// <summary>
    /// Gets the configured graph topology definition.
    /// </summary>
    public TopologyDefinition Definition { get; }
}
