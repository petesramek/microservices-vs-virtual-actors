namespace Workbench.Gateway.Internal.Extensions;

using Workbench.Gateway.Internal.Runners;
using Workbench.Gateway.Internal.Runners.Abstraction;

/// <summary>
/// Provides dependency-injection registration for scenario runner services.
/// </summary>
public static class ScenarioRunnerServiceCollectionExtensions {
    /// <summary>
    /// Registers the scenario-specific runners and the provider that resolves a
    /// runner for a requested scenario.
    /// </summary>
    /// <param name="services">
    /// The service collection to which the scenario runner services are added.
    /// </param>
    /// <returns>
    /// The same service collection so that additional registrations can be
    /// chained.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddScenarioRunners(
        this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddSingleton<IScenarioRunner, SingleOrderScenarioRunner>()
            .AddSingleton<IScenarioRunner, ConcurrentOrdersScenarioRunner>()
            .AddSingleton<IScenarioRunner, DuplicateRequestScenarioRunner>()
            .AddSingleton<ScenarioRunnerProvider>();
    }
}
