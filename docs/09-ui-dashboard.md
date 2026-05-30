# UI dashboard

The Blazor Server dashboard is intentionally small. It is not a product UI or ecommerce frontend.

The UI has three jobs:

1. Run the same scenario against one or both implementations.
2. Show side-by-side results.
3. Make topology, backend readiness, progress feedback, validation, and trade-offs visible without reading every source file.

## Interpreting concurrent orders

The concurrent orders scenario expects both implementations to prevent over-reservation.

- Microservices do this because `Inventory.Api` explicitly owns and protects inventory state.
- Virtual actors do this because `InventoryItemGrain` serializes operations for a product identity.

The scenario demonstrates different places to enforce the same invariant, not that one architecture is inherently correct and the other is inherently broken.

## Interpreting elapsed time

Elapsed time in the dashboard is useful for local feedback, but it is not a benchmark.

In this sample, the microservice implementation crosses more HTTP service boundaries. The virtual actor implementation keeps more coordination inside the Orleans runtime path. Production performance depends on persistence, networking, placement, hot-key distribution, and deployment topology.

## Hot product contention

The hot product contention scenario concentrates concurrent orders on one product. It is intended to show that both architectures can have a bottleneck around a hot state identity.

- Microservices: Inventory.Api or its backing store becomes the contention point.
- Virtual actors: InventoryItemGrain for the product becomes the contention point.

The scenario is expected to prevent over-reservation in both implementations.

## Aggregate scenario result wording

Concurrent scenarios display aggregate order attempts. `Successful order attempts` and `Rejected order attempts` are separate request groups from the same run. A partially fulfilled result means some attempts completed while other attempts were rejected after inventory was exhausted.

Aggregate timelines describe the full batch instead of showing a single representative successful order.

## Standard order attempt metrics

Result cards always display the same attempt-based metrics:

- total order attempts
- successful order attempts
- rejected order attempts
- remaining inventory
- elapsed time

This keeps single-order and aggregate scenarios visually consistent. A partially fulfilled result means successful and rejected attempts are separate request groups from the same run.

## Request submission metrics

Result cards use request-submission metrics consistently across all scenarios:

- total request submissions
- unique successful orders
- rejected submissions
- idempotent duplicate responses
- remaining inventory
- elapsed time

For duplicate request scenarios, total request submissions can be greater than unique successful orders. This means the duplicate request returned an existing order result and did not reserve inventory again.

## Duplicate request concurrent count

The `Duplicate request` scenario uses `Concurrent requests` as the number of duplicate request submissions. Every submission reuses the same order identity and idempotency key.

Expected successful duplicate behavior:

- total request submissions equals concurrent requests
- unique successful orders is 1
- idempotent duplicate responses is total request submissions minus 1
- inventory is reduced once by the requested quantity

## Microservices duplicate idempotency race

The `Duplicate request` scenario submits the same idempotency key concurrently. Orders.Api now serializes POST `/api/orders` requests by idempotency key inside the local sample process so that one request creates the unique order and the remaining duplicate submissions read the existing result.

This keeps the sample focused on idempotency behavior instead of surfacing a SQLite unique-key race as a 500 response. In a production multi-instance service, this should be enforced with database transactions, an atomic insert/upsert pattern, or another distributed coordination strategy at the state boundary.

## Payment timeout after reservation

The payment timeout scenario reserves inventory first, then models payment authorization timing out. The demo treats the timeout as failed, releases inventory, and rejects the order with reason `PaymentTimeout`.

A production system might choose a pending payment confirmation state instead because timeout is an ambiguous failure.
