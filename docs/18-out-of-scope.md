# Out of scope

This repository intentionally keeps the domain and infrastructure focused.

Its purpose is to compare how the same stateful distributed workflow can be implemented with:

- service and business-capability boundaries
- stateful identity and virtual actor boundaries

The repository is an architecture workbench. It is not intended to become a complete commerce platform, a production platform template, an operational control plane, or a benchmark suite.

## Why the scope is limited

The modeled workflow is deliberately narrow:

- place an order
- reserve inventory
- authorize payment
- complete or reject the order
- compensate selected failures
- exercise concurrency, contention, and idempotency behavior

Keeping the workflow small makes ownership, coordination, consistency, failure policy, and operational differences easier to see. Adding unrelated production features would increase implementation surface without necessarily improving the architectural comparison.

An addition belongs in the core workbench only when it helps explain a specific comparison question.

## Complete commerce functionality

The repository does not model a complete commerce domain.

Out-of-scope product capabilities include:

- customer-facing storefronts
- shopping carts
- product catalog administration
- pricing, promotions, and discounts
- taxes
- shipping and fulfillment
- refunds and returns
- notifications
- customer account management
- order-history and support experiences

These capabilities matter in real systems, but they are not required to compare state ownership, workflow coordination, concurrency, compensation, idempotency, and contention.

`Workbench.Ui` is a developer-facing comparison experience. It is not a production commerce frontend or administration portal.

## Production security and identity

The repository does not provide a production security model.

Out-of-scope concerns include:

- user sign-in
- API authentication and authorization
- OAuth 2.0 or OpenID Connect integration
- role-based or attribute-based access control
- tenant isolation
- production secrets management
- key rotation
- certificate lifecycle management
- network-security policy
- security monitoring and incident response
- regulatory or compliance controls

The absence of these features is a deliberate sample boundary, not a recommendation for production systems.

## Messaging and event-driven architecture

The current comparison focuses on direct workflow coordination:

- HTTP calls between services in the microservices implementation
- grain calls between stateful identities in the virtual actor implementation

Out-of-scope messaging and eventing concerns include:

- message brokers
- asynchronous command processing
- event sourcing
- event replay
- event-driven projections
- transactional outbox and inbox patterns
- dead-letter processing
- saga frameworks
- long-running durable workflow engines
- delivery guarantees and message ordering

Messaging could become a separate comparison dimension, but adding it would change the workflow and operational model substantially. It should not be introduced merely to make the sample appear more production-like.

## Production hosting and platform engineering

The .NET Aspire AppHost is the supported development composition for this repository. It provides local resource orchestration, service discovery, health integration, and access to development diagnostics.

It is not presented as a complete production deployment platform.

Out-of-scope production platform concerns include:

- cloud hosting architecture
- Kubernetes or other orchestrator configuration
- Helm charts
- ingress and external load-balancing policy
- service mesh configuration
- production DNS and certificate management
- infrastructure as code
- autoscaling policies
- capacity-management automation
- zero-downtime deployment implementation
- multi-region routing and failover
- production environment promotion

The real-world deployment considerations are discussed in [Deployment comparison](05-deployment-comparison.md), but the repository does not implement those production mechanisms.

## Production data management

The repository uses persistence to demonstrate ownership, concurrency, compensation, idempotency, and state evolution. It does not provide a complete production data-management strategy.

Out-of-scope concerns include:

- production database selection and sizing
- high-availability database topology
- backup and restore automation
- point-in-time recovery
- retention and archival policy
- data classification and governance
- cross-service schema governance
- multi-region replication
- production disaster recovery
- online migration orchestration
- reconciliation of every ambiguous workflow outcome

The release and migration implications are discussed in [Release, versioning, and rollback](14-release-versioning-and-rollback.md), but the sample does not implement a production migration or recovery platform.

## Production Orleans operations

The virtual actor implementation demonstrates identity-based state ownership and workflow coordination through Orleans.

Out-of-scope production Orleans concerns include:

- production cluster-membership strategy
- cloud or production persistence providers
- multi-silo capacity tuning
- custom placement strategies
- multi-cluster or multi-region topology
- production reminder and streaming infrastructure
- advanced grain-versioning rollout
- placement and activation optimization at scale
- automated hot-grain mitigation
- production Orleans dashboarding and alerting

