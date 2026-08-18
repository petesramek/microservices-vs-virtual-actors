# Observability and operations

Observability should help operators understand what a distributed system is doing, why it produced a particular outcome, and where responsibility for that outcome belongs.

Microservices and virtual actor systems need the same fundamental signals:

- logs that explain discrete events and decisions
- traces that connect causally related operations
- metrics that summarize behavior over time
- health signals that describe current operational state
- domain-aware validation that confirms business invariants

The architectures differ in where those signals originate and how operators interpret them. Microservices emphasize service, network, and persistence boundaries. Virtual actors emphasize runtime, identity, activation, placement, and actor-state boundaries.

This document discusses the broader real-world operational model and then explains how the repository illustrates it through .NET Aspire, OpenTelemetry, shared health and topology models, and the Workbench UI.

## Operational goals

A useful observability model should help answer several categories of questions.

### Request and workflow questions

- Which external action started the workflow?
- Which services, actors, stores, and dependencies participated?
- Which operation failed or became slow?
- Was the workflow completed, rejected, compensated, left pending, or abandoned?
- Did retries or duplicate delivery change the logical result?

### State and correctness questions

- Which component owned the affected state?
- Which invariant was being protected?
- Was inventory reserved or released exactly as intended?
- Did duplicate requests create more than one logical result?
- Did persisted state remain compatible after restart or deployment?

### Runtime and capacity questions

- Which service, actor identity, partition, database, or dependency is the bottleneck?
- Is load evenly distributed?
- Are one or more identities disproportionately hot?
- Are retries, timeouts, or queue growth amplifying load?
- Is capacity being added at the actual constraint?

### Release and recovery questions

- Did behavior change after a deployment?
- Are old and new versions interoperating safely?
- Can incomplete work resume after restart?
- Is rollback compatible with current durable state?
- Is reconciliation required for ambiguous outcomes?

No single signal answers all of these questions. Observability requires correlation across technical telemetry and business semantics.

## Signals and their responsibilities

### Logs

Logs record discrete events, decisions, and exceptions. Structured logs should use stable message templates and property names so events can be queried across services and runtime components.

Useful properties can include:

- service or component name
- operation name
- scenario or workflow kind
- normalized outcome
- correlation, trace, and span identifiers
- bounded reason or status values
- retry or compensation outcome

Logs should not become an uncontrolled copy of business data. Avoid recording credentials, tokens, connection strings, complete payloads, personal data, idempotency keys, or persisted state unless an explicit data-handling policy requires and protects those values.

### Distributed traces

Traces connect related operations and show causal relationships and latency across process and runtime boundaries.

A useful trace can reveal:

- the entry request
- gateway or API processing
- outgoing HTTP calls
- actor-runtime calls
- persistence operations
- retries and timeouts
- compensation
- terminal status

Trace context should propagate through every supported transport. W3C trace context and .NET `Activity` provide the foundation, while OpenTelemetry instrumentation collects and exports the resulting activity data.

A trace explains one execution. It does not replace aggregate metrics or business invariant tests.

### Metrics

Metrics summarize behavior over time and should use bounded dimensions.

Useful metric categories include:

- request and workflow duration
- completed, rejected, failed, and canceled operations
- payment timeout and compensation counts
- duplicate submissions and idempotent responses
- dependency latency and error rates
- resource saturation
- actor activation and hot-identity indicators
- health-state transitions

Do not use customer, product, order, actor, or idempotency identifiers as metric dimensions. High-cardinality investigation belongs in governed logs and traces.

### Health

Health checks answer whether a component is alive, ready, reachable, or dependent on a failing resource. They do not prove that business behavior is correct.

A useful model distinguishes:

- **Liveness**, which indicates whether a process is running and should generally avoid transient downstream checks
- **Readiness**, which indicates whether a resource can accept work and can include required dependency or persistence checks
- **Availability**, which indicates whether a configured endpoint or resource can currently be reached
- **Evaluated health**, which combines direct observations with dependency and grouping rules

