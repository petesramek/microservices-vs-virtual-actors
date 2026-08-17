# Microservices vs Virtual Actors

This repository is an architecture workbench case study for the same order workflow implemented in two styles:

- a microservices-style implementation with explicit HTTP service boundaries;
- a virtual actor-style implementation with identity-based state ownership and serialized execution per actor identity.

The project is designed to make architectural trade-offs visible across implementation, state ownership, concurrency, idempotency, compensation, timeout handling, testing, deployment, scaling, observability, operations, organizational fit, and long-term maintenance.

The repository includes a .NET Aspire AppHost, an interactive Blazor workbench, shared health and topology models, architecture-specific and full Docker Compose definitions, scenario regression tests, gateway acceptance tests, and end-to-end validation scripts.

> This project is not a benchmark. Local elapsed times help explain the sample topology, but they should not be interpreted as universal performance conclusions.

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
- How can health, dependency topology, and runtime availability be presented consistently across both designs?

## Architecture styles compared

### Microservices-style path

The microservices-style path uses separate services for the main workflow responsibilities:

- `Orders.Api` owns order workflow orchestration.
- `Inventory.Api` owns inventory state and reservation invariants.
- `Payments.Api` owns payment authorization behavior.
- Each service owns its SQLite persistence boundary and health checks.

```text
Workbench.Gateway
  -> Orders.Api
      -> Inventory.Api
      -> Payments.Api
```

This style makes service boundaries and independent deployment explicit. The trade-off is that every boundary introduces compatibility, reliability, observability, and operational concerns.

See [`src/Microservices/README.md`](src/Microservices/README.md) for the implementation overview.

### Virtual actor-style path

The virtual actor-style path expresses the workflow through stateful identities:

- `OrderGrain(orderId)` owns one logical order workflow.
- `InventoryItemGrain(productId)` owns one product inventory identity.
- `PaymentAccountGrain(customerId)` owns payment behavior.
- `Ordering.Api` exposes the actor-backed workflow entry point.
- `Ordering.Silo` hosts the Orleans runtime.
- `Ordering.Persistence.Sqlite` persists grain state.

```text
Workbench.Gateway
  -> Ordering.Api
      -> Ordering.Silo
          -> OrderGrain
          -> InventoryItemGrain
          -> PaymentAccountGrain
```

This style makes identity-based state ownership explicit. The trade-off is that grain interface compatibility, persistent grain-state evolution, runtime behavior, activation lifecycle, and hot-identity behavior become important design and operational concerns.

See [`src/VirtualActors/README.md`](src/VirtualActors/README.md) for the implementation overview.

## Repository structure

```text
src/
  Hosting/
    Hosting.AppHost/
    Hosting.ServiceDefaults/
  Microservices/
    Inventory.Api/
    Orders.Api/
    Payments.Api/
  Observability/
    Observability.Health/
    Observability.Topology/
  VirtualActors/
    Ordering.Api/
    Ordering.Grains/
    Ordering.Persistence.Sqlite/
    Ordering.Silo/
  Workbench/
    Workbench.Contracts/
    Workbench.Gateway/
    Workbench.Ui/
tests/
  Microservices.Tests/
  VirtualActors.Tests/
  Workbench.AcceptanceTests/
  Workbench.ScenarioRegressionTests/
```

The major supporting areas are:

- `src/Hosting/Hosting.AppHost` composes the complete local topology with .NET Aspire.
- `src/Hosting/Hosting.ServiceDefaults` provides shared service discovery, resilience, health checks, logging, metrics, and tracing configuration.
- `src/Observability/Observability.Health` provides reusable health-report models and status evaluation.
- `src/Observability/Observability.Topology` provides topology definitions, validation, dependency evaluation, snapshots, and availability modeling.
- `src/Workbench/Workbench.Contracts` defines shared inventory, order, payment, and scenario contracts.
- `src/Workbench/Workbench.Gateway` prepares and runs comparison scenarios against one or both implementations.
- `src/Workbench/Workbench.Ui` provides scenario, health, topology, and trade-off pages.

Each major folder and project includes a focused README where deeper implementation details belong.

## Workbench

The Workbench provides the common comparison surface rather than a third order-processing implementation.

```text
Browser
  -> Workbench.Ui
      -> Workbench.Gateway
          -> microservices architecture
          -> virtual actor architecture
      <- normalized scenario results
```

### Scenario dashboard

The root page allows users to:

- select `Both`, `Microservices`, or `Virtual Actors`;
- choose a deterministic scenario;
- use scenario defaults or edit advanced stock, quantity, concurrency, and identifier inputs;
- follow presentation-only progress while a request is active;
- compare normalized result cards side by side;
- inspect completion counts, rejection counts, idempotent responses, remaining inventory, elapsed time, reasons, and timeline events.

### Health dashboard

The health page presents:

- overall health summaries;
- architecture and service groups;
- nodes and dependencies;
- readiness and liveness information;
- resource availability;
- explanatory health messages.

