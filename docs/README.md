# Documentation

This directory contains the detailed design, comparison, validation, operational, and interpretation guidance for the **Microservices vs Virtual Actors** architecture workbench.

The repository root [README](../README.md) provides the short project introduction and supported startup path. Use this index when you want to understand a specific architecture decision, real-world trade-off, scenario, operational concern, or limitation in more depth.

The documentation has two complementary purposes:

- explain how this repository implements and validates the comparison
- use the repository as a focused illustration of broader real-world architecture concerns

## Choose a reading path

### New to the repository

Start with the modeled problem, understand both implementations, and then review the main trade-offs:

1. [Problem](01-problem.md)
2. [Microservices design](02-microservices-design.md)
3. [Virtual actors design](03-virtual-actors-design.md)
4. [Trade-offs](07-tradeoffs.md)
5. [Scenario guide](12-scenario-guide.md)

### Running and validating the workbench

Use this path when you want to start the repository through the .NET Aspire AppHost and verify its behavior:

1. [Local validation](09-local-validation.md)
2. [UI dashboard](10-ui-dashboard.md)
3. [End-to-end validation](11-end-to-end-validation.md)
4. [Scenario guide](12-scenario-guide.md)
5. [Observability and operations](16-observability-and-operations.md)

### Comparing architecture characteristics

Use this path when evaluating development, deployment, scaling, team ownership, release behavior, and long-term fit beyond the sample implementation:

1. [Development comparison](04-development-comparison.md)
2. [Deployment comparison](05-deployment-comparison.md)
3. [Scaling comparison](06-scaling-comparison.md)
4. [Trade-offs](07-tradeoffs.md)
5. [Organizational scaling and architecture fit](08-organizational-scaling-and-architecture-fit.md)
6. [Release, versioning, and rollback](14-release-versioning-and-rollback.md)
7. [Maintenance and evolution](15-maintenance-and-evolution.md)

### Operating and evolving the sample

Use this path when reviewing diagnostics, compatibility, release behavior, maintenance, and operational interpretation:

1. [Correlation and trace context](13-correlation-id-logging.md)
2. [Observability and operations](16-observability-and-operations.md)
3. [Release, versioning, and rollback](14-release-versioning-and-rollback.md)
4. [Maintenance and evolution](15-maintenance-and-evolution.md)
5. [Known limitations](17-known-limitations.md)
6. [Out of scope](18-out-of-scope.md)

## Understand the problem

### [01. Problem](01-problem.md)

Defines the modeled order workflow, the comparison goals, the primary ownership questions, and the conclusions the workbench is not intended to prove.

### [02. Microservices design](02-microservices-design.md)

Explains the repository's HTTP service boundaries, state ownership, orchestration, persistence, concurrency, idempotency, compensation, and development-observability model.

### [03. Virtual actors design](03-virtual-actors-design.md)

Explains the Orleans grain identities, identity-based state ownership, request scheduling, workflow coordination, persistence, silo hosting, and runtime responsibilities.

## Compare the architectures

Documents `04` through `08` are primarily real-world architecture comparisons. They use this repository as an illustration rather than treating its local topology as a production blueprint.

### [04. Development comparison](04-development-comparison.md)

Compares day-to-day development concerns, including boundaries, contracts, local reasoning, concurrency, testing, debugging, and runtime-specific knowledge.

### [05. Deployment comparison](05-deployment-comparison.md)

Compares real-world deployment boundaries, compatibility, rollout, failure isolation, platform requirements, state migration, and operational ownership. It distinguishes those concerns from the repository's Aspire-based development composition.

### [06. Scaling comparison](06-scaling-comparison.md)

Compares service replication, actor-runtime capacity, persistence constraints, workload partitioning, hot keys, hot identities, and the difference between adding capacity and relieving the actual bottleneck.

### [07. Trade-offs](07-tradeoffs.md)

Compares where each architecture places state ownership, concurrency, coordination, idempotency, failure policy, compatibility, scaling pressure, and operational complexity.

### [08. Organizational scaling and architecture fit](08-organizational-scaling-and-architecture-fit.md)

