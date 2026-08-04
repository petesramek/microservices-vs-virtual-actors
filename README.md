# Microservices vs Virtual Actors

This repository is an architecture workbench case study for the same order workflow implemented in two styles:

- a microservices-style implementation with explicit HTTP service boundaries
- a virtual actor-style implementation with identity-based state ownership and serialized execution per actor identity

The project is designed to make architectural trade-offs visible across implementation, state ownership, concurrency, idempotency, compensation, timeout handling, testing, deployment, scaling, observability, operations, organizational fit, and long-term maintenance.

This project is not a benchmark. Local elapsed times are useful for understanding the sample topology, but they should not be interpreted as universal performance conclusions.

## What this project demonstrates

The workbench focuses on practical architecture questions:

- Who owns inventory state?
- Who protects inventory invariants?
- Who owns order workflow decisions?
- How is idempotency protected under concurrent duplicate submissions?
- How does each style handle contention for one hot product?
- How are payment failure and timeout compensation expressed?
- How are scenario outcomes tested and documented?
- How do the approaches differ in deployment, scaling, observability, operations, maintenance, and organizational fit?

## Architecture styles compared

### Microservices-style path

The microservices-style path uses separate services for the main workflow responsibilities:

- `Orders.Api` owns order workflow orchestration.
- `Inventory.Api` owns inventory state and reservation invariants.
- `Payments.Api` owns payment authorization behavior.
- `Workbench.Gateway` runs the same scenario against each architecture path.
- `Workbench.Ui` provides the interactive scenario dashboard.

This style makes service boundaries and independent deployment explicit. The trade-off is that every boundary introduces compatibility, reliability, observability, and operational concerns.

### Virtual actor-style path

The virtual actor-style path expresses the workflow through stateful identities:

- `OrderGrain(orderId)` owns one logical order workflow.
- `InventoryItemGrain(productId)` owns one product inventory identity.
- `PaymentAccountGrain(customerId)` owns payment behavior.
- `Ordering.Api` exposes the actor-backed workflow entry point.

This style makes identity-based state ownership explicit. The trade-off is that grain interface compatibility, persistent grain state evolution, runtime behavior, activation lifecycle, and hot identity behavior become important design and operational concerns.

## Scenario list

The UI exposes the following workbench scenarios. See [docs/12-scenario-guide.md](docs/12-scenario-guide.md) for the full scenario guide, expected results, architecture interpretations, and operational notes.

### Successful order

Demonstrates the happy path. Inventory is available, payment succeeds, and the order completes.

Expected shape:

- total request submissions: `1`
- unique successful orders: `1`
- rejected submissions: `0`
- idempotent duplicate responses: `0`
- remaining inventory decreases by the requested quantity

### Insufficient inventory

Demonstrates business rejection before payment. Inventory is not available, so the order is rejected and payment should not be attempted.

Expected shape:

- total request submissions: `1`
- unique successful orders: `0`
- rejected submissions: `1`
- reason: `InsufficientInventory`

### Payment failure compensation

Demonstrates compensation after a known downstream failure. Inventory is reserved, payment explicitly fails, and inventory is released.

Expected shape:

- total request submissions: `1`
- unique successful orders: `0`
- rejected submissions: `1`
- remaining inventory returns to initial stock
- reason: `PaymentFailed`

### Payment timeout after reservation

Demonstrates timeout handling after inventory has already been reserved. The sample treats timeout as failed, releases inventory, and rejects the order.

Expected shape:

- total request submissions: `1`
- unique successful orders: `0`
- rejected submissions: `1`
- remaining inventory returns to initial stock
- reason: `PaymentTimeout`

### Concurrent orders

Demonstrates many independent order submissions competing for the same product stock.

Expected shape when demand exceeds stock:

- unique successful orders do not exceed available stock divided by quantity
- extra submissions are rejected
- remaining inventory does not go below zero

### Hot product contention

Demonstrates many concurrent requests targeting one hot product identity.

Expected shape with initial stock `25`, quantity `1`, and `50` concurrent requests:

- total request submissions: `50`
- unique successful orders: `25`
- rejected submissions: `25`
- remaining inventory: `0`
- reason: `SomeOrdersRejected`

### Duplicate request

Demonstrates idempotency under repeated duplicate submissions. The scenario submits the same logical order multiple times concurrently using the same order identity and idempotency key.

Expected shape with initial stock `10`, quantity `2`, and `20` duplicate submissions:

- total request submissions: `20`
- unique successful orders: `1`
- rejected submissions: `0`
- idempotent duplicate responses: `19`
- remaining inventory: `8`
- reason: `IdempotentResultReturned`

## Result terminology

The UI result cards use request-submission terminology consistently:

- **Total request submissions**: how many requests were submitted for the scenario run.
- **Unique successful orders**: how many unique logical orders completed successfully.
- **Rejected submissions**: how many logical submissions were rejected.
- **Idempotent duplicate responses**: how many duplicate submissions returned an existing logical result.
- **Remaining inventory**: the final inventory quantity after the scenario run.
- **Elapsed**: local elapsed time for the architecture path in this sample topology.

These values are semantic contracts for the workbench. For example, **unique successful orders** means unique successful logical orders, not raw successful HTTP responses.

## How to run locally

Start the backend services and UI according to the project launch settings. At minimum, the workbench UI expects the workbench gateway and backend services to be running on their configured local ports.

### Validate build and tests

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

### Option A — run from Visual Studio