Health should be actionable. A status without an explanation, affected dependency, timestamp, or ownership context is difficult to use during diagnosis.

## Correlation and context propagation

Correlation connects logs, traces, metrics, and workflow results without turning diagnostic identifiers into domain state.

The primary causal context should come from distributed tracing. A human-readable correlation value can complement trace context when it improves log search or support workflows.

Diagnostic context should remain outside business request and result contracts unless a business requirement explicitly makes it part of the domain.

See [Correlation and trace context](13-correlation-id-logging.md) for the repository's implementation and validation guidance.

## Microservices operations

Microservices expose operational boundaries directly through independently running services and their dependencies.

### Useful signals

Operators commonly need to correlate:

- ingress or gateway requests
- downstream HTTP or messaging calls
- service-owned database operations
- retry and timeout behavior
- version and deployment state
- readiness and dependency health
- compensation and reconciliation workflows

### Common operational risks

- long synchronous dependency chains
- retry amplification
- version skew
- partial deployment
- shared database or infrastructure bottlenecks
- contract mismatch
- distributed idempotency races
- compensation failure
- unclear incident ownership

### Diagnostic advantage

Failures often appear at explicit service, network, or persistence boundaries. This can make ownership clear when service responsibilities and telemetry are well designed.

### Diagnostic cost

One business workflow can produce signals across many services, stores, and platforms. Without propagated context and consistent terminology, operators must reconstruct the workflow manually.

## Virtual actor operations

Virtual actor systems expose a different operational model. Actor identity and runtime behavior become central diagnostic dimensions.

### Useful signals

Operators commonly need visibility into:

- API-to-cluster connectivity
- cluster membership and silo lifecycle
- actor activation, placement, and deactivation
- actor-call latency and failure
- hot identities and skewed placement
- persistence reads and writes
- reminders, timers, and reactivation
- actor-state compatibility
- reentrancy and request-scheduling behavior

### Common operational risks

- hot actors
- uneven placement
- activation churn
- persistence saturation
- runtime-version incompatibility
- actor-state migration failure
- long call chains across identities
- runtime behavior that is poorly understood by operators
- unclear ownership of actor families

### Diagnostic advantage

Stable actor identity can provide a natural way to reason about state ownership and identity-local workflows.

### Diagnostic cost

Operators need runtime-specific knowledge. Fewer explicit HTTP service boundaries do not remove distributed execution, persistence, placement, compatibility, or failure concerns.

## Scenario-aware operations

Technical telemetry becomes more useful when it can be interpreted through stable business semantics.

For an order workflow, useful bounded dimensions include:

- scenario kind
- implementation or service-client name
- normalized status
- normalized reason
- submission, completion, rejection, and idempotent-response counts
- duration

Identifiers such as order, product, customer, and idempotency key can be useful for targeted investigation, but they should not be metric dimensions and should be logged only under an explicit privacy and retention policy.

### Successful order

Expected evidence includes one reservation, one payment authorization, one completed logical order, and the expected inventory reduction.

Unexpected evidence includes repeated reservation, payment before reservation, completed status with unchanged inventory, or mismatched terminal state.

### Insufficient inventory

Expected evidence includes reservation rejection, no successful payment authorization, unchanged inventory, and reason `InsufficientInventory`.

### Payment failure compensation

Expected evidence includes reservation, payment failure, release, rejection, and restored inventory.

Missing or failed compensation is more important operationally than the original payment failure because it leaves business state inconsistent.

### Payment timeout after reservation

Expected evidence includes reservation, modeled timeout, release, rejection, and reason `PaymentTimeout`.

A real production system may instead record a pending state and reconcile later. Observability must distinguish timeout, known failure, pending confirmation, and reconciliation outcome.

### Concurrent orders

