# UI dashboard

`Workbench.Ui` is a small, developer-facing Blazor application for exploring the architecture workbench. It is not a product UI, ecommerce frontend, or production operations portal.

The UI makes the comparison understandable without requiring readers to inspect every source file. It provides four focused pages:

- the **Scenario runner** executes the same workflow through both implementations and compares normalized results
- the **Health page** combines live health reports with the shared topology model
- the **Topology page** explains the intended architecture in text
- the **Trade-offs page** summarizes the main architectural differences

[View the UI dashboard screenshot](images/ui-dashboard.png)

## UI responsibilities

The UI has five main responsibilities:

- run deterministic scenarios through both implementations
- present normalized results side by side
- display request-submission metrics consistently across single-order, concurrent, and duplicate-request scenarios
- present live health, dependency health, and resource availability
- explain the architecture and its trade-offs

The UI prioritizes clarity, scenario visibility, and architectural explanation over product-style interaction design.

## Scenario runner

The Scenario runner is the root page of the Workbench UI. It lets the user select a scenario, review its defaults, optionally adjust advanced inputs, start the comparison, and inspect the resulting outcomes.

The same scenario request is executed through:

- the microservices implementation
- the virtual actor implementation

There is no architecture-selection control. The purpose of the page is to compare both implementations through the same scenario semantics rather than run one implementation as an isolated product experience.

### Scenario inputs

The form exposes the scenario and the inputs needed by the workbench, including:

- initial stock
- requested quantity
- concurrent request count where applicable
- product identifier
- customer identifier
- idempotency key

Scenario-specific defaults keep the demonstrations deterministic. Fields that do not apply to the selected scenario are disabled or ignored when the request is created.

The advanced settings remain optional so the default path is easy to run while still allowing users to explore different stock, quantity, concurrency, and identity values.

### Execution feedback

The page displays progress feedback while a scenario is running. This is especially useful for concurrent, duplicate-request, and timeout scenarios where the final result represents more than one submission or includes deliberate compensation behavior.

The progress steps are presentation-only. They are not backend workflow events, server-pushed status updates, or distributed trace evidence.

### Result comparison

After a run completes, the page displays one result card for each implementation. Both cards use the shared `ScenarioExecutionResult` contract so the comparison focuses on observable business semantics rather than implementation-specific response shapes.

The result cards show:

- total request submissions
- unique successful orders
- rejected submissions
- idempotent duplicate responses
- remaining inventory
- elapsed time
- terminal reason where applicable
- an explanatory timeline

The timelines explain the intended workflow at a high level. Detailed runtime evidence belongs in logs and distributed traces, which are available through the Aspire dashboard.

## Interpreting scenario results

### Concurrent orders

The concurrent-orders scenario expects both implementations to prevent over-reservation.

- The microservices implementation protects inventory through `Inventory.Api` and its persistence boundary
- The virtual actor implementation coordinates reservations through `InventoryItemGrain(productId)` and its identity boundary

The invariant is the same in both implementations:

> Inventory must not be over-reserved.

The useful comparison is where ownership and concurrency protection are expressed, not whether one architecture supports correctness and the other does not.

### Hot product contention

The hot-product-contention scenario concentrates concurrent requests on one product.

- In the microservices implementation, `Inventory.Api` and its backing store form the contention boundary
- In the virtual actor implementation, `InventoryItemGrain(productId)` forms the contention boundary for that product identity

Both implementations are expected to preserve the inventory invariant. The scenario demonstrates that adding service instances or silos does not automatically remove contention around one hot key or identity.

### Duplicate request

The duplicate-request scenario submits the same logical order concurrently by reusing the same order identity and idempotency key.

Expected behavior is:

- total request submissions equals the configured concurrent request count
- one unique logical order completes when the request can succeed
- later duplicate submissions return the established result
- inventory is reserved at most once
- idempotent duplicate responses equal the remaining successful replays

The scenario validates observable idempotency behavior. It does not prescribe one universal production implementation for distributed idempotency.

### Payment timeout after reservation

The payment-timeout scenario reserves inventory first and then models payment authorization timing out.

The sample treats the timeout as a failed authorization, releases the reservation, and rejects the order with reason `PaymentTimeout`.

A production workflow could instead enter a pending state and reconcile the payment outcome later because a timeout is ambiguous. The UI presents the deterministic policy used by this workbench without implying that it is the only valid production policy.

### Aggregate result wording

Concurrent scenarios describe the full batch of submissions.

A partially fulfilled result means that some submissions completed while others were rejected after available inventory was exhausted. The timeline therefore summarizes the aggregate run rather than presenting one successful order as if it represented every submission.

## Result terminology

The UI uses request-submission terminology deliberately:

- **Total request submissions** counts attempts sent to the backend
- **Unique successful orders** counts distinct logical orders that completed
- **Rejected submissions** counts logical submissions that were rejected
- **Idempotent duplicate responses** counts repeated submissions that returned an existing logical result
- **Remaining inventory** is the final observed inventory quantity
- **Elapsed time** is local workbench feedback for the implementation path

