namespace Workbench.Gateway.Internal.Endpoints;

using Hosting.ServiceDefaults.Observability;
using Hosting.ServiceDefaults.Observability.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using Workbench.Contracts;
using Workbench.Contracts.Scenarios;
using Workbench.Gateway.Internal.Clients;
using Workbench.Gateway.Internal.Clients.Abstraction;
using Workbench.Gateway.Internal.Scenarios;
using Workbench.Gateway.Logging;

/// <summary>
/// Provides endpoint mappings for running architecture workbench scenarios.
/// </summary>
internal static class ScenarioEndpoints {
    /// <summary>
    /// Identifies the request header used to select architecture implementations.
    /// </summary>
    private const string ArchitectureHeader = "X-Architecture";

    /// <summary>
    /// Identifies a request to run both architecture implementations.
    /// </summary>
    private const string BothArchitectures = "both";

    /// <summary>
    /// Identifies a request to run only the microservices implementation.
    /// </summary>
    private const string MicroservicesArchitecture = "microservices";

    /// <summary>
    /// Identifies a request to run only the virtual actor implementation.
    /// </summary>
    private const string VirtualActorsArchitecture = "virtual-actors";

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
    /// <param name="httpRequest">
    /// The HTTP request containing the optional architecture-selection header.
    /// </param>
    /// <param name="scenarioRunner">The scenario execution coordinator.</param>
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
    /// An HTTP 200 response containing the selected execution results, HTTP 400
    /// for an unsupported architecture value, or HTTP 500 for an unexpected
    /// execution failure.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled while a scenario is
    /// running.
    /// </exception>
    private static async Task<IResult> RunScenarioAsync(
        RunScenarioRequest request,
        HttpRequest httpRequest,
        ScenarioRunner scenarioRunner,
        MicroservicesServiceClient microservicesClient,
        VirtualActorsServiceClient virtualActorsClient,
        ILoggerFactory loggerFactory,
        IOptions<ObservabilityOptions> observabilityOptions,
        CancellationToken cancellationToken) {
        string architecture = httpRequest.Headers.TryGetValue(
            ArchitectureHeader,
            out StringValues values)
            ? values.FirstOrDefault() ?? BothArchitectures
            : BothArchitectures;

        bool runMicroservices = architecture.Equals(
                MicroservicesArchitecture,
                StringComparison.OrdinalIgnoreCase)
            || architecture.Equals(
                BothArchitectures,
                StringComparison.OrdinalIgnoreCase);
        bool runVirtualActors = architecture.Equals(
                VirtualActorsArchitecture,
                StringComparison.OrdinalIgnoreCase)
            || architecture.Equals(
                BothArchitectures,
                StringComparison.OrdinalIgnoreCase);

        ILogger logger = loggerFactory.CreateLogger("Workbench.Gateway");

        if (!runMicroservices && !runVirtualActors) {
            logger.UnsupportedArchitectureRequested(architecture);

            return Results.BadRequest(new {
                Error = "Unsupported X-Architecture value. Use microservices, virtual-actors, or both.",
            });
        }

        bool createScenarioRoot = observabilityOptions.Value.TraceMode
            == TraceCollectionMode.ScenarioOnly;
        Activity? parentActivity = Activity.Current;

        if (createScenarioRoot) {
            Activity.Current = null;
        }

        Activity? activity = StartScenarioActivity(
            request,
            architecture);

        if (createScenarioRoot && activity is null) {
            Activity.Current = parentActivity;
        }

        try {
            logger.StartingScenario(request.Scenario, architecture);

            ScenarioExecutionResult? microservices = null;
            ScenarioExecutionResult? virtualActors = null;

            if (runMicroservices && runVirtualActors) {
                Task<ScenarioExecutionResult> microservicesTask =
                    RunArchitectureAsync(
                        scenarioRunner,
                        microservicesClient,
                        request,
                        cancellationToken);
                Task<ScenarioExecutionResult> virtualActorsTask =
                    RunArchitectureAsync(
                        scenarioRunner,
                        virtualActorsClient,
                        request,
                        cancellationToken);

                ScenarioExecutionResult[] results = await Task.WhenAll(
                    microservicesTask,
                    virtualActorsTask).ConfigureAwait(false);

                microservices = await microservicesTask.ConfigureAwait(false);
                virtualActors = await virtualActorsTask.ConfigureAwait(false);
            } else if (runMicroservices) {
                microservices = await RunArchitectureAsync(
                    scenarioRunner,
                    microservicesClient,
                    request,
                    cancellationToken).ConfigureAwait(false);
            } else {
                virtualActors = await RunArchitectureAsync(
                    scenarioRunner,
                    virtualActorsClient,
                    request,
                    cancellationToken).ConfigureAwait(false);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            logger.ScenarioCompleted(
                scenarioKind: request.Scenario,
                architecture: architecture,
                microservicesExecuted: microservices is not null,
                virtualActorsExecuted: virtualActors is not null);

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
                request.Scenario,
                architecture);

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
        ScenarioRunner scenarioRunner,
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        using Activity? activity = ScenarioInstrumentation.ActivitySource.StartActivity(
            $"Architecture: {serviceClient.Name}",
            ActivityKind.Internal);

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
        }
    }

    /// <summary>
    /// Starts the root activity used to trace a scenario execution.
    /// </summary>
    /// <param name="request">
    /// The scenario request that provides activity name and tag values.
    /// </param>
    /// <param name="architecture">
    /// The normalized architecture selection recorded on the activity.
    /// </param>
    /// <returns>
    /// The started activity, or <see langword="null"/> when no listener samples
    /// the activity.
    /// </returns>
    private static Activity? StartScenarioActivity(
        RunScenarioRequest request,
        string architecture) {
        ActivityTagsCollection tags = new() {
            [ScenarioInstrumentation.TagNames.ScenarioRun] = true,
            [ScenarioInstrumentation.TagNames.ScenarioKind] = request.Scenario.ToString(),
            [ScenarioInstrumentation.TagNames.Architecture] = architecture,
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
