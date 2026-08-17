# Documentation

This directory contains the detailed design, comparison, validation, operational, and interpretation guidance for the **Microservices vs Virtual Actors** architecture workbench.

The repository root [README](../README.md) provides the short project introduction and startup path. Use this documentation index when you want to understand a specific architecture decision, scenario, operational concern, or limitation in more depth.

## Choose a reading path

### New to the repository

Start with the problem, understand both implementations, and then review the main trade-offs:

1. [Problem](01-problem.md)
2. [Microservices design](02-microservices-design.md)
3. [Virtual actors design](03-virtual-actors-design.md)
4. [Trade-offs](07-tradeoffs.md)
5. [Scenario guide](12-scenario-guide.md)

### Running and validating the workbench

Use this path when you want to start the repository through the Aspire AppHost and verify its behavior:

1. [Local validation](09-local-validation.md)
2. [UI dashboard](10-ui-dashboard.md)
3. [End-to-end validation](11-end-to-end-validation.md)
4. [Scenario guide](12-scenario-guide.md)
5. [Observability and operations](16-observability-and-operations.md)

### Comparing architecture characteristics

Use this path when evaluating development, deployment, scaling, team ownership, and long-term fit:

1. [Development comparison](04-development-comparison.md)
2. [Deployment comparison](05-deployment-comparison.md)
3. [Scaling comparison](06-scaling-comparison.md)
4. [Trade-offs](07-tradeoffs.md)
5. [Organizational scaling and architecture fit](08-organizational-scaling-and-architecture-fit.md)
6. [Maintenance and evolution](15-maintenance-and-evolution.md)

### Operating and evolving the sample

Use this path when reviewing diagnostics, compatibility, release behavior, maintenance, and operational interpretation:

1. [Correlation ID logging](13-correlation-id-logging.md)
2. [Release, versioning, and rollback](14-release-versioning-and-rollback.md)
3. [Maintenance and evolution](15-maintenance-and-evolution.md)
4. [Observability and operations](16-observability-and-operations.md)
5. [Known limitations](17-known-limitations.md)
6. [Out of scope](18-out-of-scope.md)

## Understand the problem

### [01. Problem](01-problem.md)

Defines the modeled order workflow, the comparison goals, and the questions the workbench is intended to make visible.

### [02. Microservices design](02-microservices-design.md)

Explains the HTTP service boundaries, state ownership, orchestration responsibilities, persistence boundaries, and failure handling in the microservices implementation.

### [03. Virtual actors design](03-virtual-actors-design.md)

Explains the Orleans grain identities, state ownership, serialized execution model, workflow coordination, persistence, and runtime responsibilities in the virtual actor implementation.

## Compare the architectures

### [04. Development comparison](04-development-comparison.md)

Compares implementation structure, local development, testing, debugging, contracts, and day-to-day engineering concerns.

### [05. Deployment comparison](05-deployment-comparison.md)

Compares deployment units, startup dependencies, compatibility concerns, persistence, rollout, and operational surface area.

### [06. Scaling comparison](06-scaling-comparison.md)

Compares service-boundary scaling, identity-based scaling, contention, hot resources, and capacity considerations.

### [07. Trade-offs](07-tradeoffs.md)

Summarizes the main benefits, costs, and situations in which each architecture style may be a better fit.

### [08. Organizational scaling and architecture fit](08-organizational-scaling-and-architecture-fit.md)

Discusses team ownership, organizational boundaries, product maturity, operational capability, and long-term architecture fit.

## Run and validate

### [09. Local validation](09-local-validation.md)

Documents the supported local startup and validation flow through the .NET Aspire AppHost.

### [10. UI dashboard](10-ui-dashboard.md)

Explains the Workbench UI, including the scenario runner, Health page, Topology page, and Trade-offs page.

Keep these distinctions clear:

- The **Scenario runner** executes and compares deterministic workflows.
- The **Health page** combines live health reports with the shared topology model to present groups, nodes, dependencies, readiness, liveness, and resource availability.
- The **Topology page** explains the intended architecture and dependency relationships. It is not a live availability dashboard.
- The **Trade-offs page** provides a concise in-product comparison.

### [11. End-to-end validation](11-end-to-end-validation.md)

Defines full-system validation expectations across the UI, gateway, compared implementations, persistence, and normalized scenario results.

### [12. Scenario guide](12-scenario-guide.md)

Documents every supported scenario, its defaults, expected result semantics, reason values, architecture interpretation, and validation expectations.

## Operate and evolve

### [13. Correlation ID logging](13-correlation-id-logging.md)

Explains correlation propagation and how related requests and logs can be traced across the Workbench Gateway and compared backends.

### [14. Release, versioning, and rollback](14-release-versioning-and-rollback.md)

Covers shared-contract compatibility, API and grain interface evolution, persisted-state changes, deployment ordering, and rollback limitations.

### [15. Maintenance and evolution](15-maintenance-and-evolution.md)

Compares how each architecture absorbs new features, scenario changes, persistence changes, dependency changes, and ongoing maintenance work.

### [16. Observability and operations](16-observability-and-operations.md)

Documents logs, traces, metrics, correlation, health, topology-aware evaluation, readiness, liveness, and operational interpretation.

The repository has complementary observability surfaces:

- The **Aspire dashboard** is the development diagnostics dashboard for composed resources, endpoints, dependencies, structured logs, distributed traces, metrics, configuration, and lifecycle operations.
- The **Workbench Health page** provides application-specific interpretation of live health through architecture groups, nodes, dependencies, and availability.
- The **Workbench Topology page** provides a static explanation of the intended architecture.

## Interpret responsibly

### [17. Known limitations](17-known-limitations.md)

Explains the sample's technical and methodological limitations and identifies conclusions that the workbench does not prove.

### [18. Out of scope](18-out-of-scope.md)

Lists concerns intentionally excluded from the sample, including production security, recovery, reconciliation, multi-region operation, and controlled performance benchmarking.

## Project documentation

Implementation-specific documentation is located beside the relevant source:

- [Hosting](../src/Hosting/README.md)
- [Aspire AppHost](../src/Hosting/Hosting.AppHost/README.md)
- [Service defaults](../src/Hosting/Hosting.ServiceDefaults/README.md)
- [Microservices](../src/Microservices/README.md)
- [Virtual actors](../src/VirtualActors/README.md)
- [Health model](../src/Observability/Observability.Health/README.md)
- [Topology model](../src/Observability/Observability.Topology/README.md)
- [Workbench](../src/Workbench/README.md)
- [Workbench contracts](../src/Workbench/Workbench.Contracts/README.md)
- [Workbench Gateway](../src/Workbench/Workbench.Gateway/README.md)
- [Workbench UI](../src/Workbench/Workbench.Ui/README.md)

## Maintaining the documentation

When behavior changes:

- update the narrowest relevant document;
- keep scenario defaults, expected results, tests, and UI guidance synchronized;
- keep Health, Topology, and Aspire dashboard responsibilities distinct;
- update project READMEs when source structure or configuration changes;
- update the root README only when repository-level navigation, startup, or purpose changes;
- preserve relative links and verify them before merging;
- avoid repeating detailed guidance across several documents.

When adding a new document:

1. Confirm the material does not fit coherently into an existing document.
2. Use the next available numeric prefix when the document belongs in the ordered narrative.
3. Add the document to the appropriate section and reading path in this index.
4. Link it from the root or project README only when it is relevant to that reader journey.
