# Orders.Api

Orders.Api is the order orchestration service for the microservices implementation in the **Microservices vs Virtual Actors** architecture workbench. It hosts an ASP.NET Core API, persists order workflow state to SQLite through Entity Framework Core, and coordinates the inventory and payment service boundaries through dedicated HTTP clients.

The service does not own inventory quantities or payment authorization outcomes. It records order state and coordinates calls to Inventory.Api and Payments.Api as part of the distributed order workflow.

## Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Orders.Api owns workflow coordination for the microservices path. Inventory and payment behavior remain behind their respective HTTP service boundaries, while the order record captures the request, reservation identity, status, and optional rejection reason.

See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The project performs eight main tasks:

- Hosts the ASP.NET Core application for the orders service
- Coordinates the distributed order workflow
- Calls Inventory.Api through an inventory client abstraction
- Calls Payments.Api through a payments client abstraction
- Persists order workflow state through Entity Framework Core and SQLite
- Adds correlation information and source-generated structured logging
- Verifies database and downstream service connectivity for readiness
- Exposes shared readiness and liveness endpoints

## Startup flow

`Program.cs` is responsible for application composition. Based on the maintained project structure, startup includes the following concerns:

- creating the web application builder
- applying shared service defaults
- registering `OrdersDbContext` with SQLite
- registering inventory and payments HTTP clients
- registering database and downstream readiness checks
- building the web application
- adding request correlation and structured logging behavior
- initializing the local database when required
- mapping application and shared health endpoints
- running until shutdown

Keep host composition in `Program.cs` concise. Endpoint behavior, client behavior, persistence mapping, and health-check logic should remain in focused types rather than accumulating in the host entry point.

## Client organization

Downstream integrations are separated into abstractions and HTTP implementations:

```text
Internal/Clients/Abstraction/IInventoryClient.cs
Internal/Clients/Abstraction/IPaymentsClient.cs
Internal/Clients/HttpInventoryClient.cs
Internal/Clients/HttpPaymentsClient.cs
```

Orders workflow code should depend on `IInventoryClient` and `IPaymentsClient`, not concrete HTTP implementations. This keeps orchestration testable and prevents transport-specific details from spreading into workflow code.

`HttpInventoryClient` owns communication with Inventory.Api. `HttpPaymentsClient` owns communication with Payments.Api. Their responsibilities should include request construction, cancellation propagation, response handling, and transport-specific failure reporting.

## Order workflow

The orders service coordinates a distributed workflow across independently persisted services. The expected responsibility boundary is:

1. create or load the order record
2. request an inventory reservation
3. stop and record rejection when inventory cannot be reserved
4. request payment authorization when inventory is reserved
5. release the inventory reservation when payment is rejected
6. record the terminal order outcome

This sequence is a compensating workflow, not a distributed database transaction. Each service owns its own persistence, and compensation is explicit when a later step fails.

The concrete route paths, request contracts, and handler arrangement should be documented from the current `Program.cs` or endpoint extension when those files are reviewed. They are intentionally not inferred from the directory tree alone.

## Idempotency

`OrderRecord` contains an `IdempotencyKey` that associates repeated order-placement requests with the same persisted workflow result.

Idempotency behavior is part of the service contract. A repeated request should not create a second logical order, reserve inventory again, or authorize payment again after a terminal result has been persisted.

The exact uniqueness constraint and lookup behavior must remain aligned across:

- the HTTP endpoint
- `OrdersDbContext` mapping
- the persisted `OrderRecord`
- downstream inventory and payment request identifiers

Changes to key comparison, uniqueness, replay semantics, or terminal-result reuse can affect distributed workflow correctness even when the HTTP shape remains unchanged.

## Compensation

When inventory has been reserved and payment authorization subsequently fails, Orders.Api is responsible for requesting release of the inventory reservation.

Compensation should:

- use the original reservation identifier
- propagate caller-requested cancellation where appropriate
- log failures with order and reservation context
- avoid reporting a completed order when release or persistence behavior is uncertain
- remain idempotent when the downstream inventory service receives a repeated release

Compensation is not atomic with payment authorization or order persistence. Tests and operational documentation should cover partial-failure behavior explicitly.

## Persistence model

`OrderRecord` stores:

- `OrderId`, the unique order identifier
- `IdempotencyKey`, the key associated with repeated placement requests
- `CustomerId`, the customer that placed the order
- `ProductId`, the ordered product
- `Quantity`, the requested unit count
- `ReservationId`, the identity used for inventory reservation and release
- `Status`, the current workflow status represented by its contract value
- `Reason`, optional details explaining rejection

`OrdersDbContext` owns Entity Framework Core access to persisted orders. Database mapping should define the primary key, idempotency constraint, string-length limits, and any query indexes in a dedicated entity configuration when the model requires non-conventional mapping.

Keep persistence concerns out of HTTP client implementations. Keep transport concerns out of the entity model.

## Database initialization

The local service uses SQLite and produces `orders.db`, `orders.db-shm`, and `orders.db-wal` runtime artifacts. These files are local persistence outputs and should not be documented as maintained source or committed as project content.

