# Microservices vs Virtual Actors

This repository is an architecture workbench for comparing the same order workflow implemented in two styles:

- **Microservices**, with explicit HTTP service boundaries and service-owned persistence.
- **Virtual actors**, with Orleans grains that own state and behavior by durable identity.

The comparison focuses on how each architecture expresses state ownership, concurrency, idempotency, compensation, contention, deployment, observability, and evolution. It includes a Blazor Workbench UI, deterministic scenarios, topology-aware health, shared observability, and a .NET Aspire development environment.

> This repository is a teaching and comparison tool. It is not a production reference architecture or a controlled benchmark.

## What you can explore

Use the workbench to investigate questions such as:

- Who owns inventory state and protects its invariants?
- Where does order workflow coordination live?
- How are concurrent requests prevented from over-reserving stock?
- How are duplicate submissions resolved idempotently?
- How is inventory compensated after payment failure or timeout?
- What happens when many requests target one hot product identity?
- How do deployment and operational responsibilities differ?
- How do the architectures affect long-term maintenance and team ownership?

Start with the [problem statement](docs/01-problem.md), then explore the [microservices design](docs/02-microservices-design.md), [virtual actors design](docs/03-virtual-actors-design.md), and detailed [trade-offs](docs/07-tradeoffs.md).

## Architecture at a glance

```text
Workbench.Ui
  -> Workbench.Gateway
      -> Microservices
          -> Orders.Api
              -> Inventory.Api
              -> Payments.Api
      -> Virtual actors
          -> Ordering.Api
              -> Ordering.Silo
                  -> OrderGrain
                  -> InventoryItemGrain
                  -> PaymentAccountGrain
```

`Workbench.Gateway` runs each scenario through both implementations and returns normalized results to `Workbench.Ui`.

### Microservices

- `Orders.Api` coordinates the order workflow.
- `Inventory.Api` owns inventory state and reservation invariants.
- `Payments.Api` owns payment authorization behavior.
- Workflow coordination crosses explicit HTTP and persistence boundaries.

See the [Microservices folder overview](src/Microservices/README.md) and [Microservices design](docs/02-microservices-design.md).

### Virtual actors

- `OrderGrain(orderId)` owns one logical order workflow.
- `InventoryItemGrain(productId)` owns inventory for one product identity.
- `PaymentAccountGrain(customerId)` owns payment behavior for one customer or account identity.
- `Ordering.Api` is the HTTP entry point and Orleans client, while `Ordering.Silo` hosts the Orleans runtime.

See the [Virtual actors folder overview](src/VirtualActors/README.md) and [Virtual actors design](docs/03-virtual-actors-design.md).

## Workbench experience

`Workbench.Ui` provides four focused views.

### Scenario runner

The Scenario runner executes the selected deterministic workflow through both implementations and presents normalized results side by side. It supports scenario defaults and optional advanced inputs for stock, quantity, concurrency, and identity values.

The result cards show request submissions, unique successful orders, rejected submissions, idempotent duplicate responses, remaining inventory, elapsed time, terminal reasons, and explanatory timelines.

See the [UI dashboard guide](docs/10-ui-dashboard.md), [Scenario guide](docs/12-scenario-guide.md), and [Workbench folder overview](src/Workbench/README.md).

### Health

The Health page combines live readiness and liveness reports with the shared topology model. It organizes resources into groups, nodes, and dependencies, and presents:

- service availability
- direct and aggregate health
- required and optional dependency health
- group health
- unknown or missing observations

Health describes runtime reachability and readiness. It does not prove business correctness.

See [Observability and operations](docs/16-observability-and-operations.md), the [Health model](src/Observability/Observability.Health/README.md), and the [Topology model](src/Observability/Observability.Topology/README.md).

### Topology

The Topology page is a text-based explanation of the intended architecture. It describes the Workbench request path, service ownership, actor identities, Orleans runtime boundary, and dependency relationships.

It does not display live resource state or availability. Runtime topology-aware health belongs on the Health page.

### Trade-offs

The Trade-offs page provides a concise in-product comparison of the two architecture styles. Detailed reasoning remains in [Trade-offs](docs/07-tradeoffs.md) and [Organizational scaling and architecture fit](docs/08-organizational-scaling-and-architecture-fit.md).

## Scenarios

The workbench includes seven scenarios:

- **Successful order:** inventory is available and payment succeeds.
- **Insufficient inventory:** the workflow is rejected before payment.
- **Payment failure compensation:** reserved inventory is released after explicit payment failure.
- **Payment timeout after reservation:** timeout is treated as failure and compensated.
- **Concurrent orders:** independent orders compete for limited stock.
- **Duplicate request:** concurrent duplicate submissions resolve to one logical result.
- **Hot product contention:** many requests target one product identity.

See the [Scenario guide](docs/12-scenario-guide.md) for default inputs, expected counts, reason values, architecture interpretation, and operational validation.

## Result semantics

The normalized result contract distinguishes attempts from logical outcomes:

- **Total request submissions** counts attempts sent to one implementation.
- **Unique successful orders** counts distinct logical orders that completed.
- **Rejected submissions** counts logical submissions that were rejected.
- **Idempotent duplicate responses** counts repeated submissions that returned an established result.
- **Remaining inventory** is the final observed quantity.
- **Elapsed time** is local workbench feedback, not benchmark evidence.

