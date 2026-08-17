namespace Hosting.AppHost.Observability.Topology;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using global::Observability.Health.Abstraction;
using global::Observability.Topology.Definitions;
using global::Observability.Topology.Validation;
using Hosting.AppHost.Resources;
using System.Text.Json;

/// <summary>
/// Provides extensions that register an observability topology and expose its
/// definition and service endpoints to a topology-provider project resource.
/// </summary>
/// <remarks>
/// The generated environment-variable names use double underscores to model
/// hierarchical .NET configuration keys.
/// </remarks>
internal static class TopologyResourceExtensions {
    /// <summary>
    /// Identifies the environment variable that contains the serialized
    /// topology definition.
    /// </summary>
    private const string TopologyConfigurationName =
        "Observability__TopologyDefinition";

    /// <summary>
    /// Identifies the configuration-key prefix for service health endpoints.
    /// </summary>
    private const string HealthEndpointConfigurationPrefix =
        "Observability__HealthEndpoints";

    /// <summary>
    /// Identifies the configuration-key prefix for service liveness endpoints.
    /// </summary>
    private const string AliveEndpointConfigurationPrefix =
        "Observability__AliveEndpoints";

    /// <summary>
    /// Identifies the named HTTP endpoint used to construct health and
    /// liveness endpoint references.
    /// </summary>
    private const string HttpEndpointName = "http";

    /// <summary>
    /// Registers one graph topology for Aspire grouping and Workbench
    /// observability collection.
    /// </summary>
    /// <param name="builder">
    /// The distributed application builder.
    /// </param>
    /// <param name="healthStatusEvaluator">
    /// The evaluator used by the generated Aspire health groups.
    /// </param>
    /// <param name="topologyProvider">
    /// The project resource that receives the serialized topology and
    /// service endpoint configuration.
    /// </param>
    /// <param name="configure">
    /// Configures topology nodes, dependency edges, and visual groups.
    /// </param>
    /// <returns>
    /// A validated snapshot of the configured neutral topology definition.
    /// </returns>
    /// <remarks>
    /// The completed topology is validated before any Aspire health groups or
    /// environment variables are registered. Visual groups are registered as
    /// Aspire health groups when they contain at least one project-backed
    /// member. The serialized definition and each service's <c>http</c> health
    /// and liveness endpoint references are added to
    /// <paramref name="topologyProvider"/> as environment variables.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="healthStatusEvaluator"/>,
    /// <paramref name="topologyProvider"/>, or <paramref name="configure"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The configured topology is invalid, or a registered service does not
    /// expose the named <c>http</c> endpoint required for endpoint references.
    /// </exception>
    public static TopologyDefinition AddTopology(
        this IDistributedApplicationBuilder builder,
        IHealthStatusEvaluator healthStatusEvaluator,
        IResourceBuilder<ProjectResource> topologyProvider,
        Action<TopologyBuilder> configure) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(healthStatusEvaluator);
        ArgumentNullException.ThrowIfNull(topologyProvider);
        ArgumentNullException.ThrowIfNull(configure);

        TopologyBuilder topology = new();
        configure(topology);

        TopologyDefinition definition = topology.Definition;
        EnsureValid(definition);

        AddAspireHealthGroups(
            builder,
            topology,
            definition.Groups,
            healthStatusEvaluator);

        topologyProvider.WithEnvironment(
            TopologyConfigurationName,
            JsonSerializer.Serialize(definition));

        AddServiceEndpointConfiguration(
            topologyProvider,
            topology,
            definition.Nodes);

