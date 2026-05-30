# UI dashboard

The Blazor Server dashboard is intentionally small. It is not a product UI, ecommerce frontend, or production operations portal.

The dashboard exists to make the architecture comparison visible. It provides one place to run the same scenario against one or both implementations and compare the result shape without reading every source file.

## Dashboard responsibilities

The UI has five main responsibilities:

- run the same scenario against one or both implementations
- show side-by-side results
- make topology, backend readiness, progress feedback, validation, and trade-offs visible
- display request-submission metrics consistently across single-order, aggregate, and duplicate-request scenarios
- show the correlation ID for a completed run so logs can be searched across services

The dashboard is intentionally developer-facing. It prioritizes clarity, scenario visibility, and architectural explanation over product-style user experience.

## Architecture selection

The dashboard can run scenarios against:

- the microservices implementation
- the virtual actor implementation
- both implementations side by side

When both implementations are selected, the dashboard should make differences visible without implying that one result is automatically correct and the other is incorrect. The goal is to compare where each architecture places responsibility for state, concurrency, failure handling, and idempotency.

## Backend readiness

The dashboard should make backend readiness visible before a scenario is run.

This matters because the comparison depends on several local processes or containers being available:

- `Comparison.Gateway`
- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`
- `Ordering.Api`

Readiness indicators help distinguish scenario behavior from local environment problems such as a missing backend process, wrong port, or failed startup.

## Scenario execution feedback

The dashboard should provide progress feedback while a scenario is running.

This is especially useful for concurrent scenarios, duplicate request scenarios, and timeout scenarios where the user needs to understand that the run is still active and that the final result represents a full batch of submissions.

Progress feedback is part of local validation. It is not intended to be a full production workflow monitor.

## Interpreting concurrent orders

The concurrent orders scenario expects both implementations to prevent over-reservation.

- Microservices do this because `Inventory.Api` explicitly owns and protects inventory state.
- Virtual actors do this because `InventoryItemGrain(productId)` serializes operations for a product identity.

The scenario demonstrates different places to enforce the same invariant. It does not demonstrate that one architecture is inherently correct and the other is inherently broken.

The invariant is the same in both implementations:

> Inventory must not be over-reserved.

## Interpreting elapsed time

Elapsed time in the dashboard is useful for local feedback, but it is not a benchmark.

In this sample, the microservice implementation crosses more HTTP service boundaries. The virtual actor implementation keeps more coordination inside the Orleans runtime path.

Local elapsed time can help explain the sample topology, but it should not be interpreted as a universal performance conclusion. Production performance depends on persistence, networking, placement, hot-key distribution, deployment topology, runtime configuration, database behavior, and operational tuning.

## Hot product contention

The hot product contention scenario concentrates concurrent orders on one product.

It is intended to show that both architectures can have a bottleneck around a hot state identity.

- Microservices: `Inventory.Api` or its backing store becomes the contention point.
- Virtual actors: `InventoryItemGrain(productId)` for the product becomes the contention point.

The scenario is expected to prevent over-reservation in both implementations.

The useful comparison is where contention is managed, not whether contention exists.

## Request-submission metrics

Result cards use request-submission metrics consistently across all scenarios:

- total request submissions
- unique successful orders
- rejected submissions
- idempotent duplicate responses
- remaining inventory
- elapsed time

This wording is intentional.

A request submission is an attempt sent to the backend. A unique successful order is a logical order that completed successfully. These are not always the same count.

For duplicate request scenarios, total request submissions can be greater than unique successful orders. This means duplicate submissions returned an existing logical order result and did not reserve inventory again.

## Aggregate scenario result wording

Concurrent scenarios display aggregate request submissions.

Unique successful orders and rejected submissions are separate request groups from the same run. A partially fulfilled result means some submissions completed while other submissions were rejected after inventory was exhausted.

Aggregate timelines describe the full batch instead of showing a single representative successful order.

This avoids implying that a concurrent run has only one meaningful order outcome.

## Duplicate request concurrent count

The duplicate request scenario uses `Concurrent requests` as the number of duplicate request submissions.

Every submission reuses the same order identity and idempotency key.

Expected successful duplicate behavior:

- total request submissions equals concurrent requests
- unique successful orders is `1`
- idempotent duplicate responses is total request submissions minus `1`
- inventory is reduced once by the requested quantity

This scenario validates idempotency. It should not create multiple unique successful orders and should not reserve inventory more than once.

## Microservices duplicate idempotency race

The duplicate request scenario submits the same idempotency key concurrently.

`Orders.Api` serializes `POST /api/orders` requests by idempotency key inside the local sample process so that one request creates the unique order and the remaining duplicate submissions read the existing result.

This keeps the sample focused on idempotency behavior instead of surfacing a SQLite unique-key race as a `500` response.

In a production multi-instance service, this should be enforced with database transactions, an atomic insert/upsert pattern, or another distributed coordination strategy at the state boundary.

## Payment timeout after reservation

The payment timeout scenario reserves inventory first, then models payment authorization timing out.

The sample treats the timeout as failed, releases inventory, and rejects the order with reason `PaymentTimeout`.

A production system might choose a pending payment confirmation state instead because timeout is an ambiguous failure.

The dashboard should make the sample policy visible without implying that this is the only valid production policy.

## Correlation ID display

The dashboard shows a correlation ID for a completed scenario run.

The ID is diagnostic metadata sent through the `X-Correlation-ID` header. It should be used to search gateway and backend logs for the same run.

The correlation ID is not business data. It exists to connect UI output with logs across local services and actor-backed components.

## Practical takeaway

The dashboard is part of the comparison, not just a convenience UI.

It should help the user answer these questions quickly:

- Are both backends available?
- Did both implementations receive the same scenario input?
- Did both implementations preserve the same business invariants?
- How many request submissions were sent?
- How many unique successful orders were created?
- Were duplicate submissions handled idempotently?
- Was inventory released when the sample policy required compensation?
- Which correlation ID should be used to inspect logs?

The dashboard should keep the comparison understandable without hiding the distributed-systems trade-offs that the repository is designed to show.
