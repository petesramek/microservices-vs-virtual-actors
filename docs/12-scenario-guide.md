# Scenario guide

This guide consolidates the scenario-specific documentation for the architecture comparison sample.

The scenarios compare the same order workflow across two implementation styles:

- a microservices-style implementation with explicit HTTP service boundaries
- a virtual actor-style implementation with identity-based grains and serialized execution per actor identity

The goal is not to prove that one architecture is universally better. The goal is to make trade-offs visible across state ownership, concurrency, failure handling, idempotency, release/versioning, and operations.

## How to read this guide

Each scenario is described with the same structure:

- **Purpose**: what the scenario demonstrates
- **Expected result**: what the scenario should show in the UI
- **Microservices interpretation**: how the responsibility is expressed in the service-based design
- **Virtual actors interpretation**: how the responsibility is expressed in the actor-based design
- **State ownership lesson**: where the important state or invariant lives
- **Concurrency or failure lesson**: what can go wrong and how the design handles it
- **Release and versioning note**: what must be considered when this behavior changes over time
- **Operational note**: what operators should observe when diagnosing the scenario

## Common result terminology

Result cards use the following terminology consistently:

- **Total request submissions**: how many requests were submitted for this scenario run
- **Unique successful orders**: how many unique logical orders completed successfully
- **Rejected submissions**: how many logical submissions were rejected
- **Idempotent duplicate responses**: how many duplicate submissions returned an existing logical result
- **Remaining inventory**: final inventory quantity after the scenario run
- **Elapsed time**: local run feedback, not a benchmark result

A request submission is an attempt sent to the backend. A unique successful order is a logical order that completed successfully. These counts are not always the same, especially in duplicate request and concurrent scenarios.

## Successful order

### Purpose

Demonstrates the happy path. Inventory is available, payment succeeds, and the order completes.

### Expected result

With initial stock `10` and quantity `1`:

- total request submissions: `1`
- unique successful orders: `1`
- rejected submissions: `0`
- idempotent duplicate responses: `0`
- remaining inventory: `9`
- status: `Fulfilled`

### Microservices interpretation

`Orders.Api` orchestrates the workflow. `Inventory.Api` owns product inventory state. `Payments.Api` owns payment authorization. The order workflow crosses explicit HTTP boundaries and must handle each downstream response.

### Virtual actors interpretation

`OrderGrain(orderId)` owns the order workflow. `InventoryItemGrain(productId)` owns inventory for one product identity. `PaymentAccountGrain(customerId)` owns payment behavior. The workflow is expressed as grain interactions rather than service-to-service HTTP orchestration inside the domain model.

### State ownership lesson

Both designs need a clear owner for inventory state. The difference is how ownership is expressed:

- microservices: service and persistence boundary
- virtual actors: actor identity boundary

### Concurrency or failure lesson

The happy path does not stress concurrency, but it establishes the baseline invariants that the other scenarios challenge.

### Release and versioning note

Changing the successful order contract is high impact because every client and every scenario depends on the happy-path response shape. Additive response changes are safer than renaming fields or changing status semantics.

### Operational note

Use the correlation ID shown in the UI to trace the scenario through gateway and backend logs. In the microservices path, expect log entries across `Orders.Api`, `Inventory.Api`, and `Payments.Api`. In the virtual actor path, expect log entries around `Ordering.Api` and grain workflow execution.

## Insufficient inventory

### Purpose

Demonstrates business rejection before payment. The requested quantity is greater than available inventory, so the order should be rejected and payment should not be attempted.

### Expected result

With initial stock `1` and quantity `2`:

- total request submissions: `1`
- unique successful orders: `0`
- rejected submissions: `1`
- idempotent duplicate responses: `0`
- remaining inventory: `1`
- reason: `InsufficientInventory`

### Microservices interpretation

`Inventory.Api` rejects the reservation. `Orders.Api` must stop the workflow and avoid calling `Payments.Api`. The inventory service owns the invariant that stock cannot be reserved when unavailable.

### Virtual actors interpretation

`InventoryItemGrain(productId)` rejects the reservation for the product identity. `OrderGrain(orderId)` stops the workflow and returns a rejected order result.

### State ownership lesson

The inventory owner decides whether stock is available. Other components should not duplicate inventory availability rules in a way that can diverge.

### Concurrency or failure lesson

This scenario is primarily a business-rule failure. It becomes more complex under concurrency, which is covered by the concurrent orders and hot product contention scenarios.

### Release and versioning note

Changing rejection reasons is a semantic contract change. Clients, dashboards, alerts, and tests may rely on `InsufficientInventory` to distinguish a business rejection from a technical failure.

