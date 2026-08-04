namespace Microsoft.Extensions.DependencyInjection;

using Workbench.Ui.Observability.Health;

/// <summary>
/// Provides registration methods for Workbench system health services.
/// </summary>
internal static class SystemHealthServiceCollectionExtensions {
    private static readonly TimeSpan HealthRequestTimeout =
        TimeSpan.FromSeconds(2);

    /// <summary>
    /// Adds the server-side system health collector.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
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

        services.AddSingleton(TimeProvider.System);

        services.AddHttpClient<SystemHealthService>(client => {
            client.Timeout = HealthRequestTimeout;
        });

        return services;
    }
}
