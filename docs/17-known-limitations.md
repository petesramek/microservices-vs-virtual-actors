# Known limitations and interpretation guide

This document explains what the architecture workbench does not prove, which concerns are intentionally simplified, and how to interpret its results responsibly.

The repository is a teaching and comparison tool. It is not a production system, a production reference architecture, or a controlled benchmark.

## Comparison scope

The workbench is designed to make these concerns visible:

- state ownership
- inventory invariants
- workflow coordination
- idempotency
- concurrency and contention
- compensation
- timeout policy
- compatibility and evolution
- development observability and health

It is not designed to prove that one architecture is always faster, simpler, safer, cheaper, easier to deploy, or easier to operate.

The strongest comparison dimension is state ownership. The scenarios help readers ask:

- Who owns inventory state?
- Who protects the inventory invariant?
- Who owns idempotency state?
- Who owns the order workflow decision?
- Who coordinates compensation?
- What happens when many requests target the same state identity?

The repository demonstrates how two architecture styles express those responsibilities. It does not compare every concern found in a production platform.

## This is not a benchmark

Elapsed times in the Workbench UI are local observations. They help explain the sample topology, but they are not general performance results.

Local timings can be affected by:

- machine performance
- process and runtime warmup
- local HTTP overhead
- SQLite behavior
- Orleans activation and scheduling
- logging, tracing, and metric overhead
- gateway orchestration
- resource contention
- whether requests target one hot identity or many independent identities

A credible performance study would require:

- controlled and repeatable infrastructure
- defined workloads and warmup
- several capacity levels
- latency distributions and percentiles
- throughput and error rates
- CPU, memory, network, and storage measurements
- equivalent persistence and consistency assumptions
- documented runtime and deployment configuration
- repeated statistical analysis

A local result such as one implementation completing a scenario faster means only that it completed faster in that run and topology. It must not be generalized into an architectural performance claim.

## The development topology affects observations

The repository uses a deliberately small .NET Aspire development topology.

The microservices path crosses explicit HTTP and persistence boundaries involving `Workbench.Gateway`, `Orders.Api`, `Inventory.Api`, and `Payments.Api`.

The virtual actor path crosses `Workbench.Gateway`, `Ordering.Api`, `Ordering.Silo`, Orleans grain calls, and grain-state persistence.

These paths are not production-equivalent deployments. Their local timing, resource use, failure behavior, and operational surface reflect the sample composition and configuration.

The Aspire AppHost is the supported development composition. It is not presented as a production deployment blueprint.

## Simplified domain model

The order workflow is intentionally narrow. It models:

- inventory reservation
- payment authorization
- order completion or rejection
- selected compensation behavior
- concurrent and duplicate submissions

It does not model many concerns common in real commerce or workflow systems, including:

- pricing and promotions
- tax
- fulfillment and shipping
- warehouse selection
- fraud and risk evaluation
- asynchronous confirmation
- cancellation and refund lifecycles
- customer communication
- manual review
- reconciliation and repair workflows

The small domain keeps state ownership and coordination visible. It should not be mistaken for a complete order-management model.

## Simplified persistence

The repository uses SQLite-based persistence suitable for local development and deterministic comparison.

A production data platform would require deliberate decisions for:

- durable database hosting
- transaction isolation
- optimistic or pessimistic concurrency
- schema and state migration
- backup and restore
- retention and archival
- reconciliation
- outbox and inbox patterns
- multi-instance coordination
- disaster recovery
- regional replication
- storage performance and capacity

The sample demonstrates ownership and state transitions. It does not provide a production persistence strategy.

## Simplified payment behavior

Payment behavior is deterministic so scenario outcomes remain understandable.

The workbench models:

- successful authorization
- explicit authorization failure
- timeout treated as failed authorization

A real payment system may require:

- provider-specific idempotency
- asynchronous callbacks
- authorization and capture separation
- settlement delays
- cancellation and refund flows
- fraud checks
- provider reconciliation
- duplicate provider responses
- manual review
- ambiguous and late outcomes

The payment components are comparison collaborators, not payment-provider integrations.

## Simplified timeout and compensation policy

The payment-timeout scenario uses this deterministic policy:

```text
reserve inventory
payment times out
release inventory
reject order
```