### Operational note

A correct run should not show payment authorization for this scenario. If payment appears in logs, the orchestration sequence is wrong.

## Payment failure with compensation

### Purpose

Demonstrates compensation after a known downstream failure. Inventory is reserved first, payment explicitly fails, and the reservation is released.

### Expected result

With initial stock `10` and quantity `2`:

- total request submissions: `1`
- unique successful orders: `0`
- rejected submissions: `1`
- idempotent duplicate responses: `0`
- remaining inventory: `10`
- reason: `PaymentFailed`

### Microservices interpretation

`Orders.Api` coordinates a multi-service workflow. It calls `Inventory.Api` to reserve stock, calls `Payments.Api`, receives an explicit payment failure, and calls `Inventory.Api` again to release the reservation.

### Virtual actors interpretation

`OrderGrain(orderId)` coordinates the workflow across `InventoryItemGrain(productId)` and `PaymentAccountGrain(customerId)`. When payment fails, `OrderGrain(orderId)` explicitly asks `InventoryItemGrain(productId)` to release the reservation.

### State ownership lesson

Inventory remains owned by the inventory component. Compensation does not mean the orchestrator owns inventory; it means the orchestrator requests the inventory owner to undo a previous reservation.

### Concurrency or failure lesson

Compensation is part of distributed workflow design. The failure is known, so releasing inventory is safe in this simplified sample.

### Release and versioning note

Changing compensation semantics can be breaking even if the API shape does not change. For example, changing from immediate release to delayed release affects inventory availability and client expectations.

### Operational note

Logs should show reservation, payment failure, and reservation release under the same correlation ID. Missing release logs indicate a potential compensation bug.

## Payment timeout after reservation

### Purpose

Demonstrates timeout handling after inventory has already been reserved. The sample treats timeout as failed, releases inventory, and rejects the order.

### Expected result

With initial stock `10` and quantity `2`:

- total request submissions: `1`
- unique successful orders: `0`
- rejected submissions: `1`
- idempotent duplicate responses: `0`
- remaining inventory: `10`
- reason: `PaymentTimeout`

### Microservices interpretation

`Orders.Api` reserves inventory, observes a simulated payment timeout, and releases the reservation. The timeout is presented separately from explicit payment failure because the operational meaning is different.

### Virtual actors interpretation

`OrderGrain(orderId)` coordinates the same policy through grain calls. The actor model helps express workflow state, but timeout policy remains a business decision rather than something solved automatically by actors.

### State ownership lesson

The order workflow owns the decision policy, while inventory still owns stock state. The workflow decides whether to release, retry, or hold a reservation; the inventory owner performs the actual state transition.

### Concurrency or failure lesson

A timeout is ambiguous in real systems. This sample uses the simple policy `timeout => release and reject` to keep the demo deterministic. A production system might use pending confirmation and reconciliation instead.

### Release and versioning note

Changing timeout behavior from rejected to pending would be a semantic breaking change. UI labels, alerts, tests, retry behavior, and client expectations would all need to be reviewed.

### Operational note

Logs should clearly distinguish `PaymentTimeout` from `PaymentFailed`. The correlation ID should connect the reservation, timeout decision, release, and final rejected result.

## Concurrent orders

### Purpose

Demonstrates multiple independent order submissions competing for the same product stock at the same time.

### Expected result

With initial stock `3`, quantity `1`, and `10` concurrent requests:

- total request submissions: `10`
- unique successful orders: `3`
- rejected submissions: `7`
- idempotent duplicate responses: `0`
- remaining inventory: `0`
- reason: `SomeOrdersRejected`

### Microservices interpretation

`Inventory.Api` must explicitly protect the reservation invariant with service-owned concurrency control. `Orders.Api` submits independent orders and relies on `Inventory.Api` for correct stock decisions.

### Virtual actors interpretation

`InventoryItemGrain(productId)` owns the product identity. Calls for that product identity are serialized through the grain activation, so the inventory invariant is protected at the actor identity boundary.

### State ownership lesson

The important invariant is not owned by the caller. It is owned by the component responsible for product inventory state.

### Concurrency or failure lesson

Correctness means completed orders must not exceed available stock and remaining inventory must not go below zero. Rejections are expected when demand exceeds stock.

### Release and versioning note

Changing concurrency behavior can alter business outcomes even if contracts stay the same. For example, switching reservation strategy from pessimistic locking to optimistic retries can affect latency, rejection timing, and operational profiles.

### Operational note

The result should be interpreted as a batch outcome. Completed and rejected counts refer to different request submissions, not the same submission being both completed and rejected.

## Hot product contention

### Purpose

