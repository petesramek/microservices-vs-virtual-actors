namespace Workbench.Gateway.Internal.Runners.Abstraction;

using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Clients.Abstraction;

/// <summary>
/// Defines a workflow runner for one or more supported scenario kinds.
/// </summary>
internal interface IScenarioRunner {
    /// <summary>
    /// Gets the scenario kinds supported by the runner.
    /// </summary>
    /// <value>The immutable set of supported scenario kinds.</value>
    IReadOnlySet<ScenarioKind> SupportedScenarios { get; }

    /// <summary>
    /// Executes a supported scenario through an architecture service client.
    /// </summary>
    /// <param name="serviceClient">
    /// The architecture service client used to execute scenario operations.
    /// </param>
    /// <param name="request">The scenario request to execute.</param>
    /// <param name="cancellationToken">
    /// The token that cancels scenario execution.
    /// </param>
    /// <returns>
    /// A task whose result contains the normalized scenario outcome.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serviceClient"/> or <paramref name="request"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The runner does not support <see cref="RunScenarioRequest.Scenario"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the scenario is
    /// running.
    /// </exception>
    Task<ScenarioExecutionResult> RunAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken);
}
