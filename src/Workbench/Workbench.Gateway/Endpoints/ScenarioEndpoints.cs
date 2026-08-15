namespace Workbench.Gateway.Endpoints;

using Hosting.ServiceDefaults.Observability;
using Hosting.ServiceDefaults.Telemetry;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using Workbench.Contracts;
using Workbench.Gateway.Clients;
using Workbench.Gateway.Logging;
using Workbench.Gateway.Scenarios;

/// <summary>
/// Provides endpoint mappings for running architecture workbench scenarios.
/// </summary>
internal static class ScenarioEndpoints {
    private const string ArchitectureHeader = "X-Architecture";
    private const string BothArchitectures = "both";
    private const string MicroservicesArchitecture = "microservices";
    private const string VirtualActorsArchitecture = "virtual-actors";

    /// <summary>
    /// Maps the scenario execution endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapScenarioEndpoints(
        this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/scenarios/run", RunScenarioAsync);

        return endpoints;
    }

    /// <summary>
    /// Runs a workbench scenario against the architecture selected by the request header.
    /// </summary>
    /// <param name="request">The scenario request.</param>
    /// <param name="httpRequest">The current HTTP request containing the architecture selection.</param>
    /// <param name="scenarioRunner">The scenario runner.</param>
    /// <param name="microservicesClient">The Microservices service client.</param>
    /// <param name="virtualActorsClient">The Virtual Actors service client.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="observabilityOptions">The observability configuration.</param>
    /// <param name="cancellationToken">The token used to cancel scenario execution.</param>
    /// <returns>The scenario result or an error response.</returns>
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

    private static async Task<ScenarioExecutionResult> RunArchitectureAsync(
        ScenarioRunner scenarioRunner,
        IServiceClient serviceClient,
        RunScenarioRequest request,
        CancellationToken cancellationToken) {
        using Activity? activity = ScenarioTelemetry.ActivitySource.StartActivity(
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

    private static Activity? StartScenarioActivity(
        RunScenarioRequest request,
        string architecture) {
        ActivityTagsCollection tags = new() {
            [ScenarioTelemetry.ScenarioRunTagName] = true,
            [ScenarioTelemetry.ScenarioKindTagName] = request.Scenario.ToString(),
            [ScenarioTelemetry.ArchitectureTagName] = architecture,
            [ScenarioTelemetry.ProductIdTagName] = request.ProductId,
            [ScenarioTelemetry.ConcurrentRequestsTagName] = request.ConcurrentRequests,
        };

        return ScenarioTelemetry.ActivitySource.StartActivity(
            ScenarioTelemetry.GetActivityName(request.Scenario.ToString()),
            ActivityKind.Internal,
            default(ActivityContext),
            tags);
    }
}
