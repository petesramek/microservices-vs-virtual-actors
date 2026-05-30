# Trade-offs

## Concurrency

Both implementations should prevent over-reservation in the concurrent orders scenario.

This does not mean microservices are automatically concurrency-safe. It means the microservice implementation has explicit concurrency control at the state owner, `Inventory.Api`.

The virtual actor implementation prevents over-reservation through `InventoryItemGrain`, which serializes operations for a product identity.

The comparison is therefore:

- microservices: explicit protection at the service-owned state boundary
- virtual actors: natural per-identity serialization through the actor model

## Performance timing

Elapsed time in the UI is local demo feedback, not a benchmark.

The microservice workflow crosses more HTTP boundaries:

- gateway to Orders.Api
- Orders.Api to Inventory.Api
- Orders.Api to Payments.Api

The virtual actor workflow keeps more coordination inside the Orleans runtime path.

Production performance depends on network topology, persistence, placement, hot keys, deployment shape, and operational tuning.

## Practical takeaway

Microservices are useful when boundaries are organizational, deployable, and capability-oriented.

Virtual actors are useful when state is naturally partitioned by identity and the difficult part is coordinating many stateful identities.

## Hot identities

A hot product is a useful reminder that virtual actors do not remove contention. They make the contention explicit by state identity.

In the microservice implementation, the hot product is protected by Inventory.Api and its state store. In the virtual actor implementation, the hot product is protected by InventoryItemGrain for that product identity.
