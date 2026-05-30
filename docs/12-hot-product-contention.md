# Hot product contention

The hot product contention scenario sends many concurrent orders for the same product.

The goal is to show that both architectures still have a contention point when all work targets the same state identity.

## Expected behavior

- completed orders must not exceed available stock
- remaining inventory must not go below zero
- rejected orders are expected when demand exceeds stock

## Microservices interpretation

Inventory.Api owns product inventory state. It must explicitly protect the reservation invariant with service-owned concurrency control.

The hot product is effectively a hot state key inside the inventory service or its backing store.

## Virtual actors interpretation

InventoryItemGrain owns product inventory state for one product identity. Orleans serializes calls for that grain activation, so over-reservation is prevented by the actor identity boundary.

The same product identity can still become a hot grain and therefore a bottleneck.