This distinction is especially important for concurrent and duplicate-request scenarios.

## Run locally

### Prerequisites

Install the .NET SDK required by the repository and use a suitable .NET development environment.

Confirm the installed SDKs with:

```bash
dotnet --list-sdks
```

### Start with Aspire

The supported development path uses the Aspire AppHost:

```bash
dotnet run --project src/Hosting/Hosting.AppHost/Hosting.AppHost.csproj
```

Open the Aspire dashboard URL printed by the AppHost, then open the `Workbench.Ui` endpoint from the resource list.

Aspire is used to:

- compose and start the development topology
- provide service discovery and dependency wiring
- expose project endpoints and resource health
- inspect structured logs
- inspect distributed traces
- inspect metrics
- manage resource lifecycle during development

See the [Hosting overview](src/Hosting/README.md) and [AppHost overview](src/Hosting/Hosting.AppHost/README.md).

## Repository map

```text
src/
  Hosting/         Aspire composition and shared service defaults
  Microservices/   Orders, inventory, and payment services
  Observability/   Shared health and topology models
  VirtualActors/   Orleans API, grains, persistence, and silo
  Workbench/       Shared contracts, gateway, and Blazor UI
tests/             Workflow, persistence, acceptance, and regression tests
docs/              Architecture, validation, and operational guidance
```

Each major source area contains a focused README with implementation-specific guidance.

## Testing and validation

Run the standard validation sequence from the repository root:

```bash
dotnet restore microservices-vs-virtual-actors.slnx
dotnet build microservices-vs-virtual-actors.slnx --configuration Release --no-restore
dotnet test microservices-vs-virtual-actors.slnx --configuration Release --no-build
```

The test projects provide complementary coverage:

- `Microservices.Tests` covers the HTTP-service workflow.
- `VirtualActors.Tests` covers the Orleans workflow and SQLite grain persistence.
- `Workbench.AcceptanceTests` covers externally visible gateway behavior.
- `Workbench.ScenarioRegressionTests` protects normalized scenario-result semantics.

The GitHub Actions workflow under `.github/workflows/build.yml` performs automated build and test validation.

See [Local validation](docs/09-local-validation.md) and [End-to-end validation](docs/11-end-to-end-validation.md) for the complete validation workflow.

## Observability in development

The repository uses shared service defaults and custom scenario instrumentation:

- W3C trace context and .NET `Activity`
- OpenTelemetry traces and metrics
- structured logging and `X-Correlation-ID` propagation
- scenario activities and bounded metrics
- custom trace collection and sampling
- readiness and liveness endpoints
- topology-aware health evaluation

The observability surfaces are complementary:

- **Aspire dashboard:** detailed development inspection of resources, endpoints, logs, traces, metrics, configuration, and lifecycle.
- **Workbench Health page:** application-specific interpretation of live health through groups, nodes, dependencies, and availability.
- **Workbench Topology page:** text-based explanation of the intended architecture.

Do not place credentials, connection strings, request bodies, customer identifiers, order identifiers, product identifiers, or idempotency keys in normal telemetry or metric dimensions.

See [Correlation and trace context](docs/13-correlation-id-logging.md), [Observability and operations](docs/16-observability-and-operations.md), and [Service defaults](src/Hosting/Hosting.ServiceDefaults/README.md).

## Documentation

Recommended reading path:

1. [Problem](docs/01-problem.md)
2. [Microservices design](docs/02-microservices-design.md)
3. [Virtual actors design](docs/03-virtual-actors-design.md)
4. [Trade-offs](docs/07-tradeoffs.md)
5. [Scenario guide](docs/12-scenario-guide.md)
6. [Local validation](docs/09-local-validation.md)
7. [Observability and operations](docs/16-observability-and-operations.md)
8. [Known limitations](docs/17-known-limitations.md)

See the [documentation index](docs/README.md) for categorized reading paths and links to every detailed document.

## Contributing

Contributions are welcome. Use GitHub Issues for reproducible bugs and concrete feature requests, and GitHub Discussions for questions, observations, and early architecture ideas.

Read [CONTRIBUTING.md](CONTRIBUTING.md) before making a change.

## Scope and interpretation

Keep these guardrails in mind:

- The repository is not a benchmark.
- Local timings depend on the machine, runtime state, persistence, topology, and workload.
- Aspire is the supported development composition, not a production deployment blueprint.
- The sample does not provide production security, recovery, reconciliation, scaling, telemetry retention, alerting, or incident management.
- Health does not prove business correctness.
- The comparison demonstrates trade-offs rather than declaring a winner.

See [Known limitations](docs/17-known-limitations.md) and [Out of scope](docs/18-out-of-scope.md).

## Key takeaway

The useful question is not whether microservices or virtual actors are universally better. It is how each style expresses and evolves state ownership, concurrency, coordination, compensation, idempotency, deployment, observability, and operational responsibility.

The best fit depends on workload identity, consistency requirements, team ownership, deployment boundaries, platform maturity, and expected evolution. See [Trade-offs](docs/07-tradeoffs.md) for the detailed comparison.
