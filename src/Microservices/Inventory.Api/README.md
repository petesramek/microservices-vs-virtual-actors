# Inventory.Api

Inventory.Api is the inventory management service for the microservices implementation in the **Microservices vs Virtual Actors** architecture workbench. It exposes ASP.NET Core Minimal API endpoints, persists product quantities and active reservations to SQLite through Entity Framework Core, and returns shared workbench response contracts.

The service does not orchestrate orders or authorize payments. Orders.Api calls this service to read inventory, reserve stock, and release reservations as part of the distributed order workflow.

## Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Inventory.Api owns product availability and reservation state for the microservices path. Reservation identifiers provide idempotency so a repeated reservation request does not decrement available inventory more than once.

See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The project performs eight main tasks:

- Hosts the ASP.NET Core Minimal API application
- Exposes inventory reset, lookup, reservation, and release endpoints
- Persists inventory items and reservations through Entity Framework Core and SQLite
- Provides idempotent reservation and release behavior
- Uses a transaction when creating a new reservation and decrementing inventory
- Adds correlation information and source-generated structured logging
- Verifies inventory database connectivity for readiness
- Exposes shared readiness and liveness endpoints

## Startup flow

`Program.cs` performs application composition:

- Creates the `WebApplicationBuilder`
- Applies shared service defaults
- Registers `InventoryDbContext` with SQLite
- Registers the inventory database health check
- Builds the web application
- Adds correlation logging middleware
- Ensures that the local database schema exists
- Maps inventory and shared health endpoints
- Runs until shutdown

The database connection uses the `Default` connection string when configured and otherwise falls back to:

```text
Data Source=inventory.db
```

The fallback supports local workbench execution. Environment-specific deployments should provide the connection string through standard ASP.NET Core configuration providers.

## Endpoint organization

Application and shared health endpoints are registered by:

```csharp
app.MapInventoryEndpoints();
```

`InventoryEndpointRouteBuilderExtensions` owns route mapping, handlers, persistence calls, transaction handling, result construction, and endpoint-specific logging. Keeping the handlers outside `Program.cs` preserves a clear boundary between host composition and HTTP behavior.

Inventory operations are grouped under `/api/inventory`, while the root service-information endpoint remains at `/`.

## Endpoint reference

### Service information

```http
GET /
```

Returns identifying information for the service:

```json
{
  "name": "Inventory API",
  "phase": "Microservices"
}
```

### Reset inventory

```http
POST /api/inventory/reset
```

Accepts a `ResetInventoryRequest` containing a product identifier and quantity.

The handler:

- loads or creates the inventory item
- replaces its available quantity
- removes all active reservations for the product
- persists the resulting state
- returns an `InventoryResponse`

Reset is an architecture-workbench control. It clears reservations for the selected product and should not be treated as a general production inventory adjustment workflow.

### Get inventory

```http
GET /api/inventory/{productId}
```

Returns the current available quantity for the selected product. When no inventory item exists, the service returns quantity `0` rather than an HTTP 404 response.

The query uses `AsNoTracking` because the handler does not modify the loaded entity.

### Reserve inventory

```http
POST /api/inventory/{productId}/reserve
```

Accepts a `ReserveInventoryRequest` containing the order identifier, reservation identifier, and requested quantity.

The handler:

- checks whether the reservation identifier already exists
- returns the current successful reservation result when it is a replay
- begins a database transaction for a new reservation
- verifies that sufficient inventory is available
- decrements the available quantity
- creates the reservation record
- persists both changes
- commits the transaction
- returns a `ReserveInventoryResponse`

Insufficient inventory is represented as a successful HTTP response with:

```text
Reserved: false
Reason:   InsufficientInventory
```

Unexpected failures are logged and mapped to HTTP 500. Caller-requested cancellation is propagated.

### Release inventory

```http
POST /api/inventory/{productId}/release
```

Accepts a `ReleaseInventoryRequest` containing the reservation identifier.

When the reservation exists, the handler restores its quantity to the inventory item, removes the reservation, persists the updated state, and returns the current inventory response.

When the reservation does not exist, the operation is idempotent. The handler returns the current available quantity without creating or changing persistence state.

## Reservation idempotency

`ReservationId` is the lookup key for repeated reservation and release requests.

When a matching reservation already exists, the reserve endpoint returns success without decrementing inventory again. When a release references an unknown reservation, the endpoint returns the current quantity without modifying state.

Reservation idempotency is part of the service contract. Changes to reservation identity, replay behavior, or release semantics can affect distributed workflow correctness even when the HTTP shape remains unchanged.

