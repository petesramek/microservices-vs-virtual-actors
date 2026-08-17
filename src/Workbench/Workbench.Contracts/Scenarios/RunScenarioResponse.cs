namespace Workbench.Contracts.Scenarios;

/// <summary>
/// Represents the comparison result returned after executing a workbench
/// scenario.
/// </summary>
/// <param name="Scenario">The scenario that was executed.</param>
/// <param name="Microservices">
/// The microservices execution result.
/// </param>
/// <param name="VirtualActors">
/// The virtual actor execution result.
/// </param>
public sealed record RunScenarioResponse(
    ScenarioKind Scenario,
    ScenarioExecutionResult Microservices,
    ScenarioExecutionResult VirtualActors);