Demonstrates many concurrent requests targeting one hot product identity. The scenario shows that both designs still have a contention point when all work targets the same state identity.

### Expected result

With initial stock `25`, quantity `1`, and `50` concurrent requests:

- total request submissions: `50`
- unique successful orders: `25`
- rejected submissions: `25`
- idempotent duplicate responses: `0`
- remaining inventory: `0`
- reason: `SomeOrdersRejected`

### Microservices interpretation

`Inventory.Api` owns product inventory state and must protect the reservation invariant. Even with separate services, one hot product can concentrate load on one state key, database row, or partition.

### Virtual actors interpretation

`InventoryItemGrain(productId)` owns the hot product identity. Orleans-style per-identity serialization prevents over-reservation for that product, but the same identity can still become a hot grain and therefore a throughput bottleneck.

### State ownership lesson

State ownership protects correctness but does not eliminate contention. If all requests target one identity, the owner of that identity becomes the coordination point.

### Concurrency or failure lesson

This scenario separates correctness from scalability. The correct result includes rejected submissions when demand exceeds stock. Faster local timings should not be interpreted as a universal benchmark.

### Release and versioning note

Optimizing hot product behavior may require partitioning, sharding, reservation queues, or product-specific scaling strategies. Those changes can affect deployment topology and operational procedures.

### Operational note

Watch completed/rejected counts, remaining inventory, elapsed time, and logs for the hot product ID. Correlation ID helps connect UI results to backend logs, but product ID is the key diagnostic dimension for contention.

## Duplicate request

### Purpose

Demonstrates idempotency under repeated duplicate submissions. The scenario submits the same logical order multiple times concurrently using the same order identity and idempotency key.

### Expected result

With initial stock `10`, quantity `2`, and `20` duplicate request submissions:

- total request submissions: `20`
- unique successful orders: `1`
- rejected submissions: `0`
- idempotent duplicate responses: `19`
- remaining inventory: `8`
- reason: `IdempotentResultReturned`

### Microservices interpretation

`Orders.Api` must explicitly protect idempotency key creation and lookup. A unique index is useful, but a unique index alone can surface a concurrency race as a database exception unless the service coordinates concurrent duplicate submissions or handles unique-key conflicts correctly.

### Virtual actors interpretation

`OrderGrain(orderId)` uses a stable order identity. Duplicate submissions for the same order identity are naturally serialized at the grain boundary and can return the existing logical result.

### State ownership lesson

Idempotency state is real state. The system needs a clear owner for the mapping between idempotency key and logical order result.

### Concurrency or failure lesson

Idempotency is not only about retrying after a completed request. It must also work when duplicate submissions arrive concurrently before the first request has finished.

### Release and versioning note

Changing idempotency semantics is a behavior contract change. Clients may rely on whether duplicate submissions return the original result, a special duplicate result, or an error. This must be versioned and documented carefully.

### Operational note

The key metrics are total request submissions, unique successful orders, and idempotent duplicate responses. Inventory should be reduced once by the requested quantity, not once per duplicate submission.

## Cross-scenario lessons

### State ownership is the central comparison point

Most scenarios are about identifying the correct owner for state and invariants:

- product inventory belongs to `Inventory.Api` or `InventoryItemGrain(productId)`
- order workflow belongs to `Orders.Api` or `OrderGrain(orderId)`
- payment behavior belongs to `Payments.Api` or `PaymentAccountGrain(customerId)`
- idempotency belongs to the component that creates and returns logical order results

The architectures differ less in whether ownership is needed and more in how ownership is expressed.

### Concurrency guarantees are not free

Microservices require explicit concurrency control at service, database, or partition boundaries.

Virtual actors provide serialized execution per actor identity, which is useful for per-identity invariants, but hot identities can still become bottlenecks.

### Failure handling is a policy decision

The architecture does not decide whether timeout means rejection, retry, compensation, or pending confirmation.

The architecture affects how clearly the workflow can express and enforce that policy.

### Versioning is part of architecture

Changing status values, reason strings, idempotency behavior, timeout policy, or metric meanings can break clients even when method signatures or JSON shapes remain compatible.

Scenario behavior is part of the contract.

### Timings are demo-local, not benchmark proof

Elapsed times in this sample are useful for observing shape and coordination overhead in the local topology.

They should not be interpreted as a general performance benchmark for either architecture.

## What these scenarios do not prove

These scenarios do not prove that one architecture is universally better.

They also do not prove production performance, operational cost, or correctness under every possible failure mode.

The scenarios are intentionally small and deterministic so the comparison remains focused on where each architecture places state ownership, workflow coordination, concurrency control, idempotency, and failure handling.
