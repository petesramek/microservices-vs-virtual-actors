namespace Workbench.Gateway.Internal.Endpoints;

using Hosting.ServiceDefaults.Observability;
using Hosting.ServiceDefaults.Observability.Configuration;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Clients;
using Workbench.Gateway.Internal.Clients.Abstraction;
using Workbench.Gateway.Internal.Runners;
using Workbench.Gateway.Internal.Runners.Abstraction;
using Workbench.Gateway.Logging;

/// <summary>
/// Provides endpoint mappings for running architecture workbench scenarios.
/// </summary>
internal static class ScenarioEndpoints {
    /// <summary>
    /// Maps the scenario execution endpoint.
    /// </summary>
    /// <param name="endpoints">
    /// The endpoint route builder that receives the scenario route.
    /// </param>
    /// <returns>
    /// <paramref name="endpoints"/> so additional endpoints can be mapped.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> is <see langword="null"/>.
    /// </exception>
    public static IEndpointRouteBuilder MapScenarioEndpoints(
        this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/scenarios/run", RunScenarioAsync);

        return endpoints;
    }

    /// <summary>
    /// Runs a workbench scenario against the architecture implementations
    /// selected by the request header.
    /// </summary>
    /// <param name="request">The scenario execution request.</param>
    /// <param name="scenarioRunnerProvider">The scenario runner provider.</param>
    /// <param name="microservicesClient">
    /// The client for the microservices implementation.
    /// </param>
    /// <param name="virtualActorsClient">
    /// The client for the virtual actor implementation.
    /// </param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="observabilityOptions">
    /// The observability options controlling scenario-root activity creation.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that cancels scenario execution.
    /// </param>
    /// <returns>
    /// An HTTP 200 response containing the selected execution results
    /// , or HTTP 500 for an unexpected execution failure.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while a scenario is
    /// running.
    /// </exception>
    private static async Task<IResult> RunScenarioAsync(
        RunScenarioRequest request,
        ScenarioRunnerProvider scenarioRunnerProvider,
        MicroservicesServiceClient microservicesClient,
        VirtualActorsServiceClient virtualActorsClient,
        ILoggerFactory loggerFactory,
        IOptions<ObservabilityOptions> observabilityOptions,
        CancellationToken cancellationToken) {
        ILogger logger = loggerFactory.CreateLogger("Workbench.Gateway");

        var runner = scenarioRunnerProvider.GetRunner(request.Scenario);

        bool createScenarioRoot = observabilityOptions.Value.TraceMode == TraceCollectionMode.ScenarioOnly;
        Activity? parentActivity = Activity.Current;

        if (createScenarioRoot) {
            Activity.Current = null;
        }

        Activity? activity = StartScenarioActivity(request);

        if (createScenarioRoot && activity is null) {
            Activity.Current = parentActivity;
        }

        try {

            ScenarioExecutionResult microservices;
            ScenarioExecutionResult virtualActors;

            Task<ScenarioExecutionResult> microservicesTask =
                RunArchitectureAsync(
                    runner,
                    microservicesClient,
                    request,
                    logger,
                    cancellationToken);
            Task<ScenarioExecutionResult> virtualActorsTask =
                RunArchitectureAsync(
                    runner,
                    virtualActorsClient,
                    request,
                    logger,
                    cancellationToken);

            ScenarioExecutionResult[] results = await Task.WhenAll(
                microservicesTask,
                virtualActorsTask)
                .ConfigureAwait(false);

            microservices = await microservicesTask
                .ConfigureAwait(false);
            virtualActors = await virtualActorsTask
                .ConfigureAwait(false);

            activity?.SetStatus(ActivityStatusCode.Ok);

            return Results.Ok(new RunScenarioResponse(
                request.Scenario,
                microservices,
                virtualActors));
        } catch (OperationCanceledException) {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                "Scenario execution was canceled.");
            throw;
        } catch (Exception exception) {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            logger.ScenarioExecutionFailed(
                exception,
                request.Scenario);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError);
        } finally {
            activity?.Dispose();

            if (createScenarioRoot) {
                Activity.Current = parentActivity;
            }
        }
    }

    /// <summary>
    /// Executes a scenario against one architecture implementation and records
    /// the execution status on a child activity.
    /// </summary>
    /// <param name="scenarioRunner">The scenario execution coordinator.</param>
    /// <param name="serviceClient">
    /// The client for the architecture implementation being executed.
    /// </param>
    /// <param name="request">The scenario execution request.</param>
    /// <param name="logger"></param>
    /// <param name="cancellationToken">
    /// The token that cancels architecture execution.
    /// </param>
    /// <returns>
    /// A task whose result contains the architecture execution outcome.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while the architecture
    /// is executing.
    /// </exception>
    private static async Task<ScenarioExecutionResult> RunArchitectureAsync(
        IScenarioRunner scenarioRunner,
        IServiceClient serviceClient,
        RunScenarioRequest request,
        ILogger logger,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(scenarioRunner);
        ArgumentNullException.ThrowIfNull(serviceClient);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(logger);

        using Activity? activity = ScenarioInstrumentation.ActivitySource.StartActivity(
            $"Architecture: {serviceClient.Name}",
            ActivityKind.Internal);

        logger.StartingScenario(serviceClient.Name, request.Scenario);

        try {
            ScenarioExecutionResult result = await scenarioRunner
                .RunAsync(serviceClient, request, cancellationToken)
                .ConfigureAwait(false);

            activity?.SetStatus(ActivityStatusCode.Ok);

            return result;
        } catch (OperationCanceledException) {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                $"{serviceClient.Name} execution was canceled.");
            throw;
        } catch (Exception exception) {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);
            throw;
        } finally {
            logger.ScenarioCompleted(serviceClient.Name, request.Scenario);
        }
    }

    /// <summary>
    /// Starts the root activity used to trace a scenario execution.
    /// </summary>
    /// <param name="request">
    /// The scenario request that provides activity name and tag values.
    /// </param>
    /// <returns>
    /// The started activity, or <see langword="null"/> when no listener samples
    /// the activity.
    /// </returns>
    private static Activity? StartScenarioActivity(
        RunScenarioRequest request) {
        ActivityTagsCollection tags = new() {
            [ScenarioInstrumentation.TagNames.ScenarioRun] = true,
            [ScenarioInstrumentation.TagNames.ScenarioKind] = request.Scenario.ToString(),
            [ScenarioInstrumentation.TagNames.ProductId] = request.ProductId,
            [ScenarioInstrumentation.TagNames.ConcurrentRequests] = request.ConcurrentRequests,
        };

        return ScenarioInstrumentation.ActivitySource.StartActivity(
            ScenarioInstrumentation.GetActivityName(request.Scenario.ToString()),
            ActivityKind.Internal,
            default(ActivityContext),
            tags);
    }
}
