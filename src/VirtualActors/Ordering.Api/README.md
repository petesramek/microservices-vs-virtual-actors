# Ordering.Api

Ordering.Api is the HTTP entry point for the virtual actor implementation in the **Microservices vs Virtual Actors** architecture workbench. It exposes Minimal API endpoints for order placement, order retrieval, inventory reset, and inventory retrieval, then delegates workflow execution to Orleans grains through an `IClusterClient`.

The project does not host grain implementations or own grain-state persistence. Grain behavior belongs to `Ordering.Grains`, the Orleans silo is hosted by `Ordering.Silo`, and SQLite grain storage is provided by `Ordering.Persistence.Sqlite`.

## Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Ordering.Api is the HTTP adapter for the virtual actor path. It translates workbench requests into grain calls and maps grain results back to the shared response contracts used by the scenario runner.

See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The project performs six main tasks:

- Hosts the ASP.NET Core Minimal API application
- Configures an Orleans client for local cluster access
- Maps workbench requests to inventory and order grains
- Converts grain results to shared HTTP response contracts
- Adds correlation information and source-generated structured logging
- Maps shared readiness and liveness endpoints through the service-defaults project

## Startup flow

`Program.cs` keeps application composition concise:

1. Create the `WebApplicationBuilder`.
2. Apply shared service defaults.
3. Configure the Orleans client.
4. Register health-check services.
5. Build the web application.
6. add correlation logging middleware;
7. map ordering endpoints;
8. map shared readiness and liveness endpoints;
9. run until shutdown.

The Orleans client is configured with:

```csharp
builder.UseOrleansClient(clientBuilder => {
    clientBuilder
        .UseLocalhostClustering()
        .AddActivityPropagation();
});
```

`UseLocalhostClustering` is appropriate for the local architecture workbench. It is not a production clustering-discovery strategy.

## Endpoint organization

Application endpoints are registered by:

```csharp
app.MapOrderingEndpoints();
```

`EndpointRouteBuilderExtensions` owns route mapping, handlers, grain calls, result conversion, and endpoint-specific logging. Keeping these handlers outside `Program.cs` preserves a clear boundary between host composition and HTTP behavior.

The API routes are grouped under `/api`, while the root service-information endpoint remains at `/`.

## Endpoint reference

### Service information

```http
GET /
```

Returns identifying information for the application:

```json
{
  "name": "Ordering API",
  "phase": "Virtual Actors"
}
```

### Reset inventory

```http
POST /api/scenarios/reset
```

Accepts a `ResetInventoryRequest`, resolves `IInventoryItemGrain` by product ID, resets its available quantity, and returns an `InventoryResponse`.

Successful requests return HTTP 200. Unexpected failures are logged and mapped to HTTP 500. Caller-requested cancellation is propagated.

### Get inventory

```http
GET /api/inventory/{productId}
```

Resolves `IInventoryItemGrain` by string product ID and returns its current `InventorySnapshot` as an `InventoryResponse`.

Successful requests return HTTP 200. Unexpected failures are logged and mapped to HTTP 500. Caller-requested cancellation is propagated.

### Place order

```http
POST /api/orders
```

Accepts a `RunScenarioRequest`, resolves `IOrderGrain` by GUID order ID, and forwards:

- idempotency key
- customer ID
- product ID
- quantity
- payment-failure simulation flag

The resulting `GrainOrderResult` is converted to the shared `OrderResponse` contract. Successful calls return HTTP 200. Unexpected failures are logged and mapped to HTTP 500. Caller-requested cancellation is propagated.

### Get order

```http
GET /api/orders/{orderId:guid}
```

Resolves `IOrderGrain` by GUID order ID and retrieves its current result.

- HTTP 200 is returned when an order result is available
- HTTP 404 is returned when the grain has no current result
- HTTP 500 is returned for unexpected retrieval failures
- Caller-requested cancellation is propagated

## HTTP and grain contract boundary

The API depends on two contract sets:

- `Ordering.Grains.Contracts` for grain-call results and snapshots
- `Workbench.Contracts` for public scenario requests and HTTP responses

The API does not expose grain persistence state directly. It maps:

```text
InventorySnapshot
    -> InventoryResponse

GrainOrderResult
    -> OrderResponse
```

This keeps Orleans-specific serialization contracts separate from the shared workbench HTTP contracts.

## Order-status conversion

The API converts the grain result status string to `OrderStatus`:

```csharp
Enum.Parse<OrderStatus>(result.Status)
```

