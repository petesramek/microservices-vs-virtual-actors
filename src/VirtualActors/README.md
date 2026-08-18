# VirtualActors

VirtualActors contains the Orleans implementation of the **Microservices vs Virtual Actors** architecture workbench. It exposes the ordering scenario through an ASP.NET Core API, executes the workflow through identity-addressed Orleans grains, hosts those grains in a local silo, and persists grain state to SQLite through a custom Orleans storage provider.

This folder represents one side of the repository's architecture comparison. The corresponding microservices implementation models the same business flow with explicit HTTP service boundaries.

## Architecture overview

The virtual actor implementation is divided into four projects:

```text
VirtualActors/
  Ordering.Api/
  Ordering.Grains/
  Ordering.Persistence.Sqlite/
  Ordering.Silo/
```

The dependency and runtime flow is:

```text
Workbench caller
    -> Ordering.Api
        -> Orleans client
            -> Ordering.Silo
                -> Ordering.Grains
                    -> Orleans persistence abstraction
                        -> Ordering.Persistence.Sqlite
                            -> SQLite
```

## Projects

### Ordering.Api

`Ordering.Api` is the HTTP adapter for the virtual actor workflow.

It:

- hosts the ASP.NET Core Minimal API
- configures an Orleans client with localhost clustering
- maps inventory and order endpoints
- converts grain-call contracts to shared workbench responses
- propagates activity context into Orleans calls
- adds correlation scopes and source-generated structured logging
- maps shared readiness and liveness endpoints

Application endpoint registration lives in:

```text
Ordering.Api/Extensions/EndpointRouteBuilderExtensions.cs
```

The API does not implement grain behavior or access grain-state persistence directly.

### Ordering.Grains

`Ordering.Grains` defines and implements the virtual actors.

It contains:

- Orleans grain interfaces
- grain implementations
- serialized result and snapshot contracts
- mutable persisted state models
- stable Orleans aliases and serialization member IDs

The current actor identities are:

- inventory item grain: string product ID
- order grain: GUID order ID
- payment account grain: string customer or account ID

The order grain coordinates inventory reservation and payment authorization. Grain implementations own command validation, idempotency behavior, state mutation, compensation, and persistence timing.

### Ordering.Persistence.Sqlite

`Ordering.Persistence.Sqlite` implements a named SQLite-backed Orleans `IGrainStorage` provider.

It:

- registers a pooled Entity Framework Core context factory
- registers named Orleans grain storage
- serializes grain state through the Orleans storage serializer
- maps provider-managed versions to Orleans ETags
- applies migrations during silo startup
- enables SQLite write-ahead logging
- exposes a connectivity-only health check

Storage and health-check registration are intentionally separate:

```csharp
siloBuilder
    .AddSqliteGrainStorage(
        storageProviderName,
        connectionString)
    .AddSqliteGrainStorageHealthCheck(
        healthCheckName);
```

The provider is designed for the local architecture workbench. It is not a distributed or multi-host storage solution.

### Ordering.Silo

`Ordering.Silo` hosts the Orleans runtime and grain implementations.

It:

- applies shared service defaults
- uses localhost Orleans clustering
- enables activity propagation
- registers SQLite grain storage
- registers the SQLite connectivity health check
- hosts the Orleans Dashboard
- maps shared readiness and liveness endpoints

The current local contracts are:

```text
Connection string: Default
Storage provider:  OrderingStorage
Health check:      ordering-database
Dashboard route:   /dashboard
```

## Workflow

The primary order flow is:

1. A caller sends an order request to `Ordering.Api`.
2. The API resolves `IOrderGrain` using the order GUID.
3. The order grain resolves the inventory grain using the product ID.
4. The inventory grain attempts to reserve the requested quantity.
5. If inventory is unavailable, the order is rejected.
6. If inventory is reserved, the order grain resolves the payment account grain using the customer or account ID.
7. The payment grain returns a deterministic authorization result.
8. If payment is rejected, the order grain releases the inventory reservation.
9. The order grain persists the terminal result.
10. The API converts the grain result to the shared workbench response.

## Identity and ownership

The implementation uses actor identity as the state-ownership boundary:

