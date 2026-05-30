# Microservices design

The microservice-style implementation is split by deployable business capability:

- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`

Each service owns its data and communicates through HTTP APIs.

The design makes service boundaries, deployment independence, and operational surface area visible.
