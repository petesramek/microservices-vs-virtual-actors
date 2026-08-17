namespace Workbench.Ui.Internal.Extensions;

using global::Observability.Health;
using global::Observability.Health.Abstraction;
using global::Observability.Topology.Evaluators;
using global::Observability.Topology.Evaluators.Abstraction;
using Workbench.Ui.Internal.Observability.Health;

/// <summary>
/// Provides registration methods for Workbench system health services.
/// </summary>
internal static class SystemHealthServiceCollectionExtensions {
    private static readonly TimeSpan HealthRequestTimeout =
        TimeSpan.FromSeconds(2);

    /// <summary>
    /// Adds the graph-oriented server-side system health collector and its
    /// dependencies.
    /// </summary>
    /// <param name="services">
    /// The service collection.
    /// </param>
    /// <param name="configuration">
    /// The application configuration containing service health and alive
    /// endpoint mappings supplied by AppHost.
    /// </param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSystemHealth(
        this IServiceCollection services,
        IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<HealthEndpointOptions>()
            .Bind(configuration.GetSection(
                HealthEndpointOptions.SectionName))
            .Validate(
                options => options.Count > 0,
                "At least one health endpoint must be configured.")
            .ValidateOnStart();

        services
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IHealthStatusEvaluator, HealthStatusEvaluator>()
            .AddSingleton<IGroupHealthEvaluator, GroupHealthEvaluator>()
            .AddSingleton<IDependencyHealthEvaluator, DependencyHealthEvaluator>();

        services.AddHttpClient<SystemHealthService>(client => {
            client.Timeout = HealthRequestTimeout;
        });

        return services;
    }
}