Health indicates reachability and readiness, not business correctness. A healthy endpoint does not prove that a scenario, compensation path, or idempotency rule is correct.

### Topology page

The topology page uses shared definitions and snapshots to explain:

- application nodes;
- architecture groups;
- dependency edges;
- required and optional dependencies;
- current resource availability.

The Aspire AppHost supplies the runtime composition, while the shared topology project provides validation and evaluation semantics.

### Trade-offs page

The trade-offs page provides an in-product summary of the comparison. Detailed and maintainable architectural claims remain in the documents under `docs`.

See [`src/Workbench/README.md`](src/Workbench/README.md) for the complete Workbench overview.

## Scenario list

The UI exposes the following workbench scenarios. See [`docs/12-scenario-guide.md`](docs/12-scenario-guide.md) for expected results, architecture interpretations, and operational notes.

### Successful order

Demonstrates the happy path. Inventory is available, payment succeeds, and the order completes.

Expected shape:

- total request submissions: `1`
- unique successful orders: `1`
- rejected submissions: `0`
- idempotent duplicate responses: `0`
- remaining inventory decreases by the requested quantity

### Insufficient inventory

Demonstrates business rejection before payment. Inventory is unavailable, so the order is rejected and payment should not be attempted.

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

Demonstrates idempotency under repeated concurrent submissions using the same order identity and idempotency key.

Expected shape with initial stock 10, quantity 2, and 20 duplicate submissions:

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

### Prerequisites

Install the .NET SDK required by the repository and use Docker Desktop only when running the Compose-based deployment options.

### Option A: run with .NET Aspire

The preferred complete local experience is the Aspire AppHost:

```bash
dotnet run --project src/Hosting/Hosting.AppHost/Hosting.AppHost.csproj
```

Open the Workbench UI endpoint from the Aspire dashboard. The AppHost composes the compared implementations, gateway, UI, health groups, service discovery, and observability resources.

### Option B: run from Visual Studio

Open `microservices-vs-virtual-actors.slnx` in Visual Studio and start `Hosting.AppHost` to use the same composed topology and Aspire dashboard.

For targeted debugging, individual projects retain their own launch profiles. Ensure all dependencies required by the selected scenario are running before opening the Workbench UI.

### Option C: use repository scripts

Run the complete local topology:

```powershell
./scripts/run-all-local.ps1
```

Run the compared backend groups independently:

```powershell
./scripts/run-microservices.ps1
./scripts/run-virtual-actors.ps1
```

Run the comparison workflow:

```powershell
./scripts/run-comparison.ps1
```

### Option D: use Docker Compose

Run the complete topology with:

```bash
docker compose -f deploy/docker-compose.full.yml up --build
```

Architecture-specific definitions are also available:

```text
deploy/microservices/docker-compose.yml
deploy/virtual-actors/docker-compose.yml
```

See [`deploy/README.md`](deploy/README.md) and [`docs/05-deployment-comparison.md`](docs/05-deployment-comparison.md) before changing ports, dependencies, images, or rollout behavior.

## Build and validation

From the repository root:

```bash
dotnet restore
dotnet build microservices-vs-virtual-actors.slnx --configuration Release
dotnet test microservices-vs-virtual-actors.slnx --configuration Release --no-build
```

Repository validation scripts are also available:

```powershell
./scripts/test-build.ps1
./scripts/validate-e2e.ps1
```

The GitHub Actions workflow under `.github/workflows/build.yml` provides repository CI validation.

## Testing

The repository separates implementation, gateway, and scenario-result coverage:

- `Microservices.Tests` covers the HTTP-service order workflow with controlled inventory and payment clients.
- `VirtualActors.Tests` covers the Orleans order workflow and SQLite grain persistence.
- `Workbench.AcceptanceTests` covers externally visible Workbench.Gateway behavior.
- `Workbench.ScenarioRegressionTests` protects normalized scenario-result semantics.

The regression suite covers:

- successful order;
- insufficient inventory;
- payment failure compensation;
- payment timeout after reservation;
- concurrent orders;
- hot product contention;
- duplicate request with concurrent duplicate submissions.

The regression suite intentionally focuses on semantic output:

- total request submissions;
- unique successful orders;
- rejected submissions;
- idempotent duplicate responses;
- remaining inventory;
- reason.

When scenario behavior changes intentionally, update the implementation, contracts, regression tests, UI guidance, and scenario documentation together.

## Observability and operations

The repository uses shared service defaults and reusable observability models rather than treating diagnostics as a gateway-only feature.

### Correlation

The UI and gateway use `X-Correlation-ID` for local request correlation. The gateway forwards the value to backend calls, and services add it to structured logging scopes.

Use the correlation ID to find related logs across:

- `Workbench.Gateway`;
- `Orders.Api`;
- `Inventory.Api`;
- `Payments.Api`;
- `Ordering.Api`.

Correlation metadata stays outside scenario request and response contracts.

### Tracing and metrics

