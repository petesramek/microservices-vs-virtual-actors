# End-to-end validation

This checklist validates the repository as a complete architecture workbench rather than as a collection of independent projects.

Use it to confirm that the solution builds, the .NET Aspire application topology starts, both implementations execute the same scenarios, the Workbench UI presents consistent results, and the available health and observability surfaces provide useful diagnostic information.

This document focuses on externally visible behavior. Detailed scenario defaults and expected values remain in the [Scenario guide](12-scenario-guide.md).

## 1. Clean build and tests

Run from the repository root:

```bash
dotnet clean microservices-vs-virtual-actors.slnx
dotnet restore microservices-vs-virtual-actors.slnx
dotnet build microservices-vs-virtual-actors.slnx \
  --configuration Release \
  --no-restore
dotnet test microservices-vs-virtual-actors.slnx \
  --configuration Release \
  --no-build
```

Expected result:

- restore succeeds
- the solution builds in Release configuration
- compiler and analyzer warnings are reviewed rather than hidden by broad suppressions
- all test projects pass

The solution-level test run should include:

- `Microservices.Tests`
- `VirtualActors.Tests`
- `Workbench.AcceptanceTests`
- `Workbench.ScenarioRegressionTests`

## 2. Start the application through Aspire

Start the supported development topology from the repository root:

```bash
dotnet run --project src/Hosting/Hosting.AppHost/Hosting.AppHost.csproj
```

Open the Aspire dashboard URL reported by the AppHost.

Expected result:

- the AppHost starts successfully
- the Aspire dashboard loads
- all configured projects and health-group resources appear
- required dependencies are connected through the AppHost
- service endpoints are available from the resource details
- no resource remains unexpectedly failed or permanently starting

The Aspire AppHost is the supported repository startup path. Individual project launch profiles may still be useful for focused debugging, but they are not the end-to-end validation topology.

## 3. Validate the composed resources

Use the Aspire dashboard to review the complete application composition.

Confirm that the expected resource areas are represented:

- Workbench UI and Gateway
- `Orders.Api`, `Inventory.Api`, and `Payments.Api`
- `Ordering.Api` and `Ordering.Silo`
- configured health groups and dependencies

Expected result:

- `Workbench.Ui` can reach `Workbench.Gateway`
- the Gateway can reach both backend implementations
- `Orders.Api` can reach its required inventory and payment dependencies
- `Ordering.Api` can communicate with the Orleans silo
- service discovery uses AppHost-provided references rather than manually coordinated local ports
- readiness failures are distinguishable from process-liveness failures

## 4. Open the Workbench UI

Open the `Workbench.Ui` endpoint from the Aspire dashboard.

Expected result:

- the application shell and navigation render
- the Scenario runner is available at the root route
- the Health, Topology, and Trade-offs pages are reachable
- the UI becomes interactive through server rendering
- no startup or component-rendering error appears

The UI does not contain an architecture-selection control. Every scenario run compares both implementations through the same request semantics.

## 5. Validate scenario execution

Run each supported scenario from the Scenario runner:

- successful order
- insufficient inventory
- payment failure compensation
- payment timeout after reservation
- concurrent orders
- duplicate request
- hot product contention

Expected result for every run:

- the running state appears while the request is active
- controls prevent overlapping submissions
- both the Microservices and Virtual Actors result cards render after completion
- each card shows status, reason where applicable, counts, remaining inventory, elapsed time, and an explanatory timeline
- the scenario summary and description match the selected scenario
- failures are presented through the error state rather than leaving the page permanently busy

The visual progress steps and explanatory timelines are presentation aids. They are not backend workflow events or substitutes for distributed traces.

## 6. Validate business invariants

Focus on normalized behavior rather than internal implementation details.

### Successful order

Confirm that:

- one logical order completes in each implementation
- inventory decreases by the requested quantity
- no rejection or idempotent duplicate response is reported

### Insufficient inventory

Confirm that:

- the logical order is rejected
- inventory is not overdrawn
- payment is not presented as successfully completed

### Payment failure compensation

Confirm that:

- inventory is reserved before payment is attempted
- the payment outcome is failed
- compensation releases the reservation
- final inventory returns to the expected value

### Payment timeout after reservation

Confirm that:

- inventory is reserved before the modeled timeout
- the sample reports reason `PaymentTimeout`
- the reservation is released
- the result does not imply that the sample policy is the only valid production timeout strategy

### Concurrent orders

Confirm that:

- total submissions match the configured request count
- completed and rejected submissions are reported separately
- completed orders do not exceed available stock
- remaining inventory never becomes negative

### Duplicate request

Confirm that:

- all submissions reuse one logical order identity and idempotency key
- at most one unique logical order completes
- inventory is reserved at most once
- later submissions return the established result as idempotent responses

### Hot product contention

Confirm that:

- concurrent requests target one product identity
- both implementations preserve the inventory invariant
- the result makes contention visible without claiming that either architecture removes the hot-key boundary

See the [Scenario guide](12-scenario-guide.md) for exact defaults, reason values, and expected result shapes.

## 7. Validate result terminology

Confirm that both cards use the shared result terminology consistently:

- **Total request submissions** counts attempts sent to the implementation
- **Unique successful orders** counts distinct logical orders that completed
- **Rejected submissions** counts logical submissions that were rejected
- **Idempotent duplicate responses** counts repeated submissions that returned an established result
- **Remaining inventory** reports the final observed quantity
- **Elapsed time** is local workbench feedback, not benchmark evidence

