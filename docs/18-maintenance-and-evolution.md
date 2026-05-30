# Maintenance and evolution

This document compares how the microservices-style and virtual actor-style implementations evolve after the first version is released.

The focus is not only code change mechanics. The focus is how each architecture affects long-term ownership, safe change, feature evolution, refactoring, testing, and operational confidence.

## Summary

Both architecture styles can be maintained successfully, but they concentrate maintenance work in different places.

- **Microservices** concentrate evolution around service ownership, HTTP/API contracts, database ownership, and explicit workflow coordination.
- **Virtual actors** concentrate evolution around actor identity design, grain interfaces, persistent grain state, runtime behavior, and actor workflow boundaries.

The core maintenance question is not "which style changes less?" The better question is:

> When business behavior changes, where does the change land, and how safely can that change be released?

## Maintenance model comparison

### Microservices-style implementation

The microservices-style implementation separates responsibilities into explicit services:

- `Orders.Api` owns order workflow orchestration.
- `Inventory.Api` owns inventory state and reservation invariants.
- `Payments.Api` owns payment authorization behavior.
- `Comparison.Gateway` coordinates the demo comparison.
- `Comparison.Ui` presents scenarios and results.

This makes ownership visible. A team can often change one service without redeploying every other service, provided the service contract remains compatible.

The maintenance cost is that every service boundary becomes a compatibility boundary. A small workflow change may require updates across multiple services, tests, docs, dashboards, and deployment sequencing.

### Virtual actor-style implementation

The virtual actor-style implementation organizes behavior around logical identities:

- `OrderGrain` owns one order workflow identity.
- `InventoryItemGrain` owns one product inventory identity.
- `PaymentAccountGrain` owns one payment/account behavior identity.
- `Ordering.Api` exposes entry points into the actor workflow.

This can make state ownership and workflow code easier to reason about because behavior is colocated with identity-specific state.

The maintenance cost is that actor identity, grain interfaces, persisted state schema, and runtime assumptions become central design assets. Poor grain boundaries can be as painful as poor service boundaries.

## Common evolution scenarios

## Adding a new payment provider

### Microservices

A new payment provider can often be hidden inside `Payments.Api`.

Possible change path:

1. Add provider-specific implementation inside `Payments.Api`.
2. Keep the existing payment API stable.
3. Add configuration or routing rules for provider selection.
4. Extend tests around payment success, failure, and timeout behavior.
5. Deploy `Payments.Api` independently if the contract stays compatible.

Benefits:

- `Orders.Api` may remain unchanged.
- Provider-specific complexity stays inside the payment service.
- Rollback can target `Payments.Api`.

Risks:

- if provider behavior requires new payment states, `Orders.Api` and UI result semantics may need changes
- timeout/retry behavior may differ by provider
- contract compatibility must be preserved for existing callers

### Virtual actors

A new payment provider likely changes `PaymentAccountGrain` or related payment abstractions.

Possible change path:

1. Add provider-specific behavior behind the payment grain.
2. Keep grain method contracts stable where possible.
3. Add provider selection to payment state or configuration.
4. Update grain tests and workflow tests.
5. Validate persisted state compatibility.

Benefits:

- payment behavior remains close to payment state.
- order workflow can stay stable if the payment grain contract stays stable.

Risks:

- new provider state may require grain state migration
- grain interface changes can affect all callers
- provider-specific retries/timeouts can complicate actor workflow state

## Changing inventory reservation rules

### Microservices

Inventory rules should primarily change inside `Inventory.Api` because that service owns inventory state.

Examples:

- reserve by available stock only
- reserve by warehouse location
- reserve with allocation priority
- reserve with backorder support
- reserve with expiration

If the API contract remains stable, `Orders.Api` can keep calling the same reservation endpoint. If response semantics change, callers and docs must be updated.

Maintenance risk appears when inventory rules leak into callers. If `Orders.Api` duplicates inventory availability logic, both services must be changed together, increasing the chance of inconsistency.

### Virtual actors

Inventory rules should primarily change inside `InventoryItemGrain` because the grain owns product inventory identity.

Examples:

- adding reservation expiration fields to grain state
- adding allocation policy to the grain behavior
- splitting one product identity into product-location identities
- changing how rejection reasons are produced

The colocated state and behavior can simplify rule changes, but persistent grain state must remain compatible.

## Adding a new workflow step

Example: add a fraud check after payment authorization but before order completion.

### Microservices

Likely changes:

- add `Fraud.Api` or extend an existing risk service
- update `Orders.Api` orchestration
- define new compensation behavior if fraud fails after inventory/payment steps
- update gateway timelines and scenario docs
- update contract and integration tests

The explicit service boundary is clear, but orchestration complexity grows as more services participate.

### Virtual actors

Likely changes:

- add `FraudCheckGrain` or a fraud-check collaborator
- update `OrderGrain` workflow
- decide whether fraud state belongs to order, account, or separate identity
- update grain tests and scenario regression tests

The workflow may remain easier to read inside `OrderGrain`, but the grain can become too large if too many responsibilities accumulate there.

## Changing timeout policy