A request submission and a unique logical order are not always the same thing. The duplicate-request scenario makes this distinction visible.

## Health page

The Health page presents the current operational state of the composed application. It combines live readiness and liveness observations with the shared topology definition so resources can be interpreted in their architectural context.

The page presents:

- overall service availability
- aggregate resource health
- resource and dependency issue counts
- architecture and service groups
- individual nodes
- required and optional dependencies
- current resource availability
- unknown or unmonitored dependency state
- the time of the latest snapshot

### Health model

The page distinguishes related but different concepts:

- **Liveness** indicates whether a process is running
- **Readiness** indicates whether a resource is ready to accept work, including relevant dependency or persistence checks
- **Availability** represents whether a service endpoint can currently be reached
- **Evaluated health** combines direct observations with dependency and group rules from the shared topology model

These values are operational signals, not proof of business correctness. A service can be ready while still producing an incorrect scenario result, violating an idempotency rule, or handling compensation incorrectly.

### Topology-aware health

The Health page is where the shared topology definitions are applied to live observations.

It organizes the snapshot into:

- groups representing architecture or resource areas
- nodes representing services, storage, or other resources
- edges representing dependency relationships
- aggregate node health that includes relevant dependency health
- group health evaluated from member nodes

Missing definitions, missing snapshots, unavailable resources, and unknown health remain visible instead of being silently omitted. This makes configuration gaps and unobserved dependencies distinguishable from confirmed operational failures.

### Refresh behavior

The page loads a snapshot when initialized and allows the user to request a new snapshot. While a refresh is running, the page exposes a busy state and prevents overlapping refreshes.

If a refresh fails after a previous snapshot was loaded, the last available snapshot remains visible with an explanatory warning. If no snapshot is available, the page presents an appropriate loading, error, or empty state.

## Topology page

The Topology page is a text-based explanation of the intended architecture.

It describes:

- the Workbench request path
- the microservices workflow and ownership boundaries
- the virtual actor workflow and identity boundaries
- the role of the gateway, APIs, silo, grains, and persistence
- how the two implementations relate to the comparison

The page does not display live resource state, dependency health, or current availability. Those concerns belong on the Health page.

Keeping the Topology page explanatory and the Health page operational avoids conflating the intended architecture with its current runtime condition.

## Trade-offs page

The Trade-offs page provides a concise in-product summary of the main differences between microservices and virtual actors.

It helps users connect scenario observations to broader concerns such as:

- state ownership
- concurrency boundaries
- workflow coordination
- idempotency
- compensation
- scaling and contention
- deployment and operations
- maintenance and evolution

The page is an entry point, not the complete architecture guide. Detailed reasoning remains in the documents under `docs`.

## Aspire dashboard and Workbench UI

The Aspire dashboard and `Workbench.Ui` are complementary.

Use `Workbench.Ui` to understand:

- normalized scenario outcomes
- business invariants
- resource and dependency health interpreted through the topology model
- the intended architecture
- the comparison trade-offs

Use the Aspire dashboard to inspect lower-level development diagnostics that are intentionally not reproduced in the Workbench UI:

- resource state and lifecycle
- service endpoints
- runtime dependencies
- structured logs
- distributed traces
- metrics
- runtime configuration and environment details

The Workbench timeline explains the scenario. The Aspire trace shows what actually happened across the composed runtime.

## Interpreting elapsed time

Elapsed time in the UI is useful for local feedback, but it is not a benchmark.

The microservices implementation crosses explicit HTTP service boundaries. The virtual actor implementation routes more coordination through Orleans grain calls and the silo runtime. These topology differences can influence local observations, but they do not establish a general performance result.

Production performance depends on workload distribution, persistence, networking, placement, hot identities, deployment topology, resource limits, runtime configuration, and operational tuning.

## Practical takeaway

The UI is part of the architecture workbench, not just a convenience frontend.

It should help the user answer:

- Did both implementations execute the same scenario?
- Did both preserve the same business invariants?
- How many submissions and unique logical outcomes were produced?
- Were duplicate requests handled idempotently?
- Was compensation applied when the sample policy required it?
- Are the composed resources reachable, ready, and healthy?
- Which dependencies influence the observed health?
- What is the intended architecture, independent of current runtime state?
- Where can detailed logs, traces, and metrics be inspected?

The UI keeps the comparison understandable while the Aspire dashboard provides the deeper runtime evidence needed for diagnosis.

## Related documentation

- [Problem](01-problem.md)
- [Microservices design](02-microservices-design.md)
- [Virtual actors design](03-virtual-actors-design.md)
- [Trade-offs](07-tradeoffs.md)
- [Local validation](09-local-validation.md)
- [End-to-end validation](11-end-to-end-validation.md)
- [Scenario guide](12-scenario-guide.md)
- [Correlation ID logging](13-correlation-id-logging.md)
- [Observability and operations](16-observability-and-operations.md)
- [Known limitations](17-known-limitations.md)