A duplicate response must not be counted as another unique successful order.

## 8. Validate the Health page

Open the Health page while the application is running.

Expected result:

- the page loads a topology snapshot
- the summary reports service availability, healthy resources, resource issues, and dependency issues
- architecture and service groups render
- nodes show direct or evaluated health
- service nodes show availability
- required and optional dependencies are visible
- unknown or missing observations remain visible rather than being silently omitted
- the snapshot timestamp is shown
- a manual refresh updates the snapshot without overlapping requests

Validate the distinction between:

- liveness
- readiness
- service availability
- evaluated node health
- dependency health
- group health

If practical, stop or restart a resource from the Aspire dashboard and refresh the Health page.

Expected result:

- the affected resource or dependency changes to an appropriate unavailable, unhealthy, degraded, or unknown state
- the page remains usable
- after recovery and refresh, the displayed state returns to the current observation
- a failed refresh can preserve the last available snapshot with an explanatory warning

Health validates operational state, not scenario correctness.

## 9. Validate the Topology page

Open the Topology page.

Expected result:

- the page provides a text-based explanation of the intended architecture
- the Workbench, microservices, and virtual actor paths are described clearly
- service ownership and actor identity boundaries are distinguishable
- `Ordering.Api`, `Ordering.Silo`, grains, and persistence have accurate roles
- the page does not present live availability or runtime health

Live topology-aware observations belong on the Health page. The Topology page explains the static architecture independently of its current runtime condition.

## 10. Validate the Trade-offs page

Open the Trade-offs page.

Expected result:

- the page summarizes the comparison without declaring a universal winner
- state ownership, concurrency, idempotency, compensation, scaling, and operational concerns are represented
- local elapsed time is not presented as benchmark proof
- the page remains a concise in-product entry point while detailed analysis stays in the documentation set

## 11. Validate logs and correlation

Run a scenario and inspect structured logs in the Aspire dashboard.

Expected result:

- logs are available for the Gateway and the backend resources involved in the run
- related requests can be connected through the repository's correlation and trace context
- stable structured properties identify the scenario and implementation where configured
- exceptions retain useful context without exposing credentials, connection strings, request secrets, or personal data

The Workbench UI does not need to reproduce the complete log or correlation experience. Detailed diagnostics belong in the Aspire dashboard.

## 12. Validate distributed traces

Open the trace view in the Aspire dashboard after running a scenario.

Expected result:

- the scenario produces a trace when allowed by the configured collection and sampling policy
- spans connect the Workbench Gateway with the selected backend resources
- the microservices path exposes the relevant HTTP service calls
- the virtual actor path exposes the relevant API, silo, and Orleans activity where instrumented
- activity status and timing make failed or slow operations diagnosable
- trace names and tags use bounded, stable values

The exact span graph can differ between scenarios and implementations. Validate causal continuity and useful context rather than requiring both architectures to produce identical traces.

## 13. Validate metrics

Open the metrics view in the Aspire dashboard after running several scenarios.

Expected result:

- scenario workflow metrics are emitted
- implementation identity uses the registered service-client name
- scenario identity uses stable `ScenarioKind` values
- durations and counters update after successful runs
- metric dimensions remain bounded and do not contain customer, product, order, or idempotency identifiers

Metrics support development diagnosis and comparison. They are not a production monitoring, alerting, or service-level-objective system.

## 14. Validate recovery behavior

Use the Aspire dashboard to restart a backend resource, then repeat an affected scenario after the resource becomes ready.

Expected result:

- temporary unavailability is visible through resource state and health
- scenario failures do not permanently break the Workbench UI
- after the required resources recover, a new scenario can complete
- the Health page reflects the recovered state after refresh
- logs and traces retain enough context to distinguish the failed run from the successful retry

This check validates developer-facing recovery behavior. It is not a test of production failover, reconciliation, or disaster recovery.

## 15. Stop the application

Stop the AppHost or use the Aspire dashboard to stop the composed resources.

Expected result:

- child resources shut down
- no supported repository cleanup script is required
- a later AppHost run can start the development topology again
- local persistence behavior remains consistent with the repository's development configuration

## Validation summary

A successful end-to-end validation means:

- the solution restores, builds, and passes all tests
- the complete development topology starts through the Aspire AppHost
- the Workbench UI loads and all four pages are reachable
- every scenario executes through both implementations
- normalized results preserve the expected business invariants
- duplicate requests remain idempotent
- concurrent requests do not over-reserve inventory
- compensation restores inventory when the sample policy requires it
- the Health page presents live topology-aware operational state
- the Topology page explains the intended architecture without presenting live state
- the Aspire dashboard provides useful logs, traces, metrics, endpoints, and resource status
- temporary resource failures are diagnosable and recoverable in the development environment

The goal is to validate the repository as one coherent architecture workbench with consistent externally visible behavior and useful development diagnostics across both implementations.

## Related documentation

- [Problem](01-problem.md)
- [Microservices design](02-microservices-design.md)
- [Virtual actors design](03-virtual-actors-design.md)
- [Local validation](09-local-validation.md)
- [UI dashboard](10-ui-dashboard.md)
- [Scenario guide](12-scenario-guide.md)
- [Correlation ID logging](13-correlation-id-logging.md)
- [Observability and operations](16-observability-and-operations.md)
- [Known limitations](17-known-limitations.md)
- [Out of scope](18-out-of-scope.md)
