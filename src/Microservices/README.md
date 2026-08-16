### Microservices

Microservices contains the HTTP service implementation of the **Microservices vs Virtual Actors** architecture workbench. It exposes the ordering scenario through independently hosted ASP.NET Core APIs, coordinates the workflow across explicit HTTP boundaries, and persists each service's state to its own SQLite database through Entity Framework Core.

This folder represents one side of the repository's architecture comparison. The corresponding virtual actor implementation models the same business flow with identity-addressed Orleans grains and grain-state persistence.

#### Architecture overview

The microservices implementation is divided into three projects:

```text
Microservices/
  Orders.Api/
  Inventory.Api/
  Payments.Api/
```

The primary dependency and runtime flow is:

```text
Workbench caller
  -> Orders.Api
      -> Inventory.Api
          -> inventory.db
      -> Payments.Api
          -> payments.db
      -> orders.db
```

Each API owns its HTTP boundary, persistence model, health checks, and structured logging. The services exchange shared workbench request and response contracts, but they do not share database state.

#### Projects

##### Orders.Api

Orders.Api is the workflow coordinator for the microservices implementation.

It:

- hosts the ASP.NET Core API;
- maps scenario, inventory-proxy, and order endpoints;
- coordinates inventory reservation and payment authorization;
- releases inventory when payment authorization fails;
- persists order workflow state and terminal outcomes;
- provides order-level idempotency handling;
- uses typed HTTP clients for Inventory.Api and Payments.Api;
- adds correlation scopes and source-generated structured logging;
- maps shared readiness and liveness endpoints.

Application composition is separated across:

```text
Orders.Api/Program.cs
Orders.Api/Extensions/OrdersServiceCollectionExtensions.cs
Orders.Api/Extensions/OrdersApplicationBuilderExtensions.cs
Orders.Api/Extensions/OrdersEndpointRouteBuilderExtensions.cs
```

`Program.cs` keeps the top-level startup sequence visible. Service registration, request-pipeline behavior, and endpoint handlers remain in focused extension types.

Orders.Api does not directly modify inventory or payment persistence. It communicates through `IInventoryClient` and `IPaymentsClient`.

##### Inventory.Api

Inventory.Api owns product availability and active inventory reservations.

It:

- hosts the ASP.NET Core Minimal API;
- resets and retrieves product inventory;
- reserves quantities for orders;
- releases existing reservations;
- persists inventory items and reservations to SQLite;
- uses reservation identifiers for idempotent reserve and release behavior;
- uses an explicit transaction when decrementing inventory and creating a reservation;
- adds correlation scopes and source-generated structured logging;
- exposes database readiness and shared liveness endpoints.

Application endpoint registration lives in:

```text
Inventory.Api/Extensions/InventoryEndpointRouteBuilderExtensions.cs
```

Persistence mappings are separated into dedicated Entity Framework Core configuration types. Inventory.Api does not orchestrate orders or authorize payments.

##### Payments.Api

Payments.Api owns deterministic payment authorization outcomes for the workbench scenario.

It:

- hosts the ASP.NET Core Minimal API;
- accepts payment authorization requests;
- persists payment attempts to SQLite;
- uses an idempotency key to replay previously stored authorization outcomes;
- supports deterministic simulated payment rejection;
- adds correlation scopes and source-generated structured logging;
- exposes database readiness and shared liveness endpoints.

Application endpoint registration lives in:

```text
Payments.Api/Extensions/EndpointRouteBuilderExtensions.cs
```

Payments.Api does not orchestrate orders, reserve inventory, process settlement, or integrate with a production payment provider.

#### Workflow

The primary order flow is:

1. A caller sends an order request to Orders.Api.
2. Orders.Api checks for an existing order with the same idempotency key.
3. If no existing order is found, Orders.Api persists an initial order record.
4. Orders.Api asks Inventory.Api to reserve the requested quantity.
5. If inventory is unavailable, Orders.Api records a rejected terminal result.
6. If inventory is reserved, Orders.Api asks Payments.Api to authorize payment.
7. If payment is rejected, Orders.Api asks Inventory.Api to release the reservation.
8. Orders.Api persists the final rejected or completed result.
9. The stored order result is returned to the caller.

This is a compensating distributed workflow, not a transaction spanning all three databases. Each service commits its own state independently. Orders.Api explicitly requests compensation when a later workflow step fails.

#### Service boundaries and ownership

