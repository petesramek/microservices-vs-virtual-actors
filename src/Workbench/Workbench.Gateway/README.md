# Workbench.Gateway

Workbench.Gateway is the scenario execution boundary for the **Microservices vs Virtual Actors** architecture workbench. It exposes an ASP.NET Core API, routes scenario requests to one or both architecture implementations, prepares deterministic scenario inputs, aggregates comparable results, and adds correlation, metrics, tracing, health, and structured logging behavior.

The gateway does not own order, inventory, or payment state. It coordinates calls to the microservices Orders API and the virtual actor Ordering API through dedicated HTTP clients, then returns a shared `RunScenarioResponse` for the UI or other callers.

## Repository context

The repository implements the same ordering workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Workbench.Gateway provides a common entry point for running the same deterministic scenario against either implementation or both implementations in parallel. It normalizes orchestration at the workbench boundary without hiding the architectural differences described by each result timeline.

See the repository-level README and docs directory for the scenario guide, architecture discussions, topology, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The project performs nine main tasks:

- Hosts the ASP.NET Core gateway application
- Exposes the shared scenario execution endpoint
- Calls the configured backend APIs through dedicated service clients
- Prepares deterministic inventory, payment, concurrency, and idempotency inputs
- Aggregates architecture-specific execution results and explanatory timelines
- Creates and propagates correlation identifiers
- Records scenario metrics, activities, and source-generated structured logs
- Exposes shared readiness and liveness endpoints

## Startup flow

`Program.cs` performs application composition:

- creates the `WebApplicationBuilder`
- applies shared service defaults
- registers standardized problem details
- binds and validates `ServiceEndpointOptions` during startup
- registers the microservices and virtual actor HTTP clients
- registers health-check services
- registers `ScenarioRunner` and `ScenarioMetrics` as singletons
- builds the web application
- adds correlation identifier middleware
- adds the exception handler
- maps service information, scenario, readiness, and liveness endpoints
- runs until shutdown

Configuration validation uses data annotations and `ValidateOnStart`. Missing or invalid backend URLs therefore prevent the application from starting instead of failing on the first scenario request.

Keep `Program.cs` focused on composition. Backend HTTP behavior belongs in clients, scenario preparation belongs in `ScenarioRunner`, endpoint behavior belongs in `ScenarioEndpoints`, and correlation handling belongs in its application-builder extension.

## Endpoint reference

# Service information

```http
GET /
```

Returns identifying information for the gateway:

```json
{
  "name": "Workbench Gateway",
  "description": "Routes scenario requests."
}
```

# Run scenario

```http
POST /api/scenarios/run
```

Accepts a `RunScenarioRequest` and returns a `RunScenarioResponse` containing the selected architecture results.

An unsupported value returns HTTP 400 with an explanatory error. Unexpected execution failures are logged and returned as HTTP 500 problem responses. Caller-requested cancellation is propagated.

# Health and liveness

Shared service defaults expose:

```http
GET /health
GET /alive
```

`/health` represents readiness. `/alive` represents process liveness. The gateway currently registers health-check services and can add caller-specific downstream dependency checks when backend availability should determine readiness.

## Service clients

Gateway backend access is organized around a common service-client abstraction:

```text
Clients/IServiceClient.cs
Clients/HttpServiceClient.cs
Clients/MicroservicesServiceClient.cs
Clients/VirtualActorsServiceClient.cs
```

`IServiceClient` defines the operations required by scenario execution. `HttpServiceClient` owns shared HTTP request and response behavior. The two concrete clients provide architecture identity and explanatory timelines.

`MicroservicesServiceClient` communicates with the microservices Orders API. Its timeline describes interactions among:

- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`

`VirtualActorsServiceClient` communicates with the virtual actor Ordering API. Its timeline describes interactions among:

- `Ordering.Api`
- `OrderGrain`
- `InventoryItemGrain`
- `PaymentAccountGrain`

The timelines are explanatory workbench output. They are not a replacement for distributed traces and should not contain credentials, personal data, secrets, or raw request bodies.

## Scenario execution

`ScenarioRunner` prepares and executes deterministic scenarios through an `IServiceClient`.

It supports three execution shapes:

- a single order submission
- multiple concurrent orders with distinct identities
- concurrent duplicate submissions of the same logical order

The runner resets inventory before each execution, performs the architecture request or requests, reads final inventory, records elapsed time, records workflow metrics, and constructs a `ScenarioExecutionResult`.

The runner dispatches:

- `ConcurrentOrders` and `HotProductContention` to concurrent-order execution
- `DuplicateRequest` to duplicate-request execution
- all other scenarios to single-order execution

## Deterministic scenario preparation

Before execution, `ScenarioRunner` adjusts input values for deterministic behavior:

- `SuccessfulOrder` ensures stock is at least the order quantity and disables simulated payment failure
- `InsufficientInventory` limits stock to less than the requested quantity and disables simulated payment failure
- `PaymentFailureCompensation` ensures sufficient stock and enables simulated payment failure
- `PaymentTimeoutAfterReservation` ensures sufficient stock and enables the downstream failure path used by the workbench timeout scenario
- concurrency scenarios preserve requested stock while preventing simulated payment failure
- duplicate-request execution ensures at least two submissions and enough stock for one logical order

The original request object is not mutated. Prepared values are produced through record copies.

## Concurrent orders

For concurrent-order and hot-product scenarios, the gateway creates the configured number of order requests with:

- a new `OrderId` for each submission
- a distinct idempotency key suffix for each submission
- the same product identifier and requested quantity

The runner awaits all submissions, counts completed and rejected orders, reads final inventory, and creates an aggregate architecture-specific timeline.

The result reports:

- total request submissions
- completed orders
- rejected orders
- remaining inventory
- elapsed milliseconds
- explanatory events

## Duplicate requests

For the duplicate-request scenario, all concurrent submissions reuse the same prepared request, including its order identifier and idempotency key.

The runner derives:

- the number of unique completed order identifiers
- whether one unique rejected logical result exists
- the number of responses resolved through idempotent replay

The `IdempotentResponses` value is calculated from total submissions minus unique logical outcomes. This workbench calculation depends on backend idempotency semantics and should remain covered by architecture-specific tests.

## Payment timeout presentation

The payment-timeout scenario uses the observed order and inventory values but presents the terminal reason as:

```text
PaymentTimeout
```

The gateway creates a dedicated timeline showing reservation, timeout, compensation, and the final inventory quantity. This is workbench presentation behavior. The actual timeout, cancellation, retry, and compensation guarantees remain responsibilities of the selected backend implementation.

## Correlation identifiers

`UseCorrelationId` reads the optional request and response header:

```text
X-Correlation-ID
```

When the request supplies a non-blank value, the gateway reuses it. Otherwise, it generates a value in this form:

```text
run-<compact-guid>
```

The middleware:

- writes the resolved identifier to the response header
- stores it in the current asynchronous execution context
- adds it to a structured logging scope as `CorrelationId`
- clears the ambient value after the downstream pipeline completes

The correlation identifier is operational metadata only. It must not be treated as authenticated identity, authorization data, or a trusted business identifier.

## Tracing

`ScenarioEndpoints` creates a scenario activity with tags for:

- scenario-run identity
- scenario kind
- architecture selection
- product identifier
- concurrent request count

Each selected architecture runs within its own child activity named from the service-client implementation name.

Successful execution marks activities as successful. Cancellation and unexpected failures mark the relevant activity as an error before propagating or returning the error response.

When trace collection mode is `ScenarioOnly`, the gateway temporarily clears the current parent activity before starting the scenario root. It restores the previous activity after completion.

Do not add secrets, customer data, complete request bodies, or high-cardinality unrestricted values to activity tags.

## Metrics

`ScenarioMetrics` is registered as a singleton and used by `ScenarioRunner` to record workflow duration.

Each completed runner path records:

- elapsed workflow duration
- architecture implementation name
- scenario kind

Metric names, dimensions, and units are telemetry contracts. Avoid adding unbounded identifiers, order IDs, customer IDs, idempotency keys, or correlation IDs as metric dimensions.

## Structured logging

The project groups source-generated logging by level:

```text
Observability/Logging/LogInformation.cs
Observability/Logging/LogWarning.cs
Observability/Logging/LogError.cs
```

Informational events cover:

- scenario execution start
- scenario execution completion
- which architecture implementations executed

Warning events cover unsupported architecture selections.

Error events cover unexpected scenario execution failures.

Event IDs derive from the log level and remain stable within each class. Message-template placeholders use PascalCase structured property names. Preserve event IDs, message templates, parameter order, and structured property names when changing log definitions.

Do not log credentials, backend URLs containing secrets, complete request bodies, or other sensitive configuration values.

## Configuration

`ServiceEndpointOptions` binds the configuration section:

```text
ServiceEndpoints
```

It requires two valid backend URLs:

```text
ServiceEndpoints:MicroservicesBaseUrl
ServiceEndpoints:VirtualActorsBaseUrl
```

`MicroservicesBaseUrl` configures `MicroservicesServiceClient`. `VirtualActorsBaseUrl` configures `VirtualActorsServiceClient`.

Both values are required and validated as URLs during startup. Environment-specific values should be supplied through standard ASP.NET Core configuration providers. Do not commit secrets, credentials, access tokens, or private endpoints intended only for restricted environments.

`appsettings.json` contains project configuration. `Properties/launchSettings.json` contains local launch profiles.

## Error handling

The gateway registers problem details and enables the ASP.NET Core exception handler.

`ScenarioEndpoints` handles its expected HTTP outcomes directly:

- HTTP 200 for successful selected execution
- HTTP 500 problem response for an unexpected scenario execution failure

`OperationCanceledException` is not converted into a generic internal-server error. It is marked on the current activity and rethrown so ASP.NET Core can observe request cancellation correctly.

## Local development

Workbench.Gateway depends at runtime on the selected architecture backends:

- the microservices Orders API for `microservices` runs
- the virtual actor Ordering API for `virtual-actors` runs
- both backends for comparison runs

The preferred way to run the complete workbench is through the repository AppHost so endpoints, service discovery, observability, and environment variables are configured consistently.

From the Workbench.Gateway project directory:

```console
dotnet run
```

From the repository root:

```console
dotnet run --project <path-to-Workbench.Gateway.csproj>
```

Local URLs are defined by `Properties/launchSettings.json` or runtime configuration. Ensure the configured backend URLs match the services available in the current environment.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Gateway changes should cover at least:

- startup failure for missing endpoint configuration
- startup failure for invalid backend URLs
- default execution against both architectures
- microservices-only execution
- virtual-actors-only execution
- case-insensitive architecture values
- unsupported architecture rejection
- successful-order preparation
- insufficient-inventory preparation
- payment-failure compensation preparation
- payment-timeout result presentation
- concurrent-order aggregation
- hot-product contention aggregation
- duplicate-request logical-result and idempotent-response counting
- backend transport and invalid-response failures
- cancellation propagation
- parallel execution when both architectures are selected
- correlation reuse and generation
- correlation response headers and ambient cleanup
- scenario-root and architecture activity status
- workflow metrics and bounded dimensions
- stable logging event IDs and structured property names
- readiness and liveness endpoints

## Adding or changing scenario behavior

When modifying this project:

- Keep host composition in `Program.cs` concise
- Keep endpoint selection and HTTP result behavior in `ScenarioEndpoints`
- Keep deterministic input preparation and result aggregation in `ScenarioRunner`
- Keep backend transport behind `IServiceClient` and `HttpServiceClient`
- Keep architecture-specific timeline wording in the concrete service clients
- Preserve cancellation propagation
- Keep both-architecture execution parallel unless the comparison requires ordered execution
- Use record copies instead of mutating incoming scenario requests
- Preserve stable log event IDs and message-template properties
- Avoid high-cardinality metric dimensions and sensitive activity tags
- Keep dependency checks on readiness rather than liveness
- Update this README and shared contracts when scenario fields or results change

## Naming conventions

- Client abstractions use the `I` prefix and `Client` suffix
- HTTP client base and implementations use the `ServiceClient` suffix
- Async operations use the `Async` suffix
- Endpoint registration types use the `Endpoints` suffix
- Application-builder extensions use the `ApplicationBuilderExtensions` suffix
- Configuration binding types use the `Options` suffix
- Source-generated logging classes are grouped by log level
- Structured logging placeholders use PascalCase
- HTTP header names use canonical hyphenated casing
- Route parameters use camelCase

## Scope

Workbench.Gateway demonstrates a shared scenario boundary for comparing two architecture implementations. It is not a production API gateway, service mesh, authentication boundary, authorization policy, durable workflow engine, benchmark harness, load-testing platform, distributed transaction coordinator, or disaster-recovery design.

Scenario elapsed time is useful for workbench observation but is not a controlled performance benchmark. Production use would require independent decisions for authentication, authorization, rate limiting, quotas, retries, timeouts, circuit breaking, durable execution, security, deployment, scaling, monitoring, and recovery.