Expected evidence includes bounded completion by stock capacity, explicit rejection after stock is exhausted, and non-negative remaining inventory.

### Hot product contention

Expected evidence includes concentrated latency or work around one product state owner. Operators should distinguish healthy serialization from overload, starvation, or excessive queueing.

### Duplicate request

Expected evidence includes many submissions, one logical result, one inventory reservation, and idempotent replay for later submissions.

Unique constraint failures, repeated downstream work, or several unique completed orders indicate an idempotency defect.

## Alerts and operational policies

Production alerting should focus on actionable symptoms and violated objectives rather than raw infrastructure noise.

Potential alerts include:

- elevated error or timeout rate
- sustained workflow latency
- unavailable required dependencies
- repeated compensation failure
- negative inventory or impossible business counts
- duplicate requests creating several logical results
- persistence saturation
- unusual actor activation churn
- hot-identity backlog
- missing telemetry from a required component
- failed migration or incompatible state activation

An alert should identify ownership, expected impact, relevant telemetry, and the next diagnostic step. Alert thresholds should come from service objectives and workload behavior rather than arbitrary sample values.

## Incident investigation

A practical investigation sequence is:

1. Confirm the user-visible or business symptom.
2. Identify the affected workflow, scenario, service, or actor family.
3. Locate the trace or correlation context.
4. Inspect the entry request and dependency chain.
5. Identify the state owner and relevant invariant.
6. Compare direct resource health with dependency health.
7. Check persistence, retries, timeouts, compensation, and duplicate behavior.
8. Compare the outcome with the expected business semantics.
9. Determine whether the issue is code, configuration, capacity, dependency, state, or compatibility related.
10. Record the recovery and reconciliation actions required.

The most important question is often not which component logged an exception, but which component owned the state transition that produced the incorrect or incomplete outcome.

## Repository implementation

The repository demonstrates these concerns through shared hosting defaults, reusable observability models, scenario instrumentation, the Aspire dashboard, and Workbench UI pages.

### Shared service defaults

`Hosting.ServiceDefaults` configures common development behavior for:

- service discovery
- HTTP resilience
- readiness and liveness endpoints
- structured logging integration
- OpenTelemetry tracing and metrics
- OTLP export
- scenario instrumentation and trace sampling

Keeping these defaults shared reduces configuration drift while still allowing each project to add service-specific health checks, logs, activities, and metrics.

### Scenario instrumentation

The Workbench records scenario activities and metrics using stable, bounded values such as scenario kind and service-client name.

Custom trace collection and sampling can prioritize Workbench scenario traffic. Sampling controls telemetry collection rather than business execution.

Instrumentation names, event identifiers, property names, activity tags, and metric dimensions should remain stable because diagnostic queries and documentation can depend on them.

### Health and topology models

`Observability.Health` provides reusable health-report models and status evaluation.

`Observability.Topology` provides:

- topology definitions
- nodes, groups, and dependency edges
- required and optional dependency semantics
- topology validation
- availability and health snapshots
- dependency and group evaluation

The AppHost defines the development resource topology and health groups. `Workbench.Ui` combines current observations with that shared model.

### Aspire dashboard

The Aspire dashboard is the detailed development diagnostics surface.

Use it to inspect:

- application resources and lifecycle
- service endpoints and dependencies
- structured logs
- distributed traces
- metrics
- development configuration exposed by the composed environment

The dashboard provides information that is intentionally not reproduced in the Workbench UI.

### Workbench Health page

The Health page provides application-specific interpretation of live observations.

It presents:

- aggregate availability and health
- architecture and service groups
- nodes and dependencies
- required and optional dependency health
- readiness and liveness information
- current resource availability
- unknown and missing observations
- snapshot refresh and stale-snapshot behavior

The Health page is topology-aware, but it is not a production monitoring or alerting platform.

### Workbench Topology page

The Topology page is a text-based explanation of the intended architecture. It describes the Workbench, microservices, virtual actor runtime, grain identities, and persistence relationships.