If the application uses `EnsureCreatedAsync`, that approach is suitable for a local architecture workbench with a disposable schema. If the schema evolves through migrations, replace schema creation with an explicit migration workflow rather than mixing the two strategies.

Environment-specific deployments should provide the database connection through normal ASP.NET Core configuration providers and ensure that the database path is writable by the runtime user.

## Correlation logging

The request pipeline should use the repository correlation convention:

```text
X-Correlation-ID
```

When the header contains a non-blank value, the service can create a logging scope with a structured `CorrelationId` property and keep that scope active through downstream inventory and payment calls.

The same correlation value should be propagated through the typed HTTP clients when the existing client contracts support it. The header is operational metadata only and must not be treated as authenticated identity or authorization data.

## Structured logging

The project groups source-generated logging by level:

```text
Internal/Observability/Logging/LogInformation.cs
Internal/Observability/Logging/LogError.cs
```

Informational events should cover:

- request correlation
- order placement start
- idempotent replay
- inventory reservation results
- payment authorization results
- compensation attempts
- terminal order outcomes

Error events should cover unexpected persistence, downstream transport, response-processing, and compensation failures.

Event IDs and message templates are telemetry contracts. Preserve stable event IDs and PascalCase structured property names. Do not log credentials, connection strings, complete request bodies, or other sensitive values.

## Health endpoints

The project contains three readiness checks:

```text
Internal/Observability/Health/OrdersDatabaseHealthCheck.cs
Internal/Observability/Health/InventoryApiHealthCheck.cs
Internal/Observability/Health/PaymentsApiHealthCheck.cs
```

`OrdersDatabaseHealthCheck` verifies connectivity to the orders database. `InventoryApiHealthCheck` and `PaymentsApiHealthCheck` verify reachability of the downstream service boundaries.

Shared service defaults are expected to expose:

```text
/health
/alive
```

`/health` represents readiness and should include database and required downstream dependency checks. `/alive` represents process liveness and should not fail solely because a downstream service is unavailable.

Connectivity checks do not guarantee that the next workflow operation will succeed, that schemas are current, or that downstream business behavior is correct.

## Configuration

`appsettings.json` contains project configuration consumed by ASP.NET Core, HTTP clients, persistence, health checks, and shared service defaults.

`Properties/launchSettings.json` contains local launch profiles.

Environment-specific values should be supplied through standard ASP.NET Core configuration providers. Do not commit secrets, credentials, or production connection strings to either file.

Client registration and health checks should use the same configured downstream service identities so operational readiness reflects the dependencies used by the workflow.

## Local development

Orders.Api depends at runtime on:

- writable access to the configured SQLite database path
- Inventory.Api for inventory lookup, reservation, and release
- Payments.Api for payment authorization

From the Orders.Api project directory:

```console
dotnet run
```

From the repository root:

```console
dotnet run --project <path-to-Orders.Api.csproj>
```

Local URLs are defined by `Properties/launchSettings.json` or runtime configuration. Downstream service addresses must match the service-discovery or HTTP client configuration used by the application.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Orders API changes should cover at least:

- successful order completion
- inventory rejection
- payment rejection
- inventory release after payment rejection
- repeated requests with the same idempotency key
- order persistence and terminal-result reuse
- database initialization
- orders database connectivity failure
- Inventory.Api readiness failure
- Payments.Api readiness failure
- downstream transport and invalid-response behavior
- cancellation propagation
- compensation failure
- correlation propagation and logging scope creation
- structured log event IDs and property names
- readiness and liveness endpoints
- request binding and invalid inputs

## Adding or changing workflow behavior

When modifying this project:

- Keep host composition in `Program.cs` concise
- Keep downstream transport behind `IInventoryClient` and `IPaymentsClient`
- Keep HTTP implementations in `Internal/Clients`
- Preserve order idempotency and terminal-result replay semantics
- Reuse the persisted reservation identifier for compensation
- Keep persistence mapping in focused Entity Framework configuration types
- Propagate `OperationCanceledException` rather than converting cancellation into a generic failure
- Log unexpected failures before returning a problem response
- Preserve structured message templates and stable event IDs
- Avoid logging sensitive request or configuration data
- Keep required dependency checks on readiness rather than liveness
- Update this README when routes, clients, workflow ordering, persistence, compensation, or health behavior changes

## Naming conventions

- Client abstractions use the `I` prefix and `Client` suffix
- HTTP client implementations use the `Http` prefix and `Client` suffix
- Async operations use the `Async` suffix
- Entity Framework contexts use the `DbContext` suffix
- Entity mappings use the `EntityConfiguration` suffix
- Health checks use the `HealthCheck` suffix
- Source-generated logging classes are grouped by log level
- Structured logging placeholders use PascalCase
- Route parameters use camelCase

## Scope

Orders.Api demonstrates an independently deployed workflow coordinator for the microservices ordering scenario. It is not a production order management system, distributed transaction coordinator, durable workflow engine, message broker, authentication model, authorization policy, or disaster-recovery design.

Production use would require independent decisions for durable messaging, retries, timeouts, compensation recovery, concurrency control, validation, security, migrations, deployment, scaling, monitoring, backup, and recovery.
