# Payments.Api

Payments.Api is the payment authorization service for the microservices implementation in the **Microservices vs Virtual Actors** architecture workbench. It exposes an ASP.NET Core Minimal API endpoint, persists idempotent authorization outcomes to SQLite through Entity Framework Core, and returns shared workbench response contracts.

The service does not orchestrate orders or manage inventory. Orders.Api calls this service as part of the distributed order workflow.

## Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Payments.Api owns the payment authorization boundary for the microservices path. It records each authorization outcome so repeated requests using the same idempotency key return the previously persisted result.

See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The project performs six main tasks:

- Hosts the ASP.NET Core Minimal API application
- Exposes the payment authorization endpoint
- Persists payment attempts through Entity Framework Core and SQLite
- Provides idempotent responses for repeated authorization requests
- Adds correlation information and source-generated structured logging
- Exposes database readiness and shared liveness endpoints

## Startup flow

`Program.cs` performs application composition:

1. Create the `WebApplicationBuilder`.
2. Apply shared service defaults.
3. Register `PaymentsDbContext` with SQLite.
4. Register the payments database health check.
5. Build the web application.
6. Add correlation logging middleware.
7. Ensure the local database schema exists.
8. Map payment endpoints.
9. Map shared readiness and liveness endpoints.
10. Run until shutdown.

The database connection uses the `Default` connection string when configured and otherwise falls back to:

```text
Data Source=payments.db
```

The fallback supports local workbench execution. Environment-specific deployments should provide the connection string through normal ASP.NET Core configuration providers.

## Endpoint organization

Application endpoints are registered by:

```csharp
app.MapPaymentsEndpoints();
```

`EndpointRouteBuilderExtensions` owns route mapping, handlers, persistence calls, result construction, and endpoint-specific logging. Keeping the handlers outside `Program.cs` preserves a clear boundary between host composition and HTTP behavior.

Payment routes are grouped under `/api/payments`, while the root service-information endpoint remains at `/`.

## Endpoint reference

### Service information

```http
GET /
```

Returns identifying information for the service:

```json
{
  "name": "Payments API",
  "phase": "Microservices"
}
```

### Authorize payment

```http
POST /api/payments/authorize
```

Accepts an `AuthorizePaymentRequest` containing the payment, order, customer, idempotency, and failure-simulation values required by the workbench scenario.

The handler:

1. Looks for an existing `PaymentAttempt` with the same idempotency key.
2. Returns the persisted authorization result when a matching attempt exists.
3. Otherwise determines the deterministic authorization outcome.
4. Persists the new payment attempt.
5. Returns an `AuthorizePaymentResponse`.

Successful requests return HTTP 200. Unexpected failures are logged and mapped to HTTP 500. Caller-requested cancellation is propagated.

## Idempotency

`IdempotencyKey` has a unique database index and is the lookup key for repeated payment authorization requests.

When a matching attempt exists, the service returns the stored `Authorized` and `Reason` values without creating another payment attempt. The original payment and order identifiers are also used when logging the replayed result.

Idempotency behavior is part of the service contract. Changes to key comparison, uniqueness, or replay semantics can affect distributed workflow correctness even when the HTTP shape remains unchanged.

## Failure simulation

The workbench request can simulate a rejected payment authorization. When simulation is enabled, the persisted result contains:

```text
Authorized: false
Reason:     PaymentFailed
```

This is a deterministic architecture-workbench control, not a production payment decision engine or general fault-injection interface.

## Persistence model

`PaymentAttempt` stores:

- `PaymentId`, the primary key
- `OrderId`, the associated order identifier
- `CustomerId`, the customer identifier
- `IdempotencyKey`, the unique repeat-request key
- `Authorized`, the terminal authorization outcome
- `Reason`, the optional failure reason

`PaymentsDbContext` exposes the payment attempts and applies `PaymentAttemptEntityConfiguration` during model creation.

The entity configuration defines:

