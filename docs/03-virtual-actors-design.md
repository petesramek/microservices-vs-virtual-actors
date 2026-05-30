# Virtual actors design

The virtual actor-style implementation is split by stateful identity:

- `OrderGrain(orderId)` owns the order workflow state machine.
- `InventoryItemGrain(productId)` owns inventory state for one product and serializes reservation attempts for that product.
- `PaymentAccountGrain(customerId)` simulates payment authorization and idempotency for one customer/account identity.

The API exposes the same external order and inventory contract as the microservice implementation. Internally, workflow coordination moves from service-to-service HTTP calls to strongly typed grain calls.

## Phase 3 behavior

Phase 3 adds an in-process Orleans host to `Ordering.Api`, a standalone `Ordering.Silo` host, grain interfaces and implementations, and grain-level tests using `Microsoft.Orleans.TestingHost`.
