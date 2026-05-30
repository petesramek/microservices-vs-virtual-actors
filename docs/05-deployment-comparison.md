# Deployment comparison

The deployment shapes are intentionally different.

## Microservices deployment

The microservice-style deployment has three backend processes:

- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`

`Orders.Api` coordinates the workflow by calling `Inventory.Api` and `Payments.Api`. Each service can be deployed, configured, monitored, restarted, and scaled separately.

### Local Docker Compose

```bash
docker compose -f deploy/microservices/docker-compose.yml up --build
```

### Trade-offs

Pros:

- Independent service deployment.
- Clear service ownership boundaries.
- Different services can be scaled independently.
- Service APIs are explicit.

Cons:

- More deployable units.
- More network paths.
- More configuration.
- More operational surface area.
- Workflow consistency requires explicit compensation and idempotency.

## Virtual actors deployment

The virtual actor-style deployment has an `Ordering.Api` process hosting Orleans and grain activations.

The workflow is coordinated by grains:

- `OrderGrain`
- `InventoryItemGrain`
- `PaymentAccountGrain`

### Local Docker Compose

```bash
docker compose -f deploy/virtual-actors/docker-compose.yml up --build
```

### Trade-offs

Pros:

- The domain workflow is modeled around stateful identities.
- Per-product inventory coordination is localized in `InventoryItemGrain`.
- Fewer explicit service-to-service calls in application code.
- Runtime manages grain activation and placement.

Cons:

- Orleans runtime behavior becomes part of the operational model.
- Hot grains can become bottlenecks.
- Persistence and clustering choices matter.
- Deployment independence is different from microservice-style service boundaries.

## Full comparison stack

Run the UI, gateway, microservices backend, and virtual actor backend together:

```bash
docker compose -f deploy/docker-compose.full.yml up --build
```