- `PaymentId` as the primary key
- a maximum length of 100 for customer identifiers and failure reasons
- a maximum length of 200 for idempotency keys
- a unique index on `IdempotencyKey`

The schema limits are consolidated as named constants in the entity configuration.

## Database initialization

The application calls `EnsureCreatedAsync` before mapping requests. This creates the SQLite database and schema when they do not exist.

`EnsureCreatedAsync` is suitable for the local architecture workbench. If the schema begins evolving through migrations, replace this initialization approach with an explicit migration workflow rather than mixing both strategies.

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
- payment authorization start
- payment authorization completion

Error events cover unexpected payment authorization failures.

Event IDs are allocated from log-level-specific ranges. Message-template placeholders use stable PascalCase property names for structured telemetry.

Do not log credentials, full request bodies, connection strings, or other sensitive values. Payment, order, customer, authorization, and correlation identifiers are the current operational context carried by these events.

## Health endpoints

The project registers `PaymentsDatabaseHealthCheck`, which creates an asynchronous dependency-injection scope and calls `CanConnectAsync` on `PaymentsDbContext`.

Shared service defaults map:

```text
/health
/alive
```

`/health` is the readiness endpoint and includes the payments database connectivity check registered as:

```text
payments-database
```

`/alive` is the liveness endpoint and evaluates checks tagged for process liveness by the shared defaults.

The database health check verifies connectivity only. It does not validate schema freshness or guarantee that each subsequent write will succeed.

## Configuration

`appsettings.json` contains project configuration consumed by ASP.NET Core and shared service defaults.

`Properties/launchSettings.json` contains local launch profiles.

Environment-specific values should be supplied through normal ASP.NET Core configuration providers. Do not commit secrets or credentials to either file.

## Local development

The service has no runtime HTTP dependency on Inventory.Api or Orders.Api. It does require writable access to the configured SQLite database path.

From the Payments.Api project directory:

```console
dotnet run
```

From the repository root:

```console
dotnet run --project <path-to-Payments.Api.csproj>
```

Local URLs are defined by `Properties/launchSettings.json` or runtime configuration.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Payments API changes should cover at least:

- successful authorization
- simulated authorization failure
- repeated requests with the same idempotency key
- unique idempotency-key enforcement
- payment-attempt persistence
- database initialization
- database connectivity failure
- cancellation propagation
- correlation scope creation with and without the header
- structured log event IDs and property names
- readiness and liveness endpoints
- request binding and invalid inputs

## Adding or changing endpoints

When modifying this project:

- Keep host composition in `Program.cs` concise
- Add payment routes through `EndpointRouteBuilderExtensions`
- Keep payment routes in the `/api/payments` route group
- Preserve idempotency semantics and the unique database constraint
- Keep persistence mapping in `PaymentAttemptEntityConfiguration`
- Propagate `OperationCanceledException` rather than converting cancellation to HTTP 500
- Log unexpected failures before returning a problem response
- Preserve structured message templates and stable event IDs
- Avoid logging sensitive payment or customer data
- Keep database dependency checks on readiness rather than liveness
- Update this README when routes, persistence, idempotency, health checks, or initialization behavior change

## Naming conventions

- Endpoint registration types use the `Extensions` suffix
- Endpoint registration methods use the `Map` prefix
- Async route handlers use the `Async` suffix
- Entity Framework contexts use the `DbContext` suffix
- Entity mappings use the `EntityConfiguration` suffix
- Health checks use the `HealthCheck` suffix
- Source-generated logging classes are grouped by log level
- Structured logging placeholders use PascalCase
- Route parameters use camelCase

## Scope

Payments.Api demonstrates an independently deployed payment boundary for the microservices ordering scenario. It is not a production payment processor, fraud engine, ledger, settlement system, PCI compliance implementation, authentication model, authorization policy, or disaster-recovery design.

Production use would require independent decisions for payment-provider integration, security, compliance, durable storage, migrations, deployment, scaling, monitoring, backup, and recovery.