The current demo treats payment timeout as failed:

- inventory is released
- order is rejected
- reason is `PaymentTimeout`

A future production-style policy might instead mark the order pending.

### Microservices

Changing timeout policy may require:

- new order state
- new database fields
- retry or reconciliation process
- operational alerts for stuck pending orders
- UI changes
- API versioning or compatibility handling

This is not just an implementation change. It changes business semantics.

### Virtual actors

Changing timeout policy may require:

- new grain state fields
- reminders/timers for reconciliation
- new workflow transitions in `OrderGrain`
- state migration for existing orders
- UI and result contract updates

The actor model can express long-running workflow state naturally, but policy design and state lifecycle still need explicit decisions.

## Changing idempotency semantics

The duplicate request scenario demonstrates that idempotency is a first-class behavior.

### Microservices

Idempotency in `Orders.Api` requires safe coordination around idempotency keys.

Potential future changes:

- idempotency key retention period
- whether duplicate responses return original result or a special duplicate result
- whether duplicate rejected orders can be retried
- how idempotency interacts with customer/product identity
- whether idempotency records are archived

Maintenance risks:

- changing semantics can break clients that rely on safe retries
- unique indexes catch duplicates but do not fully define user-facing behavior
- concurrent duplicate submissions must remain safe

### Virtual actors

Idempotency can be modeled through stable actor identity, such as one `OrderGrain` per logical order.

Potential future changes:

- grain key strategy
- idempotency retention
- duplicate response semantics
- order replay/recovery behavior

Maintenance risks:

- changing grain key strategy is a major migration concern
- persisted grain state must remain readable
- callers must understand whether identity is generated by client or server

## Changing scenario result contracts

The result model currently includes:

- total request submissions
- unique successful orders
- rejected submissions
- idempotent duplicate responses
- remaining inventory
- elapsed time
- reason
- timeline events

These fields are part of the demo contract. Changing their meaning can be breaking even if property names stay the same.

Examples:

- changing `CompletedOrders` from unique logical orders to raw successful HTTP responses would break duplicate request interpretation
- changing `RejectedOrders` from logical rejected submissions to failed technical calls would confuse scenario results
- changing reason strings would break tests and docs

Maintenance rule:

> Treat scenario metrics as stable semantic contracts, not just UI labels.

## Team ownership implications

## Microservices

Microservices map naturally to team ownership when service boundaries align with business capabilities.

Good ownership shape:

- one team owns inventory rules and `Inventory.Api`
- one team owns payment integration and `Payments.Api`
- one team owns ordering workflow and `Orders.Api`

Risky ownership shape:

- many teams frequently change the same service
- one business rule is split across several services without clear ownership
- database ownership is unclear
- every feature requires coordinated deployment across all services

## Virtual actors

Virtual actors map naturally to ownership around domain identities and workflows.

Good ownership shape:

- one team owns order workflow grains
- one team owns inventory grains
- one team owns payment grains
- grain interfaces are treated as contracts

Risky ownership shape:

- grains become large procedural orchestrators with too many responsibilities
- grain state schemas change without migration discipline
- actor identity strategy changes after data exists
- runtime/platform knowledge is concentrated in too few people

## Refactoring considerations

## Microservices

Refactoring inside a service is relatively safe when public APIs and database compatibility are preserved.

Refactoring across service boundaries is harder because it may require:

- API changes
- data migration
- deployment sequencing
- consumer updates
- contract test updates

## Virtual actors

Refactoring inside a grain is relatively safe when grain interfaces and persisted state remain compatible.

Refactoring actor boundaries is harder because it may require:

- grain key migration
- state migration
- interface changes
- workflow message compatibility
- runtime deployment care

## Testing maintenance

Regression tests should evolve with scenario semantics.

When changing behavior, update:

- scenario regression tests
- docs expected results
- scenario comparison matrix
- release/versioning notes if semantics changed
- UI wording if metrics changed

Recommended maintenance tests:

### Microservices

- service API contract tests
- database migration compatibility tests
- idempotency race tests
- compensation tests
- timeout policy tests
- gateway scenario regression tests

### Virtual actors

- grain behavior tests
- Orleans TestCluster workflow tests
- grain state serialization tests
- activation/concurrency tests
- timeout policy tests
- gateway scenario regression tests

## Maintenance checklist

Before changing existing behavior, ask:

- Which component owns the state being changed?
- Which invariant is affected?
- Does the change alter status, reason, or metric semantics?
- Does the change require a database or grain state migration?
- Can old and new versions run at the same time?
- Can the change be rolled back safely?
- Do scenario regression tests need updates?
- Do docs and README links need updates?
- Does observability still expose enough information to diagnose failures?

## Key takeaways

- Maintenance complexity does not disappear; it moves to different boundaries.
- Microservices make ownership and deployment boundaries explicit, but require strong contract and integration discipline.
- Virtual actors make identity-based state ownership explicit, but require strong grain interface and state evolution discipline.
- Semantic behavior is part of the contract and should be versioned, tested, and documented.
- A good comparison must evaluate how the system changes over time, not only how the first version is built.

