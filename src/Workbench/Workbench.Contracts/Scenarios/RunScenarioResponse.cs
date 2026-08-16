namespace Workbench.Contracts.Scenarios;

/// <summary>
/// Represents the comparison result returned after executing a workbench
/// scenario.
/// </summary>
/// <param name="Scenario">The scenario that was executed.</param>
/// <param name="Microservices">
/// The microservices execution result, or <see langword="null"/> when that
/// architecture was not requested or did not produce a result.
/// </param>
/// <param name="VirtualActors">
/// The virtual actor execution result, or <see langword="null"/> when that
/// architecture was not requested or did not produce a result.
/// </param>
/// <remarks>
/// The nullable architecture results allow one response contract to represent
/// a single-architecture run or a side-by-side comparison.
/// </remarks>
public sealed record RunScenarioResponse(
    ScenarioKind Scenario,
    ScenarioExecutionResult? Microservices,
    ScenarioExecutionResult? VirtualActors);