It does not present live availability or health. Runtime topology-aware observations belong on the Health page.

### Complementary diagnostics

The two dashboards solve different problems:

- `Workbench.Ui` presents normalized scenario results, application-specific health interpretation, architecture explanation, and trade-offs
- The Aspire dashboard presents lower-level resource, log, trace, metric, dependency, endpoint, and lifecycle diagnostics

The Workbench timeline explains the intended scenario. The Aspire trace shows the operations that actually occurred.

## Local validation

To validate observability locally:

1. Start the repository through `Hosting.AppHost`.
2. Open the Workbench UI from the Aspire dashboard.
3. Run each supported scenario.
4. Confirm that both implementation results preserve the expected business semantics.
5. Inspect relevant logs for the Gateway and backend resources.
6. Open the related distributed trace and confirm causal continuity.
7. Confirm that scenario metrics update with bounded dimensions.
8. Open the Health page and review groups, nodes, dependencies, availability, and evaluated health.
9. Stop or restart a safe resource and confirm that Aspire and the Health page expose the changed state.
10. Restore the resource and confirm that readiness and health recover.

The Microservices and Virtual Actors paths do not need identical traces. Validation should focus on useful context, correct status, safe telemetry, and business-semantic continuity.

See [Local validation](09-local-validation.md) and [End-to-end validation](11-end-to-end-validation.md) for the complete repository checklist.

## Production considerations

The repository provides a development observability implementation, not a production telemetry platform.

A production design still needs explicit decisions for:

- telemetry storage and retention
- dashboards and alerting
- service-level indicators and objectives
- access control and tenant isolation
- sensitive-data classification and redaction
- sampling and telemetry cost
- cross-region collection
- audit requirements
- on-call ownership and escalation
- incident response
- reconciliation and recovery automation

The Aspire dashboard is a development instrument and diagnostic view. Production systems require an independently designed observability backend and operational model.

## Operational checklist

When a scenario or workflow result looks wrong:

1. Confirm the expected business outcome in the Scenario guide.
2. Identify the trace or correlation context.
3. Inspect the Gateway and relevant backend logs.
4. Follow the distributed trace across service or actor-runtime boundaries.
5. Identify the state owner for the affected invariant.
6. Verify submission, completion, rejection, idempotency, and inventory counts.
7. Compare direct health, dependency health, and resource availability.
8. Inspect retries, timeouts, persistence, and compensation.
9. Determine whether the behavior changed intentionally.
10. Update tests and documentation when semantics change deliberately.

## Key takeaways

- Logs, traces, metrics, health, and business validation answer different operational questions
- Diagnostic context must propagate across both service and actor-runtime boundaries
- Health indicates operational state, not business correctness
- Microservices emphasize service, network, persistence, and integration diagnostics
- Virtual actors emphasize identity, runtime, placement, activation, and actor-state diagnostics
- High-cardinality identifiers do not belong in metric dimensions
- The Aspire dashboard provides detailed development diagnostics
- The Workbench Health page provides topology-aware application interpretation
- The Workbench Topology page explains the intended architecture without presenting live state
- Production observability requires decisions beyond the sample's local development tooling

## Related documentation

- [Microservices design](02-microservices-design.md)
- [Virtual actors design](03-virtual-actors-design.md)
- [Deployment comparison](05-deployment-comparison.md)
- [Scaling comparison](06-scaling-comparison.md)
- [Local validation](09-local-validation.md)
- [UI dashboard](10-ui-dashboard.md)
- [End-to-end validation](11-end-to-end-validation.md)
- [Scenario guide](12-scenario-guide.md)
- [Correlation and trace context](13-correlation-id-logging.md)
- [Release, versioning, and rollback](14-release-versioning-and-rollback.md)
- [Known limitations](17-known-limitations.md)
- [Out of scope](18-out-of-scope.md)