## Transaction boundary

Creating a reservation changes two pieces of persistence state:

- the product's available quantity
- the new reservation record

The reserve endpoint performs both changes inside an explicit database transaction and commits only after `SaveChangesAsync` succeeds. The transaction prevents a successful reservation record from being persisted independently of its inventory decrement within that operation.

The current reset and release handlers use a single `SaveChangesAsync` call for their related tracked changes.

## Persistence model

`InventoryItem` stores:

- `ProductId`, the primary key
- `AvailableQuantity`, the quantity not allocated to active reservations

`InventoryReservation` stores:

- `ReservationId`, the primary key
- `OrderId`, the associated order identifier
- `ProductId`, the reserved product identifier
- `Quantity`, the quantity allocated to the order

`InventoryDbContext` exposes inventory items and reservations and applies dedicated entity configurations during model creation.

`InventoryItemEntityConfiguration` defines:

- `ProductId` as the primary key
- a maximum product-identifier length of `100`

`InventoryReservationEntityConfiguration` defines:

- `ReservationId` as the primary key
- a maximum product-identifier length of `100`
- an index on `OrderId`

The schema limits are expressed as named constants in the entity configuration classes.

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
Logging/LogInformation.cs
Logging/LogError.cs
```

Informational events cover:

- request correlation
- inventory reset start and completion
- inventory retrieval
- reservation start and completion
- release start and completion

Error events cover unexpected failures during reset, lookup, reservation, and release operations.

Event IDs should remain stable. Message-template placeholders should use stable PascalCase property names for structured telemetry.

Do not log credentials, connection strings, full request bodies, or other sensitive values. Product, order, reservation, and correlation identifiers are the current operational context carried by these events.

## Health endpoints

The project registers `InventoryDatabaseHealthCheck`, which creates a dependency-injection scope and calls `CanConnectAsync` on `InventoryDbContext`.

`MapInventoryEndpoints` also maps the shared service-default endpoints:

```text
/health
/alive
```

`/health` is the readiness endpoint and includes the inventory database connectivity check registered as:

```text
inventory-database
```

`/alive` is the liveness endpoint and evaluates checks tagged for process liveness by the shared defaults.

The database health check verifies connectivity only. It does not validate schema freshness or guarantee that each subsequent transaction will succeed.

## Configuration

`appsettings.json` contains project configuration consumed by ASP.NET Core and shared service defaults.

`Properties/launchSettings.json` contains local launch profiles.

Environment-specific values should be supplied through normal ASP.NET Core configuration providers. Do not commit secrets or credentials to either file.

## Docker

The project includes a `Dockerfile` for container builds. Keep it aligned with the target framework, repository build layout, database path, and runtime user when project references or output paths change.

Container-specific ports, user configuration, filesystem permissions, and health checks should be reviewed directly in the `Dockerfile`, those details are not duplicated here.

## Local development

The service has no runtime HTTP dependency on Payments.Api or Orders.Api. It requires writable access to the configured SQLite database path.

From the Inventory.Api project directory:

```console
dotnet run
```

From the repository root:

```console
dotnet run --project <path-to-Inventory.Api.csproj>
```

Local URLs are defined by `Properties/launchSettings.json` or runtime configuration.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Inventory API changes should cover at least:

- reset of an existing product
- creation of inventory during reset
- removal of reservations during reset
- lookup of existing and missing products
- successful reservation
- insufficient inventory
- repeated reservation identifiers
- release of existing and missing reservations
- transactional reservation behavior
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
- Add inventory routes through `InventoryEndpointRouteBuilderExtensions`
- Keep inventory routes under `/api/inventory`
- Preserve reservation and release idempotency semantics
- Keep persistence mappings in dedicated entity configuration classes
- Keep multi-entity reservation changes within an explicit transaction
- Propagate `OperationCanceledException` rather than converting cancellation to HTTP 500
- Log unexpected failures before returning a problem response
- Preserve structured message templates and stable event IDs
- Avoid logging sensitive request or configuration data
- Keep database dependency checks on readiness rather than liveness
- Update this README when routes, persistence, transactions, health checks, or initialization behavior change

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

Inventory.Api demonstrates an independently deployed inventory boundary for the microservices ordering scenario. It is not a production warehouse management system, allocation engine, replenishment system, distributed transaction coordinator, authentication model, authorization policy, or disaster-recovery design.

Production use would require independent decisions for concurrency control, inventory reconciliation, validation, durable storage, migrations, deployment, scaling, monitoring, backup, and recovery.