The implementation uses service and database boundaries to define state ownership:

- Orders.Api owns order requests, workflow status, reservation references, and terminal outcomes.
- Inventory.Api owns product quantities and reservation records.
- Payments.Api owns payment authorization attempts and their persisted outcomes.

A service must not read or modify another service's SQLite database. Cross-service behavior must flow through the owning service's HTTP contract.

This separation makes partial failure visible. A successful local database write does not guarantee that a subsequent downstream call or compensation request will succeed.

#### Idempotency

The workflow uses explicit identifiers for repeat-request handling:

- order placement uses an order idempotency key;
- inventory reservation uses a reservation identifier;
- payment authorization uses a payment identifier and idempotency key;
- inventory release reuses the original reservation identifier.

Orders.Api also serializes concurrent in-process order-placement requests that share an idempotency key. The database uniqueness constraint remains the persistent protection for stored order keys.

Idempotency behavior is part of the application's behavioral contract. Changing key comparison, uniqueness, replay semantics, or identifier propagation can affect scenario results even when public HTTP shapes remain unchanged.

The in-process keyed gate is local to one Orders.Api process. It is not a distributed lock and does not coordinate multiple service replicas.

#### Compensation

When inventory is reserved but payment authorization is rejected, Orders.Api requests release of the original inventory reservation.

Compensation uses the persisted reservation identifier so repeated release requests remain safe. Inventory.Api treats an unknown reservation as an idempotent release and returns the current available quantity without creating new state.

Compensation is not atomic with payment authorization or order persistence. Operational testing should include downstream timeouts, cancellation, repeated requests, and failures that occur after one service has already committed state.

#### Persistence

Each service uses its own SQLite database:

```text
Orders.Api     -> orders.db
Inventory.Api  -> inventory.db
Payments.Api   -> payments.db
```

The databases are local runtime artifacts and are intentionally excluded from maintained project trees and source control. SQLite `-shm` and `-wal` files are also runtime artifacts.

Entity Framework Core mappings are separated from their `DbContext` types:

```text
Orders.Api/Internal/Infrastructure/
  OrdersDbContext.cs
  OrderRecordEntityConfiguration.cs

Inventory.Api/Internal/Infrastructure/
  InventoryDbContext.cs
  InventoryItemEntityConfiguration.cs
  InventoryReservationEntityConfiguration.cs

Payments.Api/Internal/Infrastructure/
  PaymentsDbContext.cs
  PaymentAttemptEntityConfiguration.cs
```

The APIs use `EnsureCreatedAsync` for the local workbench. If schemas begin evolving through migrations, replace schema creation with an explicit migration workflow rather than mixing both approaches.

Do not share a connection string or database file across these service boundaries.

#### HTTP endpoints

Orders.Api exposes:

```text
GET  /
POST /api/scenarios/reset
GET  /api/inventory/{productId}
POST /api/orders
GET  /api/orders/{orderId:guid}
```

Inventory.Api exposes:

```text
GET  /
POST /api/inventory/reset
GET  /api/inventory/{productId}
POST /api/inventory/{productId}/reserve
POST /api/inventory/{productId}/release
```

Payments.Api exposes:

```text
GET  /
POST /api/payments/authorize
```

Shared service defaults map on each API:

```text
GET /health
GET /alive
```

Orders.Api's inventory endpoint is an orchestration-facing proxy to Inventory.Api. The Inventory.Api endpoints remain the owning service boundary for inventory state.

#### Configuration and service discovery

The projects use standard ASP.NET Core configuration providers.

Orders.Api reads:

```text
ConnectionStrings:Default
Services:InventoryBaseUrl
Services:PaymentsBaseUrl
```

Its local fallbacks are:

```text
Orders database:  Data Source=orders.db
Inventory API:    http://localhost:5201
Payments API:     http://localhost:5202
```

Inventory.Api and Payments.Api use `ConnectionStrings:Default` when configured and otherwise fall back to their local SQLite files.

The preferred complete-workbench startup path is the repository AppHost so project references, endpoints, service discovery, environment variables, and observability components are configured together.

Do not commit secrets, credentials, or production connection strings to project configuration files.

#### Observability

The projects use shared service defaults for logging, metrics, tracing, health reporting, service discovery, resilience, and exporter configuration.

Each API reads the optional header:

```text
X-Correlation-ID
```

When present and non-blank, the value is added to a structured logging scope as `CorrelationId`. It is operational metadata only and must not be treated as authenticated identity or authorization data.

