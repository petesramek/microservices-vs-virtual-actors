# Out of scope

This repository intentionally keeps the workflow and infrastructure small.

The goal is to compare how the same stateful distributed workflow looks when implemented with service/capability boundaries versus stateful identity boundaries. The repository is not intended to become a complete ecommerce platform, a production platform template, or a benchmark suite.

## Why scope is limited

The sample workflow is intentionally narrow:

- place an order
- reserve inventory
- authorize payment
- complete or reject the order
- handle selected failure and concurrency scenarios

Keeping the workflow small makes the architectural trade-offs visible. Adding unrelated production features would make it harder to see the differences between the microservices implementation and the virtual actor implementation.

## Product features out of scope

The repository does not attempt to model a complete commerce domain.

The following product features are out of scope:

- frontend product UI
- shopping cart
- discounts
- taxes
- shipping
- refunds
- notifications
- customer account management
- product catalog management
- order history UI

These features can be useful in real systems, but they are not required to compare workflow coordination, state ownership, concurrency, idempotency, and failure handling.

## Security and identity out of scope

Authentication and authorization are out of scope.

The sample does not attempt to demonstrate:

- user sign-in
- API authorization policies
- OAuth or OpenID Connect integration
- role-based access control
- tenant isolation
- secret management strategy

Security is important in production systems, but adding full security infrastructure would distract from the core architecture comparison.

## Messaging and eventing out of scope

The current comparison does not include message brokers or event-sourced architecture.

The following topics are out of scope:

- message brokers
- asynchronous command processing
- event sourcing
- event replay
- event-driven projections
- transactional outbox patterns
- saga frameworks

This is intentional. The current comparison focuses on direct workflow coordination in two styles:

- service-to-service coordination in the microservices implementation
- grain-to-grain coordination in the virtual actor implementation

Messaging patterns could be added later as a separate comparison dimension, but they are not required for the current purpose.

## Platform infrastructure out of scope

The repository does not include a full production platform stack.

The following infrastructure topics are out of scope:

- Kubernetes manifests
- Helm charts
- service mesh configuration
- ingress controller configuration
- distributed tracing backends
- centralized logging infrastructure
- production monitoring dashboards
- autoscaling policies
- production secrets management

The sample can run locally through Visual Studio, scripts, or Docker Compose. That is enough for the comparison goals.

## Production data management out of scope

The repository does not attempt to provide a production-grade data strategy.

The following topics are out of scope:

- production-grade database migration strategy
- cross-service schema governance
- backup and restore strategy
- data retention policy
- archival strategy
- multi-region replication
- production disaster recovery

The sample uses persistence only as much as needed to show state ownership, concurrency, compensation, and idempotency behavior.

## Production Orleans operations out of scope

The virtual actor implementation is intended to demonstrate actor-style workflow ownership and stateful identity boundaries.

The following production Orleans topics are out of scope:

- production Orleans clustering strategy
- production Orleans persistence providers
- multi-silo deployment tuning
- placement strategy tuning
- grain versioning strategy
- advanced streaming integration
- production dashboarding for Orleans runtime metrics

These topics matter for real Orleans systems, but they are not required to understand the architecture comparison in this repository.

## Performance benchmarking out of scope

This repository is not a benchmark suite.

Elapsed time shown in the UI is local feedback for the current run. It can help explain the local sample topology, but it should not be interpreted as a general performance result.

Production performance depends on many factors that are outside the scope of this sample, including network topology, persistence choices, runtime configuration, deployment shape, hot-key distribution, hardware, and operational tuning.

## What can be added later

Some out-of-scope topics could become useful future extensions if they help explain a specific comparison dimension.

Examples:

- adding a message broker to compare synchronous and asynchronous workflow coordination
- adding distributed tracing to compare operational diagnosis across both implementations
- adding production-like Orleans clustering to compare actor runtime deployment choices
- adding a more realistic persistence strategy to compare data consistency options

Any future addition should preserve the purpose of the repository: making architectural trade-offs visible without turning the sample into a full production platform.

## Practical rule

A feature should remain out of scope unless it helps answer one of these questions:

- How does state ownership differ between the two implementations?
- How does workflow coordination differ between the two implementations?
- How does each implementation handle concurrency, idempotency, and failure?
- How does each implementation change deployment, scaling, observability, or evolution?

If a feature does not help answer those questions, it should stay out of the core comparison.