The current implementation relies on `Ordering.Grains` to produce valid status names. If the status source becomes external or independently versioned, replace this with explicit `Enum.TryParse` handling and define the appropriate HTTP failure contract.

## Correlation logging

The request pipeline reads the optional header:

```text
X-Correlation-ID
```

When the header contains a non-blank value, the API:

- creates a logging scope with the structured `CorrelationId` property
- emits an informational request-handling event
- keeps the scope active through the remaining request pipeline

Requests without a correlation identifier continue without creating the scope.

The header value is used for correlation only. It must not be treated as authenticated identity or authorization data.

## Structured logging

The project uses source-generated logging methods in:

```text
Internal/Observability/Logging/LogInformation.cs
Internal/Observability/Logging/LogError.cs
```

Informational events cover:

- request correlation
- inventory reset start and completion
- inventory retrieval
- order placement start and completion
- order retrieval
- order-not-found responses

Error events cover:

- inventory reset failures
- inventory retrieval failures
- order placement failures
- order retrieval failures

Event IDs are allocated from log-level-specific ranges. Message-template placeholders use stable PascalCase property names for structured telemetry.

Do not log secrets, credentials, full request bodies, or sensitive customer data. Product identifiers, order identifiers, quantities, statuses, and correlation identifiers are the current operational context carried by these events.

## Health endpoints

Shared service defaults map:

```text
/health
/alive
```

`/health` is the readiness endpoint and evaluates all registered checks.

`/alive` is the liveness endpoint and evaluates checks tagged for process liveness by the shared defaults.

The API currently registers the health-check service collection but does not define a project-local dependency check in the supplied project structure.

## Activity propagation

`AddActivityPropagation` enables trace context to flow from incoming API work into Orleans grain calls. This supports correlation between the HTTP request, client-side Orleans activity, and grain execution in the Silo.

Shared tracing, metrics, logging, service discovery, resilience, and exporter behavior are configured through `AddServiceDefaults` rather than duplicated in this project.

## Configuration

`appsettings.json` contains project configuration consumed by ASP.NET Core and shared service defaults.

`Properties/launchSettings.json` contains local launch profiles.

Environment-specific values should be supplied through the normal ASP.NET Core configuration providers. Do not commit secrets or credentials to either file.

## Local development

The API requires a reachable local Orleans silo configured for the same cluster. Start the repository through its AppHost when using the complete architecture workbench, or start the Silo before running the API directly.

From the Ordering.Api project directory:

```console
dotnet run
```

From the repository root:

```console
dotnet run --project <path-to-Ordering.Api.csproj>
```

Local URLs are defined by `Properties/launchSettings.json` or runtime configuration.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

API changes should cover at least:

- successful inventory reset
- successful inventory retrieval
- successful order placement
- successful order retrieval
- order-not-found behavior
- Orleans client or grain-call failures
- cancellation propagation
- order-status conversion
- correlation scope creation with and without the header
- structured log event IDs and property names
- readiness and liveness endpoints
- route constraints and request binding

## Adding or changing endpoints

When modifying this project:

- Keep host composition in `Program.cs` concise
- Add ordering routes through `EndpointRouteBuilderExtensions`
- Keep `/api` routes in the existing route group
- Use grain interfaces rather than grain implementations
- Keep Orleans contracts separate from public workbench responses
- Propagate `OperationCanceledException` rather than converting cancellation to HTTP 500
- Log unexpected failures before returning a problem response
- Preserve structured message templates and stable event IDs
- Avoid logging sensitive request data
- Apply route constraints where identifiers have a defined format
- Update this README when routes, responses, correlation, health checks, or Orleans client configuration change

## Naming conventions

- Endpoint registration types use the `Extensions` suffix
- Endpoint registration methods use the `Map` prefix
- Async route handlers use the `Async` suffix
- Grain interfaces use the `I` prefix and `Grain` suffix
- Public workbench contracts use request and response suffixes
- Source-generated logging classes are grouped by log level
- Structured logging placeholders use PascalCase
- Route parameters use camelCase

## Scope

Ordering.Api is the HTTP adapter for the virtual actor ordering showcase. It does not implement grain behavior, own persistence state, host the Orleans silo, configure production cluster discovery, define authentication or authorization policy, or provide a production security and deployment model.

Those responsibilities belong to `Ordering.Grains`, `Ordering.Silo`, `Ordering.Persistence.Sqlite`, shared hosting projects, and deployment infrastructure.
