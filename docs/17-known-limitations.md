# Known limitations and interpretation guide

This document explains what the comparison sample does not prove, what is intentionally simplified, and how to interpret results responsibly.

The project is useful as an architecture comparison case study. It is not a production system, a production reference architecture, or a universal benchmark.

## Summary

The sample is designed to compare architectural trade-offs around:

- state ownership
- inventory invariants
- idempotency
- concurrency under contention
- compensation
- timeout policy
- release and versioning concerns
- operational diagnostics

The sample is not designed to prove that one architecture is always faster, simpler, safer, cheaper, or easier to operate than the other.

## This is not a benchmark

Elapsed times in the UI are useful for understanding local demo behavior, but they should not be interpreted as general production performance results.

Local elapsed time is affected by:

- machine performance
- process startup state
- local HTTP overhead
- SQLite or local storage behavior
- Orleans local runtime behavior
- logging overhead
- gateway orchestration shape
- whether services are warm or cold
- whether requests hit one hot identity or multiple independent identities

A production benchmark would need:

- controlled infrastructure
- repeatable load profile
- warmup period
- statistical sampling
- latency percentiles
- throughput measurements
- CPU, memory, and network metrics
- database and storage metrics
- realistic deployment topology
- comparable scaling strategy for both designs

This sample intentionally avoids presenting itself as that kind of benchmark.

## Local topology affects results

The microservices path and virtual actor path are not deployed in a production-equivalent topology.

The microservices path includes explicit HTTP boundaries between services such as:

- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`

The virtual actor path uses Orleans-style grain interactions behind `Ordering.Api`.

This means local timing differences often reflect the sample topology and communication paths more than inherent architecture performance.

A result such as:

```text
Microservices elapsed: 1300 ms
Virtual Actors elapsed: 170 ms
```

should be read as:

```text
In this local sample topology, this scenario completed faster through the virtual actor path.
```

It should not be read as:

```text
Virtual actors are always faster than microservices.
```

## The comparison focuses on state ownership

The strongest comparison dimension in this project is state ownership.

The scenarios are designed to make these questions visible:

- Who owns inventory state?
- Who protects the inventory invariant?
- Who owns idempotency state?
- Who owns order workflow decisions?
- Who coordinates compensation?
- What happens when many requests target the same state identity?

The project compares how the two styles express those responsibilities.

The project does not attempt to compare every aspect of architecture, such as:

- team structure in a large organization
- cloud cost at scale
- multi-region replication
- compliance requirements
- event-driven integrations
- data warehouse integration
- zero-downtime production deployment pipelines

## Simplified persistence model

The sample uses lightweight local persistence patterns suitable for a demo.

A production implementation would need stronger consideration of:

- database migrations
- backup and restore
- transaction isolation
- optimistic or pessimistic concurrency
- outbox and inbox patterns
- idempotency record retention
- schema evolution
- data retention and archival
- disaster recovery

The sample demonstrates the shape of state ownership, not a complete production data platform.

## Simplified payment model

The payment scenarios are intentionally simplified.

The sample models:

- successful payment authorization
- explicit payment failure
- payment timeout treated as failure for demo determinism

Real payment systems often require more complex handling:

- asynchronous provider callbacks
- authorization versus capture
- cancellation and refund flows
- provider-specific timeout behavior
- reconciliation jobs
- duplicate provider requests
- settlement delays
- fraud checks
- manual review

The sample uses deterministic payment behavior so the architecture comparison remains clear.

## Timeout behavior is simplified

The payment timeout after reservation scenario treats timeout as failure:

```text
reserve inventory
payment times out
release inventory
reject order
```

This is intentionally simple and deterministic.

In production, timeout is ambiguous. A payment provider might eventually complete the operation even after the caller times out. A production system might choose a different policy:

```text
reserve inventory
payment times out
mark order pending confirmation
reconcile later
complete or reject after confirmation
```

That more realistic policy introduces additional lifecycle complexity. The sample documents the ambiguity but does not implement the full pending and reconciliation lifecycle.

## No full distributed tracing backend

The sample uses `X-Correlation-ID` for lightweight correlation.

This is useful for local diagnostics, but it is not a full tracing platform.

Production applications should generally use end-to-end OpenTelemetry-based observability. A production-grade observability stack would likely include:

- W3C Trace Context
- .NET `Activity` and `ActivitySource`
- OpenTelemetry instrumentation
- trace export
- metrics export
- structured log aggregation
- dashboards
- alerting
- service-level objectives

The current implementation keeps observability understandable and low-friction for the demo. See `13-correlation-id-logging.md` and `16-observability-and-operations.md` for the detailed observability guidance.

## No production security model

The sample does not implement a full production security model.

Production systems would require:

- authentication
- authorization
- service-to-service identity
- secrets management
- transport security
- input hardening
- audit logging
- least-privilege access
- dependency scanning
- secure deployment configuration

The current project focuses on architecture workflow behavior rather than security hardening.

## No production deployment platform

The sample can be run locally and documented as a deployable architecture, but it is not a complete production platform.

A production deployment would need:

- containerization or service packaging
- environment-specific configuration
- health checks
- readiness and liveness probes
- autoscaling rules
- rolling deployment strategy
- database migration pipeline
- rollback procedures
- secrets management
- monitoring and alerting

The release and deployment documentation describes these concerns conceptually. The sample does not implement all of them.

## No universal architecture winner

The project should not be interpreted as proving that microservices or virtual actors are universally better.

Microservices can be a strong fit when:

- teams need independently deployable business capabilities
- service boundaries align with ownership boundaries
- technology independence matters
- integration boundaries are explicit and stable
- operational teams are comfortable with distributed systems

Virtual actors can be a strong fit when:

- identity-based state ownership is central
- per-identity concurrency control is valuable
- workflows are naturally expressed around stateful identities
- runtime-managed activation and placement are acceptable
- teams are comfortable with the actor runtime model

Both styles can be implemented well or poorly.

## Scenario results are semantic contracts

The scenario result fields are intentionally consistent:

- total request submissions
- unique successful orders
- rejected submissions
- idempotent duplicate responses
- remaining inventory
- elapsed time
- reason

Changing the meaning of these values is a behavior change.

For example:

- `UniqueSuccessfulOrders` means unique successful logical orders, not raw successful HTTP responses.
- `RejectedSubmissions` means rejected logical submissions, not every technical failure.
- `IdempotentDuplicateResponses` means duplicate submissions that returned an existing logical result.

These meanings are documented in `12-scenario-guide.md` and protected by regression tests.

## Race conditions are not intentionally demonstrated

The sample intentionally demonstrates correct inventory protection under concurrent scenarios.

It does not include an intentionally unsafe inventory race demo because that would compare a deliberately broken microservice path against a normal actor path. That can be educational in an anti-pattern lab, but it would be less fair as part of the main architecture comparison.

The current comparison focuses on correct implementations and the different ways each style expresses correctness.

## Known simplifications by scenario

### Successful order

Simplified because payment always succeeds and no fulfillment, shipping, fraud, or asynchronous downstream processes are modeled.

### Insufficient inventory

Simplified because inventory rejection is immediate and deterministic. Real systems may support backorders, substitutions, allocation priority, or warehouse-specific stock.

### Payment failure with compensation

Simplified because compensation always succeeds. Real systems need to handle compensation failures and retries.

### Payment timeout after reservation

Simplified because timeout is treated as failure. Real systems may hold a pending state and reconcile later.

### Concurrent orders

Simplified because all orders use the same basic quantity and product model. Real systems may include warehouses, reservation expiry, customer priority, fraud checks, and pricing changes.

### Hot product contention

Simplified because contention is limited to one product identity. Real systems may use partitioning, queues, sharding, cache layers, or product-specific scaling strategies.

### Duplicate request

Simplified because duplicate submissions use a controlled scenario and one idempotency key. Real systems need idempotency retention policy, replay rules, client retry guidance, and protection across multiple service instances.

## How to interpret the project

Use the project to ask:

- Where does state live?
- Who owns each invariant?
- How does concurrency behave under pressure?
- How is idempotency protected?
- How are failures compensated?
- How are releases versioned?
- How would the system be diagnosed in production?
- What maintenance burden moves where?

Do not use the project to claim:

- one architecture is always faster
- one architecture is always simpler
- one architecture removes the need for testing
- one architecture removes versioning concerns
- one architecture removes operational complexity

## Key takeaways

- The sample is a teaching and comparison tool, not a production reference architecture.
- Timings are local-demo observations, not benchmark conclusions.
- Correctness and state ownership are the most important comparison dimensions.
- Timeout, payment, persistence, security, observability, and deployment are intentionally simplified.
- Both microservices and virtual actors require release, versioning, testing, and operational discipline.
