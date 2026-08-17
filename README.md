# Microservices vs Virtual Actors

> **README planning draft:** This file defines the agreed structure for the repository README. Each section currently contains a concise content brief. Replace the briefs one section at a time with final reader-facing content while keeping the root README compact and linking to deeper documentation.

<!--
README goals
- Explain the repository in approximately five minutes.
- Target 1,200 to 1,800 words when complete.
- Keep implementation detail in project READMEs and docs.
- Avoid duplicating scenario matrices, operational guides, or exhaustive documentation indexes.
- Create a new document only when no existing document is an appropriate home.
-->

## What you can explore

**Section purpose:** Introduce the practical questions the workbench helps readers investigate.

Include six to eight concise points covering:

- state ownership and invariant protection;
- workflow coordination;
- concurrency and hot-identity contention;
- idempotency under concurrent duplicate submissions;
- compensation after payment failure or timeout;
- deployment, scaling, and operational differences;
- maintenance and organizational fit.

Keep detailed answers in:

- [Microservices design](docs/02-microservices-design.md)
- [Virtual actors design](docs/03-virtual-actors-design.md)
- [Trade-offs](docs/07-tradeoffs.md)
- [Organizational scaling and architecture fit](docs/08-organizational-scaling-and-architecture-fit.md)

## Architecture at a glance

**Section purpose:** Give readers a compact mental model of the compared implementations and the Workbench.

Include one small request-flow diagram:

```text
Workbench.Ui
  -> Workbench.Gateway
      -> Microservices: Orders.Api -> Inventory.Api + Payments.Api
      -> Virtual actors: Ordering.Api -> Orleans grains
```

Keep this section conceptual. Do not include persistence internals, complete deployment topology, compatibility analysis, or project-level implementation detail.

### Microservices

Summarize in two or three bullets:

- `Orders.Api` coordinates the workflow;
- `Inventory.Api` owns inventory and reservation invariants;
- `Payments.Api` owns payment behavior;
- communication crosses explicit HTTP boundaries.

Link to:

- [Microservices folder overview](src/Microservices/README.md)
- [Microservices design](docs/02-microservices-design.md)

### Virtual actors

Summarize in two or three bullets:

- `OrderGrain` owns an order identity;
- `InventoryItemGrain` owns a product identity;
- `PaymentAccountGrain` owns a customer payment identity;
- Orleans serializes calls for each grain identity.

Link to:

- [Virtual actors folder overview](src/VirtualActors/README.md)
- [Virtual actors design](docs/03-virtual-actors-design.md)

## Workbench experience

**Section purpose:** Explain what readers can open and explore in the interactive UI. Keep the four views distinct.

### Scenario runner

Explain that the scenario runner:

- executes deterministic workflows against either or both implementations;
- supports scenario defaults and advanced inputs;
- presents normalized results side by side;
- covers concurrency, idempotency, compensation, timeout, and contention behavior.

Link to:

- [UI dashboard](docs/10-ui-dashboard.md)
- [Scenario guide](docs/12-scenario-guide.md)
- [Workbench folder overview](src/Workbench/README.md)

### Health

Explain that the Health page:

- combines live health reports with the shared topology model;
- organizes resources into architecture groups, nodes, and dependencies;
- evaluates required and optional dependency health;
- presents readiness, liveness, and current resource availability.

Clarify that health indicates runtime reachability and readiness, not business correctness.

Link to:

- [Observability and operations](docs/16-observability-and-operations.md)
- [Health model](src/Observability/Observability.Health/README.md)
- [Topology model](src/Observability/Observability.Topology/README.md)

### Topology

Explain that the Topology page:

- presents the intended architecture;
- explains service, actor, and dependency relationships;
- is a static explanatory view rather than a live availability dashboard.

State explicitly that runtime dependency health and current resource availability belong on the Health page.

### Trade-offs

Explain that the Trade-offs page provides a concise in-product comparison.

Keep detailed reasoning in:

- [Trade-offs](docs/07-tradeoffs.md)
- [Organizational scaling and architecture fit](docs/08-organizational-scaling-and-architecture-fit.md)

## Scenarios

**Section purpose:** Show the breadth of behavior without duplicating the complete scenario specification.

Include one sentence for each scenario:

- **Successful order:** inventory and payment succeed.
- **Insufficient inventory:** rejection occurs before payment.
- **Payment failure compensation:** reserved inventory is released after failure.
- **Payment timeout after reservation:** timeout is treated as failure and compensated.
- **Concurrent orders:** independent orders compete for limited stock.
- **Duplicate request:** concurrent duplicate submissions return one logical result.
- **Hot product contention:** many requests target one inventory identity.

Do not include expected count matrices or reason-code tables here. Link to the [Scenario guide](docs/12-scenario-guide.md) for defaults, expected counts, reasons, and architecture-specific interpretation.

## Result semantics

**Section purpose:** Prevent readers from confusing HTTP responses with logical order outcomes.

Define briefly:

- total request submissions;
- unique successful orders;
- rejected submissions;
- idempotent duplicate responses;
- remaining inventory;
- local elapsed time.

Keep examples and scenario-specific values in the [Scenario guide](docs/12-scenario-guide.md).

## Run locally

**Section purpose:** Provide the shortest reliable path to a working development environment.

### Prerequisites

Mention only verified prerequisites:

- the repository's required .NET SDK;
- Docker Desktop for Compose workflows;
- a suitable .NET development environment.

Do not state a specific SDK version unless repository configuration verifies it.

### Recommended: Aspire

Use the Aspire AppHost as the primary development path:

