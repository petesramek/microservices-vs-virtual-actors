namespace Workbench.Gateway.Internal.Runners.Abstraction;

using Hosting.ServiceDefaults.Observability.Metrics;
using System.Diagnostics;
using Workbench.Contracts.Inventory;
using Workbench.Contracts.Orders;
using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Clients.Abstraction;

/// <summary>
/// Provides the common execution template for deterministic workbench
/// scenarios.
/// </summary>
internal abstract class ScenarioRunner : IScenarioRunner {
    private readonly ScenarioMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioRunner"/> class.
    /// </summary>
    /// <param name="metrics">The scenario metrics recorder.</param>
    protected ScenarioRunner(ScenarioMetrics metrics) {
        _metrics = metrics
            ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <summary>
    /// Gets the scenario kinds supported by this runner.
    /// </summary>
    public abstract IReadOnlySet<ScenarioKind> SupportedScenarios { get; }

    /// <summary>
    /// Executes a supported scenario through an architecture service client.
    /// </summary>
    /// <param name="serviceClient">
    /// The architecture service client.
    /// </param>
    /// <param name="request">The scenario request.</param>
    /// <param name="cancellationToken">
    /// The token that cancels scenario execution.
    /// </param>
    /// <returns>The normalized scenario execution result.</returns>
    public async Task<ScenarioExecutionResult> RunAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(serviceClient);
        ArgumentNullException.ThrowIfNull(request);

        EnsureScenarioIsSupported(request.Scenario);

        RunScenarioRequest effectiveRequest =
            PrepareRequest(request);

        await serviceClient
            .ResetInventoryAsync(
                effectiveRequest.ProductId,
                effectiveRequest.InitialStock,
                cancellationToken)
            .ConfigureAwait(false);

        Stopwatch stopwatch = Stopwatch.StartNew();

        OrderResponse[] responses = await SubmitOrdersAsync(
            serviceClient,
            effectiveRequest,
            cancellationToken).ConfigureAwait(false);

        InventoryResponse inventory = await serviceClient
            .GetInventoryAsync(
                effectiveRequest.ProductId,
                cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        _metrics.RecordWorkflowRun(
            stopwatch.Elapsed,
            serviceClient.Name,
            request.Scenario.ToString());

        return CreateResult(
            serviceClient,
            effectiveRequest,
            responses,
            inventory,
            stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Applies scenario-specific execution rules to a request.
    /// </summary>
    protected abstract RunScenarioRequest PrepareRequest(
        RunScenarioRequest request);

    /// <summary>
    /// Submits the order requests required by the scenario.
    /// </summary>
    protected abstract Task<OrderResponse[]> SubmitOrdersAsync(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates the normalized scenario result.
    /// </summary>
    protected abstract ScenarioExecutionResult CreateResult(
        IServiceClient serviceClient,
        RunScenarioRequest request,
        IReadOnlyList<OrderResponse> responses,
        InventoryResponse inventory,
        long elapsedMilliseconds);

    private void EnsureScenarioIsSupported(ScenarioKind scenario) {
        if (!SupportedScenarios.Contains(scenario)) {
            throw new NotSupportedException(
                $"{GetType().Name} does not support scenario '{scenario}'.");
        }
    }
}