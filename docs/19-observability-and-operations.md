# Observability and operations

This document explains how to operate, diagnose, and reason about the comparison sample at runtime.

The goal is to make scenario behavior traceable across the UI, gateway, microservices-style backend, and virtual actor-style backend without mixing diagnostic metadata into business contracts.

## Summary

The sample uses lightweight header-based correlation:

- the UI generates a correlation ID for a scenario run
- the UI sends the value using the `X-Correlation-ID` HTTP header
- the gateway stores the value in an asynchronous correlation context
- the gateway forwards the value to backend HTTP calls
- backend services add the value to structured logging scopes
- the UI displays the correlation ID next to the completed run summary

This is intentionally simpler than full distributed tracing. It is useful for local diagnostics and for explaining how correlation works. A production system would commonly use W3C Trace Context, `Activity`, and OpenTelemetry.

## Diagnostic goals

A scenario run should answer these operational questions:

- Which UI action triggered this run?
- Which gateway request handled this run?
- Which backend calls belong to the same run?
- Which architecture path was executed?
- Which product, order, and idempotency key were involved?
- Which state owner accepted, rejected, reserved, released, or completed work?
- Did completed/rejected/idempotent counts match the expected scenario behavior?
- Did the final inventory state preserve the scenario invariant?

## Correlation ID

The correlation ID is diagnostic metadata. It is intentionally not part of `RunScenarioRequest`, `RunScenarioResponse`, or `ArchitectureRunResult`.

The header name is:

```text
X-Correlation-ID
```

Example value:

```text
run-9f2f4a0f1c17482a8a0cc0c45c6d9a7e
```

Keeping correlation in headers avoids mixing observability concerns into business contracts.

## How to trace one scenario run

1. Run a scenario from the UI.
2. Copy the correlation ID shown in the completed run summary.
3. Search logs from the UI, gateway, and backend services for that exact value.
4. Compare the timeline in the UI with the service logs.
5. Check that final result metrics match the expected scenario behavior.

For the microservices path, look across:

