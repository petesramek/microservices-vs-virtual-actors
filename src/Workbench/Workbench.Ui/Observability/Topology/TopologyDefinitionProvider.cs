namespace Workbench.Gateway.Observability.Topology;

using System.Text.Json;
using Microsoft.Extensions.Options;
using Workbench.Contracts.Observability.Topology;

/// <summary>
/// Provides the topology definition supplied through application configuration.
/// </summary>
internal sealed class TopologyDefinitionProvider {
    /// <summary>
    /// Initializes a new instance of the <see cref="TopologyDefinitionProvider"/> class.
    /// </summary>
    /// <param name="options">The configured topology options.</param>
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
    /// Gets the configured topology definition.
    /// </summary>
    public TopologyDefinition Definition { get; }
}
