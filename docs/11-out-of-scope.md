# Out of scope

This repository intentionally keeps the workflow and infrastructure small.

The following topics are out of scope for the current version:

- authentication and authorization
- frontend product UI
- shopping cart, discounts, taxes, shipping, refunds, and notifications
- message brokers
- event sourcing
- Kubernetes manifests
- service mesh
- distributed tracing backends
- production Orleans clustering and persistence providers
- production-grade database migration strategy

These topics can be added later if they help the comparison, but they are not required for the core purpose of the repository.

The current purpose is to compare how the same stateful workflow looks when implemented with service/capability boundaries versus stateful identity boundaries.
