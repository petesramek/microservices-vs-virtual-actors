# Trade-offs

This repository keeps the trade-offs explicit instead of treating either model as universally better.

## Microservices

Pros:

- Independent deployment.
- Clear business capability boundaries.
- Natural fit for separate teams and ownership.
- Independent service scaling.
- Explicit API contracts.

Cons:

- More operational surface area.
- More network calls and failure modes.
- Distributed workflow consistency must be designed explicitly.
- Local development requires more processes.
- Idempotency and compensation are required for realistic workflows.

## Virtual actors

Pros:

- Natural fit for stateful identities.
- Per-identity coordination is localized.
- Turn-based actor execution can simplify some concurrency problems.
- Runtime manages activation and placement.
- Workflow code can be easier to follow when the domain is identity-centric.

Cons:

- Orleans runtime behavior must be understood.
- Clustering, persistence, and placement become architectural concerns.
- Hot actors can become bottlenecks.
- Deployment independence is not the same as independent microservice deployment.
- Not every service boundary should become an actor.

## Practical takeaway

Microservices are often a good fit when the system boundary is organizational, deployable, and capability-oriented.

Virtual actors are often a good fit when the hard part is coordinating many stateful identities with concurrent operations.