```bash
dotnet run --project src/Hosting/Hosting.AppHost/Hosting.AppHost.csproj
```

Explain that Aspire is used to:

- compose and start the development topology;
- provide service discovery and resource visibility;
- open service endpoints;
- inspect structured logs;
- inspect distributed traces;
- inspect metrics during development.

Link to:

- [Hosting overview](src/Hosting/README.md)
- [AppHost overview](src/Hosting/Hosting.AppHost/README.md)

### Alternative workflows

Mention briefly:

- `scripts/run-all-local.ps1`;
- architecture-specific startup scripts;
- `scripts/run-comparison.ps1`;
- `deploy/docker-compose.full.yml`.

Keep complete startup and troubleshooting steps in:

- [Local validation](docs/09-local-validation.md)
- [Deployment overview](deploy/README.md)

## Repository map

**Section purpose:** Help readers locate major areas without reproducing the full folder tree.

Use only a shallow map with links to the folder for user to be able navigate quickly:

```text
src/
  Hosting/
  Microservices/
  Observability/
  VirtualActors/
  Workbench/
tests/
deploy/
docs/
scripts/
```

Add one concise sentence per major area. Do not list individual classes, UI fragments, migrations, runtime databases, or internal namespace trees.

## Testing and validation

**Section purpose:** Explain the layers of confidence and provide standard validation commands.

Include:

```bash
dotnet restore
dotnet build microservices-vs-virtual-actors.slnx --configuration Release
dotnet test microservices-vs-virtual-actors.slnx --configuration Release --no-build
```

Summarize the four test projects:

- `Microservices.Tests` for the HTTP-service workflow;
- `VirtualActors.Tests` for Orleans workflow and SQLite grain persistence;
- `Workbench.AcceptanceTests` for externally visible gateway behavior;
- `Workbench.ScenarioRegressionTests` for normalized scenario-result semantics.

Mention:

- `scripts/test-build.ps1`;
- `scripts/validate-e2e.ps1`;
- `.github/workflows/build.yml`.

Keep exhaustive validation expectations in:

- [Local validation](docs/09-local-validation.md)
- [End-to-end validation](docs/11-end-to-end-validation.md)

## Observability in development

**Section purpose:** Distinguish the repository's observability surfaces and explain their development roles.

Cover briefly:

- `X-Correlation-ID` for request correlation;
- shared OpenTelemetry configuration;
- scenario traces and metrics;
- readiness and liveness;
- safe structured logging without request secrets or identifiers.

Make the distinction explicit:

- **Aspire dashboard:** development inspection of composed resources, logs, traces, and metrics.
- **Workbench Health page:** application-specific interpretation of live health through groups, nodes, dependencies, and availability.
- **Workbench Topology page:** static explanation of the intended architecture.

Link to:

- [Correlation ID logging](docs/13-correlation-id-logging.md)
- [Observability and operations](docs/16-observability-and-operations.md)
- [Service defaults](src/Hosting/Hosting.ServiceDefaults/README.md)
- [Health model](src/Observability/Observability.Health/README.md)
- [Topology model](src/Observability/Observability.Topology/README.md)

## Documentation

**Section purpose:** Provide a short reader journey rather than listing every document in the root README.

Recommended start-here path:

1. [Problem](docs/01-problem.md)
2. [Microservices design](docs/02-microservices-design.md)
3. [Virtual actors design](docs/03-virtual-actors-design.md)
4. [Trade-offs](docs/07-tradeoffs.md)
5. [Scenario guide](docs/12-scenario-guide.md)
6. [Local validation](docs/09-local-validation.md)
7. [Observability and operations](docs/16-observability-and-operations.md)
8. [Known limitations](docs/17-known-limitations.md)

Link to `docs/README.md` for the complete categorized documentation map.

### Documentation work required

Create `docs/README.md` because the repository currently needs a documentation landing page. Group the existing documents by reader intent:

- **Understand the problem:** documents 01 to 03.
- **Compare the architectures:** documents 04 to 08.
- **Run and validate:** documents 09 to 12.
- **Operate and evolve:** documents 13 to 16.
- **Interpret responsibly:** documents 17 and 18.

Do not create another new document unless the missing material cannot fit coherently into an existing document.

Add missing information to existing documents as follows:

- Aspire development telemetry belongs in `docs/16-observability-and-operations.md` and the Hosting READMEs.
- Health versus Topology behavior belongs in `docs/10-ui-dashboard.md` and `docs/16-observability-and-operations.md`.
- Workbench internals belong in `src/Workbench/README.md`.
- Scenario semantics belong in `docs/12-scenario-guide.md`.
- Validation behavior belongs in `docs/09-local-validation.md` and `docs/11-end-to-end-validation.md`.

## Scope and interpretation

**Section purpose:** Provide concise guardrails without duplicating the full limitations documents.

State that:

- the repository is not a benchmark;
- local timings depend on environment and topology;
- the sample is not production guidance for security, recovery, scaling, deployment, or monitoring;
- health does not prove business correctness;
- the repository demonstrates trade-offs rather than declaring a winner.

Link to:

- [Known limitations](docs/17-known-limitations.md)
- [Out of scope](docs/18-out-of-scope.md)

## Key takeaway

**Section purpose:** Close the narrative in one short paragraph.

Summarize that the repository does not try to prove one architecture is universally better. It demonstrates how microservices and virtual actors express state ownership, concurrency, coordination, compensation, idempotency, deployment, observability, and evolution differently.

Link to the detailed [Trade-offs](docs/07-tradeoffs.md).
