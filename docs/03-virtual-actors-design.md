# Virtual actors design

The virtual actor-style implementation is organized around stateful identities rather than deployable business services.

The main actors are:

- `OrderGrain(orderId)`
- `InventoryItemGrain(productId)`
- `PaymentAccountGrain(customerId)`

The API exposes the same external order and inventory contract as the microservice implementation. Internally, workflow coordination moves from service-to-service HTTP calls to strongly typed grain calls.

## Stateful identity boundaries

Virtual actors are useful in this comparison because the workflow naturally contains stateful identities.

Instead of splitting the workflow primarily by deployable service capability, the virtual actor implementation asks:

- Which identity owns the order workflow?
- Which identity owns inventory for one product?
- Which identity owns payment behavior for one customer or account?
- Which operations should be serialized for a single identity?

This makes state ownership explicit at the actor identity boundary.

## Grain responsibilities

### OrderGrain(orderId)

`OrderGrain` owns one logical order workflow.

It coordinates inventory reservation, payment authorization, compensation, and the final order outcome for one order identity.

If the same logical order is submitted again, the order grain can return the existing result instead of executing the workflow again. This makes idempotency easier to express when the order identity is stable.

### InventoryItemGrain(productId)

`InventoryItemGrain` owns inventory state for one product identity.

It is responsible for tracking available inventory, accepting valid reservations, rejecting reservations when stock is insufficient, and releasing reservations when compensation is required.

The inventory grain is the state boundary for the inventory invariant:

> Available inventory must not go below zero.

Because calls to a single grain activation are serialized by the actor runtime, reservation attempts for the same product identity are processed one at a time. This is the main concurrency difference from the microservices implementation, where concurrency control must be implemented explicitly at the service or persistence boundary.

### PaymentAccountGrain(customerId)

`PaymentAccountGrain` simulates payment authorization behavior for one customer or account identity.

It models successful authorization, explicit payment failure, timeout-oriented behavior, and idempotent authorization responses for the sample scenarios.

The payment grain is intentionally small. Its purpose is to make payment behavior part of the same stateful workflow comparison without introducing a real payment provider integration.

## Workflow shape

A successful order follows this general path:

```text
Client / Gateway
  -> Ordering.Api
      -> OrderGrain(orderId)
          -> InventoryItemGrain(productId) reserve
          -> PaymentAccountGrain(customerId) authorize
          -> OrderGrain(orderId) complete order
```

A failed payment after reservation follows this general path:

```text
Client / Gateway
  -> Ordering.Api
      -> OrderGrain(orderId)
          -> InventoryItemGrain(productId) reserve
          -> PaymentAccountGrain(customerId) authorize fails
          -> InventoryItemGrain(productId) release reservation
          -> OrderGrain(orderId) reject order
```

This keeps the workflow coordination inside the actor model while still preserving clear state ownership boundaries.

## Concurrency model

The virtual actor implementation relies on per-identity serialization.

For example, all reservation attempts for the same product identity are routed to `InventoryItemGrain(productId)`. That grain owns the product inventory state and processes calls for that identity sequentially.

This helps protect single-identity invariants such as:

- completed orders must not exceed available stock
- remaining inventory must not become negative
- duplicate requests for the same logical order must not create multiple unique orders

This does not mean virtual actors remove contention. A hot product can still become a hot grain. The actor model makes the contention explicit around the product identity.

## Idempotency model

Idempotency is modeled through stable workflow identity and stored grain state.

`OrderGrain(orderId)` owns the logical order result. If duplicate submissions target the same order identity, the grain can return the existing result instead of reserving inventory again.

`PaymentAccountGrain(customerId)` can also store authorization results by idempotency key so duplicate payment authorization attempts do not create inconsistent behavior.

This is different from the microservices implementation, where idempotency is coordinated explicitly by `Orders.Api` and its persistence strategy.

## Failure handling

The virtual actor design still needs explicit failure policies.

The actor model helps express workflow state, but it does not decide business behavior automatically. The implementation must still define what happens when:

- inventory is insufficient
- payment fails after inventory was reserved
- payment times out after inventory was reserved
- inventory needs to be released as compensation
- duplicate requests arrive while a workflow is already in progress

In this sample, these policies are deterministic so both architecture implementations can be compared using the same scenario expectations.

## Deployment and operations

The sample includes:

- `Ordering.Api`
- `Ordering.Grains`
- `Ordering.Silo`

`Ordering.Api` exposes the HTTP entry point for the virtual actor backend. The grain interfaces and implementations live in `Ordering.Grains`. `Ordering.Silo` represents a standalone Orleans silo host.

The local sample may host Orleans in-process for simplicity, but the design still highlights actor-specific operational concerns:

- grain activation and placement
- actor runtime behavior
- hot grains
- grain state compatibility
- runtime and silo deployment strategy
- observability around actor identities

The external API remains intentionally similar to the microservices API so the comparison focuses on internal workflow structure rather than client-facing contract differences.

## Trade-offs highlighted by this design

The virtual actor implementation is useful for showing:

- stateful identity boundaries
- workflow ownership by order identity
- inventory ownership by product identity
- serialized execution per actor identity
- strongly typed internal grain calls
- explicit actor-state evolution concerns
- hot identity bottlenecks
- runtime-managed activation and placement

The design is intentionally small so the actor model trade-offs remain visible without introducing unrelated production infrastructure.