- one inventory grain owns the available quantity and reservations for one product
- one order grain owns the workflow and terminal result for one order
- one payment account grain owns authorization results for one customer or account identity

Orleans serializes calls to a grain activation, reducing the need for application-level locking inside one actor identity. Cross-grain workflows still require explicit idempotency, durable state transitions, and compensation.

## Idempotency

The workflow uses explicit identifiers for repeat-call handling:

- order placement uses an idempotency key
- inventory reservation uses a reservation ID
- payment authorization uses an idempotency key and payment ID

Idempotency behavior is part of the application's behavioral contract. Changing how repeated requests are recognized or replayed can affect scenario results even when public method signatures remain unchanged.

## Persistence

Grain implementations depend on Orleans persistence abstractions rather than SQLite types.

The storage provider persists a composite grain-state identity containing:

```text
ServiceId
ProviderName
StateName
GrainType
GrainId
```

Stored payloads are opaque serialized state and may contain sensitive application data. Do not log payloads, connection strings, or raw persisted state.

## Serialization compatibility

Grain interfaces, methods, result contracts, and state models use explicit Orleans aliases and member identifiers.

When evolving these contracts:

- preserve established aliases
- preserve existing member IDs
- never reuse an old ID for a different meaning
- assign a new unused ID to each new serialized member
- treat alias changes as compatibility changes
- keep nullable semantics stable unless intentionally changing the contract

## HTTP endpoints

`Ordering.Api` exposes:

```text
GET  /
POST /api/scenarios/reset
GET  /api/inventory/{productId}
POST /api/orders
GET  /api/orders/{orderId:guid}
```

Shared service defaults also map:

```text
GET /health
GET /alive
```

The Silo additionally exposes the Orleans Dashboard at:

```text
/dashboard
```

## Observability

The projects use shared service defaults for logging, metrics, tracing, health reporting, and exporter configuration.

Activity propagation connects incoming API requests with Orleans client and grain execution activities.

The API uses the optional `X-Correlation-ID` header to create a structured logging scope. The value is for correlation only and must not be treated as authenticated identity.

Source-generated logging is used for stable event IDs, structured properties, and reduced runtime logging overhead.

## Health model

The shared endpoints distinguish readiness from liveness:

- `/health` evaluates registered dependency checks
- `/alive` evaluates process-liveness checks

The SQLite health check verifies connectivity only. It does not validate migration freshness or guarantee that every future persistence operation will succeed.

## Local development

The implementation is configured for local development with localhost Orleans clustering and SQLite storage.

The preferred way to run the complete workbench is through the repository AppHost so project references, endpoints, environment variables, and observability components are started together.

When running projects individually, start the Silo before the API so the Orleans client can connect to the local cluster.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Virtual actor changes should verify at least:

- Orleans source generation
- interface and serialization alias stability
- grain activation by the expected key type
- successful inventory reset and retrieval
- successful order placement and retrieval
- insufficient-inventory rejection
- simulated payment rejection and inventory compensation
- repeated requests and idempotency behavior
- grain persistence and reactivation
- API error and cancellation behavior
- activity and correlation propagation
- readiness and liveness behavior
- SQLite connectivity reporting

## Adding or changing behavior

When modifying this folder:

- Keep HTTP adaptation in `Ordering.Api`
- Keep actor contracts, behavior, and state in `Ordering.Grains`
- Keep Orleans storage implementation in `Ordering.Persistence.Sqlite`
- Keep runtime composition in `Ordering.Silo`
- Preserve grain identities, aliases, serialization IDs, and storage-provider names
- Validate commands before mutating and persisting grain state
- Preserve idempotency and compensation behavior
- Propagate cancellation instead of converting it to an internal-server error
- Keep dependency checks on readiness rather than liveness
- Avoid logging secrets, connection strings, or serialized grain state
- Update the relevant project README and this folder README when cross-project contracts change

## Scope

The VirtualActors folder demonstrates the actor-based implementation of the ordering scenario. It is not a production clustering design, distributed persistence strategy, multi-region architecture, security model, backup plan, autoscaling policy, or disaster-recovery solution.

Production use would require independent decisions for cluster membership, durable storage, authentication, authorization, network security, deployment, scaling, monitoring, backup, and recovery.