The repository can be run directly from Visual Studio by configuring multiple startup projects.

Set these projects to start together:

- `src/Microservices/Inventory.Api`
- `src/Microservices/Payments.Api`
- `src/Microservices/Orders.Api`
- `src/VirtualActors/Ordering.Api`
- `src/Workbench/Workbench.Gateway`
- `src/Workbench/Workbench.Ui`

Then start debugging/running from Visual Studio and open the UI at:

```text
http://localhost:5000
```

This is the most convenient local development flow when working inside Visual Studio because each service keeps its own launch profile and output window.

### Option B — run all local services

```powershell
./scripts/run-all-local.ps1
```

This starts the microservices backend, virtual actor backend, workbench gateway, and UI.

### Option C — run the backend groups separately

```powershell
./scripts/run-microservices.ps1
./scripts/run-virtual-actors.ps1
./scripts/run-workbench.ps1
```

Open the UI at:

```text
http://localhost:5000
```

## Testing

The project includes scenario regression tests that protect the agreed scenario result semantics.

The regression tests cover:

- successful order
- insufficient inventory
- payment failure compensation
- payment timeout after reservation
- concurrent orders
- hot product contention
- duplicate request with concurrent duplicate submissions

The regression suite is intentionally focused on result semantics:

- total request submissions
- unique successful orders
- rejected submissions
- idempotent duplicate responses
- remaining inventory
- reason

When scenario behavior changes intentionally, update the regression tests and the documentation together.

## Observability

The sample uses lightweight header-based correlation for local diagnostics.

The UI generates a correlation ID and sends it as:

```text
X-Correlation-ID
```

The gateway forwards the correlation ID to backend calls, and backend services add it to structured logging scopes.

Use the correlation ID shown in the UI to find related logs across:

- `Workbench.Gateway`
- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`
- `Ordering.Api`

The project deliberately keeps correlation metadata out of scenario request and response contracts. See [docs/13-correlation-id-logging.md](docs/13-correlation-id-logging.md) and [docs/16-observability-and-operations.md](docs/16-observability-and-operations.md) for more detail.

## Documentation map

Read the documentation in this order for the intended narrative:

- [Problem](docs/01-problem.md) explains the workbench problem and the workflow being modeled.
- [Microservices design](docs/02-microservices-design.md) explains the service-based implementation and ownership boundaries.
- [Virtual actors design](docs/03-virtual-actors-design.md) explains the actor-based implementation and stateful identity boundaries.
- [Development workbench](docs/04-development-workbench.md) compares day-to-day development concerns in both styles.
- [Deployment workbench](docs/05-deployment-workbench.md) compares deployment shape and operational surface area.
- [Scaling workbench](docs/06-scaling-workbench.md) compares service-boundary scaling with actor-runtime and identity-based scaling.
- [Trade-offs](docs/07-tradeoffs.md) summarizes the main architecture trade-offs.
- [Organizational scaling and architecture fit](docs/08-organizational-scaling-and-architecture-fit.md) explains team ownership, architecture fit, evolution paths, and product-quality risks.
- [Local validation](docs/09-local-validation.md) describes local validation expectations.
- [UI dashboard](docs/10-ui-dashboard.md) explains the interactive workbench dashboard.
- [End-to-end validation](docs/11-end-to-end-validation.md) explains end-to-end validation behavior.
- [Scenario guide](docs/12-scenario-guide.md) explains each scenario, expected results, architecture interpretations, and operational notes.
- [Correlation ID logging](docs/13-correlation-id-logging.md) explains the simplified correlation mechanism and the production OpenTelemetry direction.
- [Release, versioning, and rollback](docs/14-release-versioning-and-rollback.md) explains architecture-specific compatibility, state evolution, and rollback concerns.
- [Maintenance and evolution](docs/15-maintenance-and-evolution.md) compares how both styles evolve as behavior changes.
- [Observability and operations](docs/16-observability-and-operations.md) explains runtime diagnostics, metrics, alerting considerations, and operational interpretation.
- [Known limitations](docs/17-known-limitations.md) explains what the sample does not prove and how to interpret results responsibly.
- [Out of scope](docs/18-out-of-scope.md) lists topics intentionally not covered by this repository.

## How to interpret timings

Elapsed times are local demo observations. They are useful for understanding the shape of this sample topology, but they are not benchmark proof.

Timing can be affected by:

- local machine performance
- service warmup
- local HTTP overhead
- SQLite/local persistence behavior
- Orleans runtime behavior
- logging overhead
- gateway orchestration
- contention on one product identity

Use timings to ask better questions. Do not use them to claim universal performance superiority for either architecture style.

## Known limitations

This project intentionally simplifies several production concerns:

- no production authentication or authorization model
- no full distributed tracing backend
- no production-grade payment integration
- no full deployment platform
- no multi-region or autoscaling design
- simplified persistence and migration strategy
- simplified timeout policy
- no intentionally unsafe race-condition scenario in the main workbench

See [docs/17-known-limitations.md](docs/17-known-limitations.md) for the full interpretation guide and [docs/18-out-of-scope.md](docs/18-out-of-scope.md) for the explicit scope boundary.

## Key takeaway

The central workbench is not whether microservices or virtual actors are universally better.

The central workbench is how each style expresses and maintains:

- state ownership
- concurrency guarantees
- workflow coordination
- compensation policy
- idempotency behavior
- deployment and scaling boundaries
- operational diagnostics
- long-term evolution