In a real system, timeout is ambiguous. The remote operation may have completed after the caller stopped waiting.

A production workflow might instead use:

```text
reserve inventory
payment times out
mark order pending confirmation
reconcile payment outcome
complete or compensate after confirmation
```

That policy requires durable workflow state, reconciliation, retry rules, retention, alerts, and operational ownership.

The sample also assumes that inventory compensation succeeds. It does not implement compensation retry, repair, or reconciliation when release fails.

## Simplified concurrency model

The scenarios demonstrate correct inventory protection and idempotent duplicate handling under controlled local concurrency.

They do not prove correctness under every combination of:

- several service instances
- several Orleans silos
- process or silo failure during a workflow
- database failover
- network partitions
- delayed or reordered messages
- retries from external clients
- cross-region traffic
- extreme hot-key or hot-identity load

The repository intentionally avoids a deliberately unsafe race-condition comparison. Comparing a broken implementation with a correct one would demonstrate an anti-pattern, not provide a fair architecture comparison.

## Simplified idempotency lifecycle

The duplicate-request scenario uses one controlled order identity and idempotency key.

A production idempotency design must also define:

- key scope and ownership
- request-equivalence rules
- behavior for in-progress duplicates
- replay of failed or rejected outcomes
- result retention
- cleanup and archival
- multi-instance coordination
- restart and recovery behavior
- abuse and resource-exhaustion protection

The sample demonstrates the observable requirement that one logical request must not create several logical orders or reserve inventory several times.

## Development observability, not a production platform

The repository includes meaningful development observability:

- W3C trace context
- .NET `Activity` and `ActivitySource` instrumentation
- OpenTelemetry traces and metrics
- structured logging and `X-Correlation-ID` propagation
- scenario activities and bounded metrics
- custom trace collection and sampling
- readiness and liveness endpoints
- topology-aware Health page evaluation
- Aspire dashboard views for resources, logs, traces, and metrics

These capabilities are sufficient for understanding and diagnosing the local workbench. They do not provide a complete production observability platform.

Production use would still require decisions for:

- telemetry storage and retention
- access control and tenant isolation
- sensitive-data governance
- alerting and escalation
- service-level objectives
- dashboard ownership
- trace-sampling cost and policy
- long-term metric and log compatibility
- incident response
- cross-region collection and availability

The Aspire dashboard is a development diagnostics instrument. The Workbench Health page provides application-specific interpretation. Neither replaces a production monitoring and incident-management system.

See [Correlation and trace context](13-correlation-id-logging.md) and [Observability and operations](16-observability-and-operations.md).

## Health does not prove business correctness

The repository distinguishes:

- liveness
- readiness
- service availability
- direct resource health
- dependency health
- group health

These signals help diagnose the development topology. They do not prove that:

- a workflow preserves its invariants
- compensation completed correctly
- an idempotency rule was respected
- contracts are semantically compatible
- the next request will succeed

Health checks and scenario validation answer different questions. Both are necessary for the workbench, and neither substitutes for the other.

## Topology is explanatory

The Workbench Topology page is a text-based explanation of the intended architecture. It does not discover the production estate or prove that a displayed dependency is currently reachable.

Live topology-aware observations belong on the Health page. Detailed runtime resources and telemetry belong in the Aspire dashboard.

The shared topology model is intentionally small and configured for this repository. A production topology system would need broader discovery, ownership, versioning, access control, and lifecycle rules.

## No production security model

The repository does not implement a complete production security model.

Production use would require decisions for:

- user authentication and authorization
- service-to-service identity
- secrets and certificate management
- transport and network security
- input hardening
- rate limiting and abuse protection
- audit logging
- data classification and privacy
- least-privilege access
- dependency and supply-chain security
- secure configuration and deployment

Security features should be designed around the actual deployment and threat model. Their absence from the sample must not be interpreted as a recommended production approach.

## No production deployment or scaling platform

The repository uses Aspire for local development and composition. It does not provide a production deployment platform.

Production deployment would require decisions for:

- service and silo packaging
- environment configuration
- networking and service discovery
- ingress and load balancing
- secrets management
- persistent storage
- automated migrations
- rolling upgrades
- rollback and recovery
- autoscaling
- capacity planning
- multi-region behavior
- operational ownership