- `Comparison.Gateway`
- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`

For the virtual actor path, look across:

- `Comparison.Gateway`
- `Ordering.Api`
- grain workflow logs, if enabled

## Important log dimensions

The most useful diagnostic dimensions are:

- `CorrelationId`
- `Scenario`
- `Architecture`
- `ProductId`
- `OrderId`
- `IdempotencyKey`
- `TotalRequestSubmissions`
- `CompletedOrders`
- `RejectedOrders`
- `IdempotentResponses`
- `RemainingInventory`
- `ElapsedMilliseconds`
- `Reason`

Not every component logs all of these today. The correlation ID is the minimum common dimension that links the run together.

## Scenario-specific operational notes

### Successful order

Expected operational shape:

- one request submission
- one inventory reservation
- one payment authorization
- one completed order
- inventory reduced by quantity

Unexpected signs:

- payment called before inventory reservation
- multiple reservations for one request
- completed order with unchanged inventory
- missing correlation ID in backend logs

### Insufficient inventory

Expected operational shape:

- one request submission
- inventory rejects reservation
- payment is not attempted
- inventory remains unchanged

Unexpected signs:

- payment authorization appears in logs
- inventory changes despite rejection
- reason is not `InsufficientInventory`

### Payment failure compensation

Expected operational shape:

- inventory is reserved
- payment explicitly fails
- inventory reservation is released
- order is rejected
- final inventory equals initial stock

Unexpected signs:

- missing release after payment failure
- completed order after payment failure
- final inventory lower than initial stock

### Payment timeout after reservation

Expected operational shape:

- inventory is reserved
- payment timeout is reported
- inventory reservation is released
- order is rejected with `PaymentTimeout`
- final inventory equals initial stock

Unexpected signs:

- timeout reported as generic payment failure
- inventory not released
- scenario reason differs from `PaymentTimeout`

The sample treats timeout as failed for determinism. In a production system, timeout may require pending state and reconciliation.

### Concurrent orders

Expected operational shape:

- many independent request submissions
- completed count does not exceed available stock divided by quantity
- rejected submissions appear when demand exceeds stock
- final inventory is not negative

Unexpected signs:

- more completed orders than stock allows
- negative inventory
- all requests completed despite insufficient stock
- result timeline shows only one successful order instead of aggregate behavior

### Hot product contention

Expected operational shape:

- many submissions target the same product
- the product identity is the contention point
- completed and rejected counts reflect available stock
- inventory does not go below zero

Unexpected signs:

- completed count exceeds stock
- missing rejected submissions when demand exceeds stock
- high elapsed time concentrated around the hot product identity

### Duplicate request

Expected operational shape:

- many submissions reuse the same order identity and idempotency key
- one unique logical order completes
- duplicate submissions return idempotent responses
- inventory is reduced once by quantity

Unexpected signs:

- inventory reduced once per duplicate submission
- unique constraint exceptions in `Orders.Api`
- more than one unique successful order
- duplicate responses treated as rejected submissions

## Microservices operations perspective

The microservices-style implementation distributes behavior across service processes.

Operational advantages:

- failures often appear at explicit service boundaries
- each service can expose its own logs and metrics
- service ownership can align with operational ownership
- individual services can be restarted or scaled independently

Operational costs:

- diagnosis requires correlation across multiple services
- network failures and timeouts are part of normal operations
- version skew during deployment can create subtle failures
- dashboards and alerts need to account for service interactions

For this sample, the most important microservices diagnostic path is:

```text
UI -> Comparison.Gateway -> Orders.Api -> Inventory.Api / Payments.Api
```

## Virtual actors operations perspective

The virtual actor-style implementation expresses behavior through actor identities and runtime-managed activations.

Operational advantages:

- per-identity state ownership is easier to reason about
- actor identity is a useful diagnostic dimension
- per-identity serialization can reduce concurrency bugs for stateful invariants
- workflow behavior can be inspected around grain interactions

Operational costs:

- runtime behavior may be less obvious to operators unfamiliar with Orleans
- activation placement and lifecycle matter
- hot grains can become bottlenecks
- grain state storage and serialization require operational discipline

For this sample, useful actor diagnostic dimensions include:

- order grain identity
- inventory item grain identity
- payment account grain identity
- scenario name
- correlation ID

## Metrics to watch

This sample does not implement a full metrics backend, but the result model exposes useful operational signals.

### Correctness metrics

- completed orders must not exceed stock capacity
- remaining inventory must not go below zero
- duplicate submissions must not create multiple unique orders
- compensation scenarios should restore inventory

### Performance indicators

- elapsed milliseconds per architecture
- elapsed milliseconds per scenario
- elapsed time under hot product contention
- elapsed time under duplicate request concurrency

These timings are local-demo indicators, not benchmark proof.

### Reliability indicators

- rejected submissions by scenario
- reason distribution
- timeout count
- idempotent duplicate response count
- error responses from backend calls

## Alerting guidance for a production version

A production version would likely alert on:

- negative inventory
- completed orders exceeding available stock
- missing compensation release after payment failure
- increased payment timeout rate
- high duplicate request rate
- unique constraint failures on idempotency keys
- high latency for hot product identities
- high rejected submission rate outside expected scenarios
- missing correlation IDs in gateway/backend logs

The local sample does not include these alerts. The list documents what would matter operationally.

## Relationship to OpenTelemetry

The sample uses `X-Correlation-ID` because it is easy to understand and visible in local logs.

A production-grade version would usually prefer:

- W3C `traceparent` propagation
- .NET `Activity` and `ActivitySource`
- OpenTelemetry instrumentation
- exported traces to a backend such as Azure Monitor, Jaeger, Zipkin, or another tracing system
- structured logs correlated with trace/span IDs

The current approach is intentionally lightweight. It demonstrates the concept without adding tracing infrastructure to the demo.

## Operational checklist

When a scenario result looks wrong:

1. Copy the UI correlation ID.
2. Search gateway logs for the correlation ID.
3. Identify which architecture path ran.
4. Search backend logs for the same correlation ID.
5. Verify request submission count.
6. Verify completed, rejected, and idempotent response counts.
7. Verify final inventory.
8. Compare the result with `docs/16-scenario-comparison-matrix.md`.
9. If behavior changed intentionally, update regression tests and docs.
10. If behavior changed unintentionally, inspect the state owner for the affected invariant.

## Key takeaways

- Correlation is mandatory once a workflow crosses process boundaries.
- Actor-based workflows still need correlation; fewer HTTP boundaries do not remove operational diagnostics.
- The most important operational questions are about state ownership, invariant protection, and failure policy.
- Logs, metrics, traces, tests, and docs should all use the same scenario terminology.
- The correlation ID belongs in diagnostic context, not business contracts.

