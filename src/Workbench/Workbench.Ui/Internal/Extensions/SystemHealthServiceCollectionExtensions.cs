namespace Workbench.Ui.Internal.Extensions;

using global::Observability.Health;
using global::Observability.Health.Abstraction;
using global::Observability.Topology.Evaluators;
using global::Observability.Topology.Evaluators.Abstraction;
using Workbench.Ui.Internal.Observability.Health;
using Workbench.Ui.Internal.Observability.Health.Builders;
using Workbench.Ui.Internal.Observability.Health.Configuration;
using Workbench.Ui.Internal.Observability.Health.Probing;

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
            .AddOptions<SystemHealthOptions>()
            .Bind(configuration.GetSection(
                SystemHealthOptions.SectionName))
            .Validate(
                options => options.HealthEndpoints.Count > 0,
                "At least one health endpoint must be configured.")
             .Validate(
                options => options.AliveEndpoints.Count > 0,
                "At least one alive endpoint must be configured.")
            .ValidateOnStart();

        services
            .AddSingleton(TimeProvider.System)
            .AddSingleton<TopologySnapshotBuilder>()
            .AddSingleton<IHealthStatusEvaluator, HealthStatusEvaluator>()
            .AddSingleton<IGroupHealthEvaluator, GroupHealthEvaluator>()
            .AddSingleton<IDependencyHealthEvaluator, DependencyHealthEvaluator>();

        services.AddHttpClient<ServiceHealthProbe>(client => {
            client.Timeout = HealthRequestTimeout;
        });

        services
            .AddScoped<SystemHealthService>();

        return services;
    }
}