# Scaling comparison

The two approaches scale along different axes.

## Microservices scaling

Microservices scale by service instance.

Examples:

```bash
docker compose -f deploy/microservices/docker-compose.yml up --build --scale inventory-api=3
```

This is useful when one service has a different load profile from the others. For example, inventory reservation can be scaled separately from payment authorization.

Trade-offs:

- Scaling a service adds capacity for that service boundary.
- State consistency is still owned by the service and its database/update strategy.
- More instances create more logs, metrics, health checks, and network paths.

## Virtual actors scaling

Virtual actors scale by adding Orleans silo capacity and distributing grain activations.

In this sample, `Ordering.Api` hosts Orleans in-process for simplicity. A production-style deployment would normally separate API hosting and silo hosting or run multiple silo instances.

Trade-offs:

- Adding silos increases cluster capacity.
- Grain placement determines where stateful identities execute.
- Hot identities can still become bottlenecks.
- Actor-level serialization can simplify correctness for a single identity, but does not remove the need to design around hotspots.

## Blazor Server UI scaling note

The comparison UI uses Blazor Server because the dashboard is a developer-facing tool. Server-side UI state and SignalR circuits are part of the UI deployment model and should not be confused with the backend architecture comparison.
