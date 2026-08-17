namespace Workbench.Gateway.Internal.Extensions;

using Workbench.Gateway.Internal.Runners;
using Workbench.Gateway.Internal.Runners.Abstraction;

public static class ScenarioRunnerServiceCollectionExtensions {
    public static IServiceCollection AddScenarioRunners(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddSingleton<IScenarioRunner, SingleOrderScenarioRunner>()
            .AddSingleton<IScenarioRunner, ConcurrentOrdersScenarioRunner>()
            .AddSingleton<IScenarioRunner, DuplicateRequestScenarioRunner>()
            .AddSingleton<ScenarioRunnerProvider>();
    }
}