Orders.Api HTTP clients add the shared scenario-run header to downstream requests so workbench traffic can be identified by observability components.

Source-generated logging provides stable event IDs, structured properties, and reduced runtime logging overhead. Preserve event IDs and PascalCase message-template property names when changing log definitions.

Do not log credentials, connection strings, full request bodies, or raw persisted state.

#### Health model

The shared endpoints distinguish readiness from liveness:

- `/health` evaluates registered dependency checks;
- `/alive` evaluates process-liveness checks.

Current database readiness checks are registered as:

```text
Orders.Api     -> orders-database
Inventory.Api  -> inventory-database
Payments.Api   -> payments-database
```

Orders.Api also contains health-check implementations for Inventory.Api and Payments.Api. Register downstream readiness checks only when their failure should make Orders.Api unavailable to receive new work.

Database and HTTP health checks verify connectivity or reachability only. They do not validate schema freshness, business correctness, compensation safety, or guarantee that the next workflow request will succeed.

Downstream dependency failures belong on readiness, not liveness. A temporary Inventory.Api or Payments.Api outage must not cause process-liveness restarts by itself.

#### Failure behavior

The implementation makes several failure boundaries observable:

- insufficient inventory produces a rejected order without payment authorization;
- simulated payment failure produces a rejected order and inventory release request;
- unexpected endpoint failures are logged and returned as HTTP 500 problem responses;
- caller-requested cancellation is propagated rather than converted into HTTP 500;
- repeated order, reservation, authorization, and release requests use their persisted identifiers;
- downstream failure after a local commit can leave a workflow requiring retry or reconciliation.

The workbench does not include a durable message broker, background recovery processor, outbox, distributed transaction coordinator, or automated reconciliation engine.

#### Local development

The implementation is configured for local development with ASP.NET Core, HTTP service boundaries, and SQLite persistence.

The preferred way to run the complete workbench is through the repository AppHost so all three APIs and their observability dependencies start with consistent configuration.

When running projects individually, start Inventory.Api and Payments.Api before sending order-placement requests to Orders.Api. Ensure each service has writable access to its configured SQLite path.

Each project can also be run directly:

```console
dotnet run --project <path-to-Inventory.Api.csproj>
dotnet run --project <path-to-Payments.Api.csproj>
dotnet run --project <path-to-Orders.Api.csproj>
```

Project launch URLs are defined by each `Properties/launchSettings.json` file or runtime configuration.

#### Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Microservices changes should verify at least:

- successful inventory reset and retrieval;
- successful order placement and retrieval;
- insufficient-inventory rejection;
- simulated payment rejection and inventory compensation;
- repeated order requests with the same idempotency key;
- repeated inventory reservation and release requests;
- repeated payment authorization requests;
- order, inventory, and payment persistence;
- database initialization for all three services;
- downstream transport and invalid-response failures;
- compensation failure and retry behavior;
- API cancellation behavior;
- correlation and scenario-header propagation;
- structured logging event IDs and property names;
- readiness and liveness behavior;
- SQLite connectivity reporting.

#### Adding or changing behavior

When modifying this folder:

- Keep order orchestration in Orders.Api.
- Keep inventory ownership and reservation behavior in Inventory.Api.
- Keep payment authorization ownership in Payments.Api.
- Keep cross-service communication behind typed client abstractions.
- Do not access another service's database directly.
- Preserve order, reservation, payment, and release idempotency behavior.
- Preserve explicit compensation when payment fails after inventory reservation.
- Keep Entity Framework Core mappings in dedicated configuration types.
- Keep host composition concise and move focused registration, middleware, and endpoint behavior into extensions.
- Propagate cancellation instead of converting it to an internal-server error.
- Keep dependency checks on readiness rather than liveness.
- Avoid logging secrets, connection strings, request bodies, or persisted payloads.
- Update the relevant project README and this folder README when cross-service contracts change.

#### Scope

The Microservices folder demonstrates the HTTP-service implementation of the ordering scenario. It is not a production order management platform, payment processor, warehouse management system, distributed transaction design, durable workflow engine, message-driven architecture, security model, backup plan, autoscaling policy, or disaster-recovery solution.

Production use would require independent decisions for authentication, authorization, network security, durable messaging, outbox and inbox processing, retries, timeouts, reconciliation, migrations, concurrency control, deployment, scaling, monitoring, backup, and recovery.