        return definition;
    }

    /// <summary>
    /// Validates a completed topology before it is published to Aspire
    /// resources.
    /// </summary>
    /// <param name="definition">The topology definition to validate.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="definition"/> contains one or more validation errors.
    /// </exception>
    private static void EnsureValid(TopologyDefinition definition) {
        TopologyValidator validator = new();
        TopologyValidationResult result = TopologyValidator.Validate(definition);

        if (result.IsValid) {
            return;
        }

        string errors = string.Join(
            Environment.NewLine,
            result.Errors.Select(static error => $"- {error}"));

        throw new InvalidOperationException(
            "The observability topology is invalid:"
            + Environment.NewLine
            + errors);
    }

    /// <summary>
    /// Registers topology groups as Aspire health groups.
    /// </summary>
    /// <param name="builder">
    /// The distributed application builder that receives the health groups.
    /// </param>
    /// <param name="topology">
    /// The topology used to resolve project-backed group members.
    /// </param>
    /// <param name="groups">The visual groups to register.</param>
    /// <param name="healthStatusEvaluator">
    /// The evaluator used by the generated health groups.
    /// </param>
    /// <remarks>
    /// Non-project members are ignored. Groups without any project-backed
    /// members are not registered, and duplicate project resources are added
    /// only once per health group.
    /// </remarks>
    private static void AddAspireHealthGroups(
        IDistributedApplicationBuilder builder,
        TopologyBuilder topology,
        IReadOnlyCollection<TopologyGroupDefinition> groups,
        IHealthStatusEvaluator healthStatusEvaluator) {
        foreach (TopologyGroupDefinition group in groups) {
            IResourceBuilder<ProjectResource>[] resources = group.NodeIds
                .Select(topology.TryGetProjectResource)
                .Where(static resource => resource is not null)
                .Cast<IResourceBuilder<ProjectResource>>()
                .DistinctBy(
                    static resource => resource.Resource.Name,
                    StringComparer.Ordinal)
                .ToArray();

            if (resources.Length == 0) {
                continue;
            }

            builder.AddHealthGroup(
                healthStatusEvaluator,
                group.Id,
                group.DisplayName,
                resources);
        }
    }

    /// <summary>
    /// Adds health and liveness endpoint references for each service node to
    /// the topology-provider resource.
    /// </summary>
    /// <param name="topologyProvider">
    /// The project resource that receives the endpoint environment variables.
    /// </param>
    /// <param name="topology">
    /// The topology used to resolve each service's project resource.
    /// </param>
    /// <param name="nodes">The topology nodes to inspect.</param>
    /// <remarks>
    /// Non-service nodes are ignored. Service resources must expose an endpoint
    /// named <c>http</c>. Health and liveness paths are fixed to
    /// <c>/health</c> and <c>/alive</c>, respectively.
    /// </remarks>
    private static void AddServiceEndpointConfiguration(
        IResourceBuilder<ProjectResource> topologyProvider,
        TopologyBuilder topology,
        IReadOnlyCollection<TopologyNodeDefinition> nodes) {
        foreach (TopologyNodeDefinition node in nodes) {
            if (node.Kind != TopologyNodeKind.Service) {
                continue;
            }

            IResourceBuilder<ProjectResource> resource =
                topology.GetProjectResource(node.Id);

            topologyProvider.WithEnvironment(
                CreateConfigurationName(
                    HealthEndpointConfigurationPrefix,
                    node.Id),
                ReferenceExpression.Create(
                    $"{resource.GetEndpoint(HttpEndpointName)}/health"));

            topologyProvider.WithEnvironment(
                CreateConfigurationName(
                    AliveEndpointConfigurationPrefix,
                    node.Id),
                ReferenceExpression.Create(
                    $"{resource.GetEndpoint(HttpEndpointName)}/alive"));
        }
    }

    /// <summary>
    /// Creates a hierarchical environment-variable name for a topology node.
    /// </summary>
    /// <param name="prefix">The configuration-key prefix.</param>
    /// <param name="nodeId">The stable topology node identifier.</param>
    /// <returns>
    /// The prefix and node identifier joined with the .NET configuration
    /// hierarchy delimiter <c>__</c>.
    /// </returns>
    private static string CreateConfigurationName(
        string prefix,
        string nodeId) {
        return string.Join(
            "__",
            prefix,
            nodeId);
    }
}
