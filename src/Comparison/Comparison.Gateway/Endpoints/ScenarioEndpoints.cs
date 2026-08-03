namespace Comparison.Gateway.Endpoints;

using Comparison.Contracts;
using Comparison.Gateway.Clients;
using Comparison.Gateway.Logging;
using Comparison.Gateway.Scenarios;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Provides endpoint mappings for running architecture comparison scenarios.
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
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/scenarios/run", RunScenarioAsync);

        return endpoints;
    }

    /// <summary>
    /// Runs a comparison scenario against the architecture selected by the request header.
    /// </summary>
    /// <param name="request">The scenario request.</param>
    /// <param name="httpRequest">The current HTTP request containing the architecture selection.</param>
    /// <param name="scenarioRunner">The scenario runner.</param>
    /// <param name="microservicesClient">The Microservices service client.</param>
    /// <param name="virtualActorsClient">The Virtual Actors service client.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="cancellationToken">The token used to cancel scenario execution.</param>
    /// <returns>The scenario result or an error response.</returns>
    private static async Task<IResult> RunScenarioAsync(
        RunScenarioRequest request,
        HttpRequest httpRequest,
        ScenarioRunner scenarioRunner,
        MicroservicesServiceClient microservicesClient,
        VirtualActorsServiceClient virtualActorsClient,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        string architecture = httpRequest.Headers.TryGetValue(ArchitectureHeader, out StringValues values)
            ? values.FirstOrDefault() ?? BothArchitectures
            : BothArchitectures;

        bool runMicroservices = architecture.Equals(MicroservicesArchitecture, StringComparison.OrdinalIgnoreCase)
            || architecture.Equals(BothArchitectures, StringComparison.OrdinalIgnoreCase);
        bool runVirtualActors = architecture.Equals(VirtualActorsArchitecture, StringComparison.OrdinalIgnoreCase)
            || architecture.Equals(BothArchitectures, StringComparison.OrdinalIgnoreCase);

        ILogger logger = loggerFactory.CreateLogger("Comparison.Gateway");

        if (!runMicroservices && !runVirtualActors) {
            logger.UnsupportedArchitectureRequested(architecture);

            return Results.BadRequest(new {
                Error = "Unsupported X-Architecture value. Use microservices, virtual-actors, or both.",
            });
        }

        logger.StartingScenario(request.Scenario, architecture);

        try {
            ScenarioExecutionResult? microservices = null;
            ScenarioExecutionResult? virtualActors = null;

            if (runMicroservices && runVirtualActors) {
                Task<ScenarioExecutionResult> microservicesTask = scenarioRunner.RunAsync(
                    microservicesClient,
                    request,
                    cancellationToken);
                Task<ScenarioExecutionResult> virtualActorsTask = scenarioRunner.RunAsync(
                    virtualActorsClient,
                    request,
                    cancellationToken);

                await Task.WhenAll(
                    microservicesTask,
                    virtualActorsTask).ConfigureAwait(false);

                microservices = await microservicesTask.ConfigureAwait(false);
                virtualActors = await virtualActorsTask.ConfigureAwait(false);
            }
            else if (runMicroservices) {
                microservices = await scenarioRunner
                    .RunAsync(microservicesClient, request, cancellationToken)
                    .ConfigureAwait(false);
            }
            else {
                virtualActors = await scenarioRunner
                    .RunAsync(virtualActorsClient, request, cancellationToken)
                    .ConfigureAwait(false);
            }

            logger.ScenarioCompleted(
                scenarioKind: request.Scenario,
                architecture: architecture,
                microservicesExecuted: microservices is not null,
                virtualActorsExecuted: virtualActors is not null);

            return Results.Ok(new RunScenarioResponse(
                request.Scenario,
                microservices,
                virtualActors));
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception exception) {
            logger.ScenarioExecutionFailed(exception, request.Scenario, architecture);

            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
