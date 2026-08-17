namespace Workbench.Gateway.Internal.Runners;

using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Runners.Abstraction;

/// <summary>
/// Resolves scenario runners by scenario kind.
/// </summary>
internal sealed class ScenarioRunnerProvider {
    /// <summary>
    /// Stores registered runners indexed by scenario kind.
    /// </summary>
    private readonly IReadOnlyDictionary<ScenarioKind, IScenarioRunner> _runners;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioRunnerProvider"/>
    /// class.
    /// </summary>
    /// <param name="runners">The registered scenario runners.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="runners"/> is <see langword="null"/>, or contains a
    /// <see langword="null"/> runner.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Multiple runners support the same scenario kind.
    /// </exception>
    public ScenarioRunnerProvider(IEnumerable<IScenarioRunner> runners) {
        ArgumentNullException.ThrowIfNull(runners);

        Dictionary<ScenarioKind, IScenarioRunner> registeredRunners = [];

        foreach (IScenarioRunner runner in runners.Where(r => r is not null)) {
            foreach (ScenarioKind scenario in runner.SupportedScenarios) {
                if (!registeredRunners.TryAdd(scenario, runner)) {
                    throw new InvalidOperationException(
                        $"Multiple runners are registered for scenario "
                            + $"'{scenario}'.");
                }
            }
        }

        _runners = registeredRunners;
    }

    /// <summary>
    /// Gets the runner registered for a scenario kind.
    /// </summary>
    /// <param name="scenario">The scenario kind to resolve.</param>
    /// <returns>The runner registered for the scenario kind.</returns>
    /// <exception cref="NotSupportedException">
    /// No runner supports <paramref name="scenario"/>.
    /// </exception>
    public IScenarioRunner GetRunner(ScenarioKind scenario) {
        if (_runners.TryGetValue(scenario, out IScenarioRunner? runner)) {
            return runner;
        }

        throw new NotSupportedException(
            $"No runner is registered for scenario '{scenario}'.");
    }
}