`Hosting.ServiceDefaults` configures shared OpenTelemetry behavior. Scenario instrumentation records architecture, scenario, outcome, duration, request counts, and idempotent responses. Trace collection can be configured through the shared observability options and scenario trace sampler.

### Health and topology

Services expose the shared health endpoints:

- `/health` for readiness and registered dependency checks;
- `/alive` for process liveness.

`Observability.Health` provides shared health models and status evaluation. `Observability.Topology` provides topology definitions, validation, dependency evaluation, group evaluation, and availability snapshots. Workbench.Ui presents these models through its health and topology pages.

See [`docs/13-correlation-id-logging.md`](docs/13-correlation-id-logging.md) and [`docs/16-observability-and-operations.md`](docs/16-observability-and-operations.md).

## Documentation map

Read the documentation in this order for the intended narrative:

1. [Problem](docs/01-problem.md) explains the workbench problem and modeled workflow.
2. [Microservices design](docs/02-microservices-design.md) explains service boundaries and ownership.
3. [Virtual actors design](docs/03-virtual-actors-design.md) explains actor boundaries and stateful identities.
4. [Development comparison](docs/04-development-comparison.md) compares day-to-day development concerns.
5. [Deployment comparison](docs/05-deployment-comparison.md) compares deployment shape and operational surface area.
6. [Scaling comparison](docs/06-scaling-comparison.md) compares service-boundary and identity-based scaling.
7. [Trade-offs](docs/07-tradeoffs.md) summarizes the main architectural trade-offs.
8. [Organizational scaling and architecture fit](docs/08-organizational-scaling-and-architecture-fit.md) discusses ownership, fit, evolution, and product-quality risks.
9. [Local validation](docs/09-local-validation.md) describes local validation expectations.
10. [UI dashboard](docs/10-ui-dashboard.md) explains the scenario, health, topology, and trade-off experience.
11. [End-to-end validation](docs/11-end-to-end-validation.md) explains full-system validation behavior.
12. [Scenario guide](docs/12-scenario-guide.md) documents each scenario and expected result.
13. [Correlation ID logging](docs/13-correlation-id-logging.md) explains correlation and the OpenTelemetry direction.
14. [Release, versioning, and rollback](docs/14-release-versioning-and-rollback.md) covers compatibility, state evolution, and rollback.
15. [Maintenance and evolution](docs/15-maintenance-and-evolution.md) compares how both styles change over time.
16. [Observability and operations](docs/16-observability-and-operations.md) covers diagnostics, metrics, alerts, health, and operational interpretation.
17. [Known limitations](docs/17-known-limitations.md) explains what the sample does not prove.
18. [Out of scope](docs/18-out-of-scope.md) identifies intentionally excluded concerns.

## How to interpret timings

Elapsed times are local demo observations. They help explain this sample topology, but they are not benchmark proof.

Timing can be affected by:

- local machine performance;
- process and actor activation warmup;
- local HTTP overhead;
- SQLite and local persistence behavior;
- Orleans runtime behavior;
- logging, tracing, and metric overhead;
- gateway orchestration;
- contention on one product identity.

Use timings to ask better questions. Do not use them to claim universal performance superiority for either architectural style.

## Known limitations

This project intentionally simplifies several production concerns:

- no production authentication or authorization model;
- no production-grade payment integration;
- no full deployment platform or multi-region design;
- no autoscaling or capacity-management implementation;
- simplified persistence, migrations, and backup behavior;
- simplified timeout and recovery policy;
- no durable reconciliation process;
- no intentionally unsafe race-condition scenario in the main workbench;
- explanatory health and topology views rather than a production monitoring platform.

See [`docs/17-known-limitations.md`](docs/17-known-limitations.md) for the interpretation guide and [`docs/18-out-of-scope.md`](docs/18-out-of-scope.md) for the explicit scope boundary.

## Contributing and maintenance

Use the issue templates under `.github/ISSUE_TEMPLATE` for bug reports and feature requests.

When introducing an intentional behavior change:

- keep shared transport models in `Workbench.Contracts`;
- keep architecture-specific behavior in the relevant implementation;
- keep comparison orchestration in `Workbench.Gateway`;
- preserve result terminology and idempotency semantics;
- keep health and topology definitions aligned with AppHost resources;
- update tests and the narrowest relevant documentation together;
- avoid committing runtime databases, `-wal`, `-shm`, or `.csproj.user` artifacts.

See [`docs/15-maintenance-and-evolution.md`](docs/15-maintenance-and-evolution.md) for broader maintenance guidance.

## Key takeaway

The central question is not whether microservices or virtual actors are universally better.

The useful comparison is how each style expresses and maintains:

- state ownership;
- concurrency guarantees;
- workflow coordination;
- compensation policy;
- idempotency behavior;
- deployment and scaling boundaries;
- operational diagnostics;
- long-term evolution.

The best fit depends on workload identity, consistency needs, team boundaries, operational maturity, deployment constraints, and the kinds of change the system must absorb over time.
