# Hosting.ServiceDefaults

`Hosting.ServiceDefaults` provides the shared hosting conventions used by the services in the **Microservices vs Virtual Actors** architecture workbench. It centralizes service discovery, outbound HTTP resilience, health endpoints, OpenTelemetry configuration, and scenario-specific telemetry so participating applications use the same operational defaults.

The project is infrastructure-focused. It does not contain order-processing domain logic or scenario result contracts.

## What the project provides

Calling `AddServiceDefaults()` on an application builder configures:

- strongly typed and startup-validated observability options
- OpenTelemetry logging, metrics, and tracing
- OTLP export when an exporter endpoint is configured
- a default self-health check
- Aspire service discovery
- standard resilience and service discovery for `HttpClient` instances

Calling `MapDefaultEndpoints()` on a `WebApplication` maps the shared readiness and liveness endpoints in every environment.

The project also exposes shared APIs for scenario activities and workflow metrics.

## Consumer setup

Reference `Hosting.ServiceDefaults` from a participating ASP.NET Core project, then apply the defaults during startup:

```csharp
using Hosting.ServiceDefaults.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.Run();
```

`AddServiceDefaults()` applies registration-time configuration. `MapDefaultEndpoints()` must be called after the application is built if the default health routes should be available.

## Shared hosting defaults

### Service discovery

`AddServiceDefaults()` registers Aspire service discovery and enables it for the default `HttpClient` configuration. Consumers can therefore use logical service endpoints supplied by the AppHost rather than hard-coded local addresses.

### HTTP resilience

The default `HttpClient` configuration includes the standard resilience handler. Individual clients can add more specific policies when their failure semantics differ from the shared baseline.

### OpenTelemetry logging

OpenTelemetry logging includes:

- formatted messages
- logging scopes

Export is configured separately through the OTLP environment setting described below.

## Observability configuration

Configuration is bound from the `Observability` section and validated when the application starts.

```json
{
  "Observability": {
    "TraceMode": "ScenarioOnly",
    "TraceSources": "Scenario",
    "MetricSources": "Scenario"
  }
}
```

JSON does not support comments. The supported values are documented below.

If the section is absent, the option type defaults to:

```json
{
  "Observability": {
    "TraceMode": "Full",
    "TraceSources": "All",
    "MetricSources": "All"
  }
}
```

### `TraceMode`

| Value | Behavior |
| --- | --- |
| `Full` | Collects traces from the configured trace sources, except health endpoint requests, which are always filtered from ASP.NET Core tracing. |
| `ScenarioOnly` | Uses scenario-root sampling and filters ASP.NET Core and outbound HTTP instrumentation to scenario-related requests. Parent-based sampling retains distributed descendants of sampled scenario roots. |

In `ScenarioOnly` mode, an inbound request is eligible for ASP.NET Core tracing when either condition is true:

- the path starts with `/api/scenarios/run`
- the request contains the `X-Scenario-Run` header

An outbound HTTP request is eligible when it carries the `X-Scenario-Run` header. Eligibility filters instrumentation, while the sampler makes the final record-and-sample decision for root activities.

### `TraceSources`

`TraceSources` is a flags enum. Named values can be combined using the configuration binder's enum format when multiple sources are required. Combinations containing unsupported bits fail startup validation.

| Value | Trace instrumentation or source |
| --- | --- |
| `None` | Adds none of the optional trace sources listed below. |
| `AspNetCore` | ASP.NET Core server request instrumentation. |
| `HttpClient` | Outbound HTTP client instrumentation. |
| `EntityFrameworkCore` | Entity Framework Core instrumentation. |
| `MicrosoftOrleans` | Activity sources matching `Microsoft.Orleans.*`. |
| `Scenario` | The `Scenario.Workflows` activity source. |
| `All` | Selects every supported trace source. |

The current application activity source, named from `Environment.ApplicationName`, is always registered independently of `TraceSources`.

### `MetricSources`

`MetricSources` is also a flags enum. Named values can be combined, and combinations containing unsupported bits fail startup validation.

| Value | Metric instrumentation or meter |
| --- | --- |
| `None` | Adds none of the optional metric sources. |
| `Runtime` | .NET runtime instrumentation. |
| `AspNetCore` | ASP.NET Core instrumentation. |
| `HttpClient` | HTTP client instrumentation. |
| `EntityFrameworkCore` | The `Microsoft.EntityFrameworkCore` meter. |
| `MicrosoftOrleans` | The `Microsoft.Orleans` meter. |
| `Scenario` | The `Scenario.Workflows` meter. |
| `All` | Selects all metric sources. |

### Configuration validation

`TraceMode`, `TraceSources`, and `MetricSources` are validated during startup. Unsupported enum values prevent the application from starting, which makes configuration errors visible before the service begins handling work.

## Scenario tracing

`ScenarioInstrumentation` defines the shared tracing and metrics contract for workbench scenario runs.

| Member | Value or purpose |
| --- | --- |
| Activity source | `Scenario.Workflows` |
| Scenario request header | `X-Scenario-Run` |
| Scenario header marker | `true` |
| Scenario root tag | `scenario.run` |
| Scenario kind tag | `scenario.kind` |
| Product identifier tag | `scenario.product.id` |
| Concurrent request tag | `scenario.concurrent_requests` |