These concerns are important in real Orleans systems. They are not required to demonstrate the difference between service-owned state and identity-owned state in this workbench.

## Production observability and operations

The repository includes development observability through:

- shared OpenTelemetry configuration
- structured logging
- distributed traces
- scenario metrics
- correlation and trace context
- readiness and liveness endpoints
- shared health and topology models
- the Aspire dashboard
- the Workbench Health page

It does not provide a complete production observability and operating model.

Out-of-scope concerns include:

- production telemetry storage and retention
- enterprise log aggregation
- production dashboards and alerting
- service-level indicators and objectives
- paging and escalation policy
- on-call ownership
- telemetry access control and tenant isolation
- sensitive-data governance and redaction policy
- telemetry cost management
- cross-region telemetry collection
- audit and compliance requirements
- automated remediation

The Aspire dashboard is a development diagnostics surface. The Workbench Health page is an application-specific interpretation of current health. Neither is a production monitoring, alerting, or incident-management platform.

See [Observability and operations](16-observability-and-operations.md) for the implemented model and its production considerations.

## Production resilience and recovery

The scenarios demonstrate selected failure policies, including inventory rejection, payment failure, timeout compensation, concurrency, and duplicate requests.

The repository does not implement a complete resilience or recovery strategy.

Out-of-scope concerns include:

- durable reconciliation services
- generalized retry and timeout policy
- circuit-breaker tuning for production workloads
- recovery of every interrupted workflow
- operator-driven repair tools
- poison-message handling
- business-continuity planning
- disaster recovery exercises
- regional failover
- guaranteed exactly-once processing

The deterministic scenario policies are comparison tools, not production recovery recommendations.

## Performance benchmarking

This repository is not a benchmark suite.

Elapsed time shown in the Workbench UI is local feedback for one run in the current development topology. It can help explain the sample, but it must not be interpreted as a general performance, throughput, latency, cost, or scalability result.

A credible benchmark would require controlled decisions for:

- representative workloads
- warmup and repetition
- infrastructure and resource limits
- persistence configuration
- network topology
- latency distributions
- throughput and error rates
- workload skew and hot identities
- scaling levels
- statistical analysis
- reproducibility

The repository does not attempt that study.

## Workbench productization

The Workbench exists to make the comparison understandable.

Out-of-scope productization concerns include:

- production authentication and authorization
- multi-user administration
- audit trails
- localization
- accessibility certification
- browser-support guarantees
- production session and circuit recovery
- user preference persistence
- production caching
- content-security hardening
- operational write actions from the Health or Topology pages

The Topology page explains the intended architecture in text. The Health page presents topology-aware runtime observations. Neither page is an infrastructure-management console.

## Possible future comparisons

An out-of-scope subject can become a useful future extension when it introduces a clear comparison question.

Examples include:

- synchronous versus asynchronous workflow coordination
- service-owned workflows versus durable workflow engines
- actor-state persistence providers
- state repartitioning for hot identities
- production-like mixed-version deployment
- pending and reconciled payment-timeout policy
- multi-region consistency choices
- event-driven projections

A future comparison should define:

- the architectural question
- the shared business behavior
- the invariants being compared
- the operational evidence required
- the limits of the conclusion

It should not be added only to increase feature count or infrastructure complexity.

## Practical scope rule

A proposed feature belongs in the core repository only when it materially helps answer at least one of these questions:

- How does state ownership differ between the two implementations?
- How does workflow coordination differ?
- How are concurrency, contention, idempotency, and compensation expressed?
- How do deployment, scaling, observability, release, and maintenance responsibilities differ?
- What externally visible scenario behavior must remain equivalent?

If a feature does not improve one of those comparisons, it should remain outside the core workbench.

## Related documentation

- [Problem](01-problem.md)
- [Deployment comparison](05-deployment-comparison.md)
- [Scaling comparison](06-scaling-comparison.md)
- [Trade-offs](07-tradeoffs.md)
- [Organizational scaling and architecture fit](08-organizational-scaling-and-architecture-fit.md)
- [UI dashboard](10-ui-dashboard.md)
- [Scenario guide](12-scenario-guide.md)
- [Release, versioning, and rollback](14-release-versioning-and-rollback.md)
- [Observability and operations](16-observability-and-operations.md)
- [Known limitations](17-known-limitations.md)