The deployment and scaling documents discuss these concerns architecturally. They do not prescribe one production platform.

## Scenario results are semantic contracts

The normalized result fields have deliberate meanings:

- total request submissions
- unique successful orders
- rejected submissions
- idempotent duplicate responses
- remaining inventory
- elapsed time
- terminal reason
- explanatory timeline

A compatible property name or type does not guarantee compatible behavior.

For example:

- unique successful orders are logical orders, not successful HTTP responses
- rejected submissions are logical rejections, not every technical failure
- idempotent duplicate responses are repeated submissions that returned an established logical result
- elapsed time is local feedback, not benchmark evidence

These meanings are documented in the [Scenario guide](12-scenario-guide.md) and protected by acceptance and regression tests.

## Scenario-specific simplifications

### Successful order

Payment always succeeds. Fulfillment, shipping, fraud, asynchronous downstream work, and post-order lifecycle are not modeled.

### Insufficient inventory

Rejection is immediate and deterministic. Backorders, substitutions, warehouse allocation, customer priority, and partial fulfillment are not modeled.

### Payment failure compensation

Payment failure is explicit and inventory release succeeds. Compensation failure, retry, and reconciliation are not modeled.

### Payment timeout after reservation

Timeout is treated as failed authorization. Pending confirmation and late provider outcomes are not modeled.

### Concurrent orders

Orders use a small product and quantity model. Reservation expiry, fairness, pricing changes, and multi-location stock are not modeled.

### Hot product contention

Contention is concentrated on one product identity. Repartitioning, admission queues, quotas, caches, and product-specific scaling strategies are not implemented.

### Duplicate request

Duplicate submissions use one controlled order identity and idempotency key. Retention, mismatched payloads, malicious key reuse, and distributed cleanup are not modeled.

## No universal architecture winner

Microservices can be a strong fit when:

- business capabilities and team ownership are clear
- independent deployment has real value
- explicit integration contracts are acceptable
- different capabilities need different scaling or release cycles
- the organization can operate distributed services effectively

Virtual actors can be a strong fit when:

- state is naturally partitioned by durable identity
- per-identity coordination supports important invariants
- many independent identities dominate the workload
- runtime-managed activation and placement are acceptable
- the organization can operate and evolve the actor runtime and state model

Both styles can be implemented well or poorly. Real systems can also combine them.

## How to interpret the repository

Use the repository to ask:

- Where does state live?
- Who owns each invariant?
- How does concurrency behave under contention?
- How is idempotency protected?
- How are failure and compensation policies expressed?
- Which compatibility boundaries must evolve safely?
- What runtime evidence is available during diagnosis?
- Which maintenance burden moves where?

Do not use the repository to claim:

- one architecture is always faster
- one architecture is always simpler
- one architecture is always cheaper
- one architecture removes the need for testing
- one architecture removes versioning or migration concerns
- one architecture removes operational complexity
- local health or timing proves production readiness

## Key takeaways

- The repository is an architecture teaching and comparison tool, not a production reference architecture
- Local timings are observations, not benchmark conclusions
- State ownership, invariants, idempotency, and failure policy are the main comparison dimensions
- Domain, persistence, payment, timeout, reconciliation, security, deployment, and scaling are intentionally simplified
- The repository includes useful development observability, but not a production telemetry and incident-management platform
- Health and topology views support understanding and diagnosis, they do not prove business correctness or production readiness
- Both microservices and virtual actors require deliberate compatibility, testing, operations, and maintenance

## Related documentation

- [Problem](01-problem.md)
- [Microservices design](02-microservices-design.md)
- [Virtual actors design](03-virtual-actors-design.md)
- [Deployment comparison](05-deployment-comparison.md)
- [Scaling comparison](06-scaling-comparison.md)
- [Trade-offs](07-tradeoffs.md)
- [Organizational scaling and architecture fit](08-organizational-scaling-and-architecture-fit.md)
- [Scenario guide](12-scenario-guide.md)
- [Correlation and trace context](13-correlation-id-logging.md)
- [Release, versioning, and rollback](14-release-versioning-and-rollback.md)
- [Maintenance and evolution](15-maintenance-and-evolution.md)
- [Observability and operations](16-observability-and-operations.md)
- [Out of scope](18-out-of-scope.md)