Discusses team ownership, business-capability boundaries, identity-oriented domains, product-quality risks, organizational maturity, hybrid designs, and long-term architecture fit.

## Run and validate

### [09. Local validation](09-local-validation.md)

Documents the supported local restore, build, test, Aspire startup, scenario, Health, Topology, telemetry, cancellation, and recovery checks.

### [10. UI dashboard](10-ui-dashboard.md)

Explains the four Workbench UI pages:

- The **Scenario runner** executes and compares deterministic workflows through both implementations
- The **Health page** combines live health reports with the shared topology model to present groups, nodes, dependencies, readiness, liveness, availability, and evaluated health
- The **Topology page** provides a text-based explanation of the intended architecture. It is not a live availability dashboard
- The **Trade-offs page** provides a concise in-product comparison and links the workbench experience to the deeper documentation

### [11. End-to-end validation](11-end-to-end-validation.md)

Defines complete validation across solution build and tests, Aspire composition, both implementations, all Workbench pages, scenario invariants, health, logs, traces, metrics, and recovery behavior.

### [12. Scenario guide](12-scenario-guide.md)

Documents every supported scenario, its default inputs, expected normalized result, implementation interpretation, architecture lesson, operational evidence, and evolution concerns.

## Operate and evolve

### [13. Correlation and trace context](13-correlation-id-logging.md)

Explains W3C trace context, .NET activities, OpenTelemetry propagation, structured log correlation, `X-Correlation-ID`, scenario instrumentation, and Aspire-based validation.

### [14. Release, versioning, and rollback](14-release-versioning-and-rollback.md)

Covers network and message contracts, actor interfaces, persistent state, semantic compatibility, expand-and-contract migration, mixed-version deployment, rollback, roll forward, in-flight work, and release testing.

### [15. Maintenance and evolution](15-maintenance-and-evolution.md)

Compares how both architecture styles absorb new features, ownership changes, persistence changes, dependency changes, operational growth, and long-term maintenance work.

### [16. Observability and operations](16-observability-and-operations.md)

Combines real-world operational guidance with the repository's implemented development-observability model, including:

- logs, distributed traces, metrics, and health
- W3C trace context and OpenTelemetry
- microservices and virtual actor diagnostic concerns
- scenario-aware operations
- alerting and incident investigation principles
- `Hosting.ServiceDefaults`
- scenario activities, metrics, and custom sampling
- `Observability.Health` and `Observability.Topology`
- the Aspire dashboard and Workbench Health page

The repository has complementary observability surfaces:

- The **Aspire dashboard** provides detailed development diagnostics for resources, endpoints, dependencies, structured logs, distributed traces, metrics, configuration, and lifecycle operations
- The **Workbench Health page** provides application-specific interpretation of live health through architecture groups, nodes, dependencies, availability, and evaluation rules
- The **Workbench Topology page** provides a static, text-based explanation of the intended architecture

## Interpret responsibly

### [17. Known limitations](17-known-limitations.md)

Explains the repository's technical and methodological limitations and identifies conclusions that the workbench does not support.

### [18. Out of scope](18-out-of-scope.md)

Defines the deliberately excluded production concerns, including complete commerce functionality, security, messaging, platform engineering, data management, Orleans operations, resilience, observability operations, benchmarking, and Workbench productization.

It also explains how a future topic can become a valid comparison when it introduces a clear architectural question without turning the repository into a production platform template.

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

- update the narrowest relevant document
- keep scenario defaults, expected results, tests, and UI guidance synchronized
- keep the Health page, Topology page, and Aspire dashboard responsibilities distinct
- keep real-world comparison guidance separate from repository-specific startup and implementation detail
- update project READMEs when source structure, configuration, or project responsibilities change
- update the root README only when repository-level purpose, navigation, or supported startup changes
- preserve relative links and verify them before merging
- avoid repeating detailed guidance across several documents

When adding a new document:

1. Confirm that the material does not fit coherently into an existing document.
2. Decide whether it is repository-specific or a broader architecture comparison.
3. Use the next available numeric prefix when it belongs in the ordered narrative.
4. Add it to the appropriate section and reading path in this index.
5. Link it from the root or project README only when it is relevant to that reader journey.