Scenario activities should be created from `ScenarioInstrumentation.ActivitySource`. Activity names follow the format `Scenario: {scenario}`.

A sampled scenario root must include the `scenario.run` tag with the Boolean value `true` when sampling occurs. `ScenarioTraceSampler` samples such roots and drops other roots. In `ScenarioOnly` mode it is wrapped by `ParentBasedSampler`, allowing sampled parent decisions to propagate to distributed descendants.

Example:

```csharp
using System.Diagnostics;
using Hosting.ServiceDefaults.Observability;

TagList tags = new()
{
    { ScenarioInstrumentation.TagNames.ScenarioRun, true },
    { ScenarioInstrumentation.TagNames.ScenarioKind, scenarioKind },
};

using Activity? activity = ScenarioInstrumentation.ActivitySource.StartActivity(
    ScenarioInstrumentation.GetActivityName(scenarioKind),
    ActivityKind.Internal,
    default(ActivityContext),
    tags);
```

When a scenario crosses an HTTP boundary, propagate the marker header expected by the filters:

```csharp
request.Headers.Add(
    ScenarioInstrumentation.Headers.ScenarioRun,
    ScenarioInstrumentation.Headers.ScenarioRunValue);
```

## Scenario metrics

`ScenarioMetrics` uses the `Scenario.Workflows` meter and records workflow runs that reached a terminal state.

| Instrument | Type | Unit | Meaning |
| --- | --- | --- | --- |
| `workflow.run.count` | Counter | `{run}` | Number of workflow runs that reached a terminal state. |
| `workflow.run.duration` | Histogram | `s` | Duration of terminal workflow runs in seconds. |

Both instruments use bounded dimensions:

- `scenario.kind`

Register `ScenarioMetrics` through dependency injection in consumer projects that record workflow completion:

```csharp
builder.Services.AddSingleton<ScenarioMetrics>();
```

Record a terminal run with:

```csharp
scenarioMetrics.RecordWorkflowRun(
    elapsed,
    scenarioKind);
```

`duration` must be non-negative. `scenarioKind` must be nonempty. Keep both tag values bounded, do not use order IDs, customer IDs, product IDs, or other high-cardinality values as metric dimensions.

## Health endpoints

`AddDefaultHealthChecks()` registers a healthy `self` check tagged with `live`.

`MapDefaultEndpoints()` exposes the routes in every application environment:

| Endpoint | Purpose | Response behavior |
| --- | --- | --- |
| `/health` | Readiness and dependency health | Runs all registered health checks and writes the shared structured health-report JSON. |
| `/alive` | Process aliveness | Runs only checks tagged `live` and uses the framework's default health-check response. |

The `/health` response maps framework health states to the shared observability health contract and reports durations in rounded-up milliseconds. Enum values are serialized as strings.

Health endpoint requests are excluded from ASP.NET Core tracing in both trace modes to avoid routine probe traffic adding trace noise.

Because the endpoints are mapped in every environment, consuming applications and deployment infrastructure should apply any required network or access controls outside this shared showcase configuration.

## OTLP export

When `OTEL_EXPORTER_OTLP_ENDPOINT` contains a nonempty value, the project enables the OpenTelemetry OTLP exporter:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317
```

When the setting is absent or empty, no exporter is added by this project. Instrumentation can still be registered, but telemetry requires an exporter supplied elsewhere to leave the process.

Use environment-specific configuration or an approved secret-management mechanism for exporter settings that include sensitive headers or credentials. Do not commit credentials to source control.

## Public API summary

### Hosting extensions

```csharp
builder.AddServiceDefaults();
builder.ConfigureOpenTelemetry();
builder.AddDefaultHealthChecks();
app.MapDefaultEndpoints();
```

Most consumers should call only `AddServiceDefaults()` and `MapDefaultEndpoints()`. The narrower registration methods are public for applications that need to compose the defaults selectively.

### Observability APIs

- `ObservabilityOptions`
- `TraceCollectionMode`
- `TraceSource`
- `MetricSource`
- `ScenarioInstrumentation`
- `ScenarioTraceSampler`
- `ScenarioMetrics`

## Maintenance guidance

When extending this project:

1. Keep it limited to cross-cutting hosting and observability defaults.
2. Avoid adding workbench domain models or workflow behavior.
3. Make new instrumentation opt-in through the appropriate source flags when it can add material telemetry volume.
4. Keep metric dimensions bounded and stable.
5. Preserve the scenario header and tag names as telemetry contracts across services.
6. Update option validation, source enums, configuration examples, and this README together.
7. Keep health endpoints inexpensive and free of sensitive data.
8. Verify both `Full` and `ScenarioOnly` behavior when changing tracing filters or sampling.
9. Ensure a new enum flag has a corresponding configuration branch, declared but unused flags are misleading.

## Scope

This project configures in-process telemetry and local hosting defaults. It does not provision a telemetry backend, define production alerting, provide authentication or authorization for health routes, or establish environment-specific deployment policy. Those concerns belong to the consuming applications and their deployment configuration.
