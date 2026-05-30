# Release, deployment, and versioning

This document compares how the microservices-style and virtual actor-style implementations are released, deployed, versioned, and rolled back over time.

The comparison is intentionally broader than code structure. Architecture choices affect delivery flow, compatibility rules, operational risk, and how teams evolve systems safely.

## Summary

Both architecture styles need release discipline. The pressure appears at different boundaries:

- **Microservices** expose release and versioning pressure at HTTP/API boundaries, database boundaries, service-to-service contracts, and deployment ordering.
- **Virtual actors** expose release and versioning pressure at grain interface boundaries, actor state schemas, runtime/silo deployment, serialization compatibility, and activation lifecycle.

Neither style removes the need for versioning. Each style moves versioning concerns to different places.

## Deployment unit comparison

### Microservices-style implementation

The microservices-style implementation has multiple separately deployable processes:

- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`
- `Comparison.Gateway`
- `Comparison.Ui`

This enables independent deployment, but it also creates version skew. During a rolling deployment, `Orders.Api` might run a new version while `Inventory.Api` or `Payments.Api` still runs an older version.

That means each service API must be compatible across at least the deployment window.

### Virtual actor-style implementation

The virtual actor-style implementation has fewer external service boundaries, but many logical stateful identities inside the runtime:

- `Ordering.Api`
- Orleans silo/runtime
- `OrderGrain`
- `InventoryItemGrain`
- `PaymentAccountGrain`
- grain storage

The deployment unit may look simpler from the outside, but versioning still matters. Grain interfaces, serialized method arguments, persistent grain state, and activation behavior must remain compatible during upgrades.

## Independent deployment

### Microservices

Microservices are commonly optimized for independent deployment. For example, `Payments.Api` can often be released without redeploying `Orders.Api`, as long as the payment API contract remains compatible.

Benefits:

- teams can own services independently
- individual services can be scaled and deployed separately
- rollback can target one service
- technology choices can vary by service if needed

Costs:

- every API boundary needs compatibility discipline
- every service-to-service call can fail independently
- rolling deployments create version skew
- database migrations must be compatible with old and new code
- integration and contract testing become important

### Virtual actors

Virtual actors can reduce the number of explicit service-to-service HTTP contracts inside the workflow. `OrderGrain` can call `InventoryItemGrain` and `PaymentAccountGrain` as logical domain collaborators.

Benefits:

- fewer explicit internal HTTP APIs for workflow steps
- state ownership is tied to actor identity
- per-identity serialization can simplify concurrency-sensitive code
- workflow code can be easier to read as a single logical flow

Costs:

- grain interface compatibility must be managed
- grain state schema evolution must be planned
- Orleans/runtime deployment behavior must be understood
- rolling upgrades must consider old and new grain activations
- runtime/platform knowledge becomes part of operations

## Versioning boundaries

## HTTP/API contracts

Microservices expose explicit HTTP contracts. Common contract changes include:

- adding fields
- removing fields
- renaming fields
- changing enum values
- changing response status codes
- changing error/reason semantics
- changing idempotency behavior

Additive changes are usually safer. Breaking changes require versioning, compatibility shims, or coordinated deployment.

Example:

- Adding `IdempotentResponses` to a result model is additive if old clients ignore unknown fields.
- Renaming `RejectedOrders` to `RejectedSubmissions` is breaking if clients deserialize by property name.
- Changing `PaymentTimeout` from a rejected outcome to a pending outcome is a semantic breaking change even if the JSON shape remains the same.

## Grain interfaces

Virtual actors expose logical contracts through grain interfaces and serialized method calls.

Common grain interface changes include:

- adding a method
- removing a method
- changing method parameters
- changing return types
- changing serialized DTO shapes
- changing grain key strategy
- changing timeout or idempotency semantics

Adding a new grain method is usually safer than changing an existing method signature. Changing the meaning of an existing method can break callers even when the code compiles.

## Persistent state schemas

Both styles persist state.

### Microservices

Each service may own one or more database schemas. Database migrations must support safe rollout.

Safer migration pattern:

1. Add nullable/new columns or new tables.
2. Deploy code that writes both old and new shapes if needed.
3. Backfill data.
4. Deploy code that reads the new shape.
5. Remove old schema only after all old code is gone.

Risky migration pattern:

1. Rename or remove a column.
2. Deploy while old code still expects the old column.
3. Rolling deployment fails because old and new versions cannot both run.

### Virtual actors

Grain state also has schema. Persisted actor state must be compatible across code versions.

Safer grain-state evolution pattern:

1. Add optional fields to grain state.
2. Make new code tolerate missing fields.
3. Populate new fields lazily or through a migration process.
4. Avoid changing meaning of existing fields without a migration strategy.

Risky grain-state evolution pattern:

1. Change serialized field names or types without compatibility support.
2. Activate old persisted grains with new code that cannot read their state.
3. Fail at activation or during workflow execution.

## Scenario-specific versioning notes

### Successful order

The happy path is the most widely consumed behavior. Any change to the successful order response can affect all clients, tests, dashboards, and documentation.

Versioning risk:

- changing `Completed` or `Fulfilled` semantics
- changing inventory decrement behavior
- changing payment authorization response shape

### Insufficient inventory

The reason `InsufficientInventory` is a business outcome, not a technical error. Clients may use it to show specific UI messages or decide whether retry makes sense.

Versioning risk:

- renaming the reason
- returning a generic error instead of a business rejection
- attempting payment even after inventory rejection

### Payment failure compensation

Compensation semantics are behavior contracts. A client may assume that inventory is released after payment failure.

Versioning risk:

- changing release timing
- changing the final order state
- changing whether payment failure is retryable

### Payment timeout after reservation

Timeout behavior is especially sensitive because timeout is ambiguous in production systems.

Current sample policy:

- timeout is treated as failed
- inventory is released
- order is rejected with `PaymentTimeout`

Versioning risk:

- changing timeout from rejected to pending
- holding inventory after timeout
- adding retry/reconciliation behavior

These changes are semantic contract changes even if request and response DTOs remain unchanged.

### Concurrent orders

Concurrent outcomes depend on reservation strategy. If reservation behavior changes, the counts can change.

Versioning risk:

- changing from immediate rejection to queueing
- changing retry behavior
- changing reservation consistency strategy
- changing status/reason mapping for partial fulfillment

### Hot product contention

Optimizing hot product handling can require topology changes.

Versioning risk:

- sharding inventory ownership
- introducing reservation queues
- changing product identity/key strategy
- changing throughput behavior under load

### Duplicate request

Idempotency behavior is a contract. Clients rely on repeat submissions being safe.

Versioning risk:

- changing idempotency key rules
- changing duplicate response shape
- changing whether duplicate submissions return original result or special duplicate status
- changing how long idempotency records are retained

## Rollback strategy

## Microservices rollback

Microservices allow targeted rollback, but rollback must be compatible with data and dependent services.

A safe rollback requires:

- old service version can read current database schema
- old service version can call current downstream API versions
- downstream services still accept old request shapes
- idempotency and compensation semantics remain compatible

Rollback is not safe if a migration removed fields or changed data in a way old code cannot read.

## Virtual actors rollback

Virtual actor rollback must consider persisted grain state and in-flight activations.

A safe rollback requires:

- old code can read current grain state
- grain method calls remain compatible
- serialized messages are understood by old code
- activation lifecycle does not leave grains in an incompatible state

Rollback is not safe if new code persisted state in a shape old grain code cannot read.

## Rolling deployment concerns

## Microservices

During a rolling deployment:

- old and new instances may run at the same time
- callers may hit either version
- downstream services may be old or new
- databases must support both versions

This favors backward-compatible changes and staged migrations.

## Virtual actors

During a rolling deployment:

- old and new silos may coexist
- activations may move between silos
- grain calls may cross versions depending on deployment topology
- persisted state may be read by newly activated code

This favors compatible grain interfaces and tolerant state deserialization.

## Contract testing strategy

## Microservices

Recommended tests:

- API contract tests between services
- consumer-driven contract tests for downstream dependencies
- database migration compatibility tests
- idempotency behavior tests
- compensation behavior tests
- timeout behavior tests

The regression tests in this repository protect scenario result semantics, but production service contracts would need additional boundary-level tests.

## Virtual actors

Recommended tests:

- grain interface behavior tests
- Orleans TestCluster tests for grain workflows
- grain state serialization/migration tests
- activation and concurrency tests
- idempotency tests by grain identity
- timeout and compensation policy tests

The actor model can reduce some HTTP contract testing inside the workflow, but it does not remove the need to test interface and state compatibility.

## Release checklist

Before releasing a scenario or workflow change, review:

- Does the request or response shape change?
- Do status values or reason strings change?
- Does idempotency behavior change?
- Does timeout behavior change?
- Does compensation behavior change?
- Does persistent state shape change?
- Can old and new versions run at the same time?
- Can the change be rolled back safely?
- Do regression tests need new expected outcomes?
- Do docs and scenario matrix need updates?

## Key takeaways

- Independent deployment is useful, but compatibility is the cost.
- Microservices make versioning visible at service/API/database boundaries.
- Virtual actors make versioning visible at grain interface/runtime/state boundaries.
- Semantic changes can be breaking even when DTO shapes remain unchanged.
- Rollback safety depends on both code compatibility and state compatibility.
- Scenario behavior should be treated as part of the public architecture contract.

