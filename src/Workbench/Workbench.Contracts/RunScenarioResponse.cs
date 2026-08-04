namespace Workbench.Contracts;

/// <summary>
/// Represents the response returned after running a workbench scenario.
/// </summary>
/// <param name="Scenario">The scenario that was run.</param>
/// <param name="Microservices">The Microservices execution result when requested.</param>
/// <param name="VirtualActors">The Virtual Actors execution result when requested.</param>
public sealed record RunScenarioResponse(
    ScenarioKind Scenario,
    ScenarioExecutionResult? Microservices,
    ScenarioExecutionResult? VirtualActors);
