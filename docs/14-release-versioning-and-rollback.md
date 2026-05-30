# Release, versioning, and rollback

This document focuses on release compatibility, versioning, rollback, and safe evolution of the architecture comparison sample.

Deployment topology is covered separately in `[05-deployment-comparison.md](05-deployment-comparison.md)`. The important release point here is that each topology creates different compatibility boundaries. Microservices expose compatibility pressure at HTTP/API and database boundaries. Virtual actors expose compatibility pressure at grain interface, grain state, serialization, runtime, and activation boundaries.

Neither style removes the need for versioning. Each style moves versioning concerns to different places.

## Summary

Both architecture styles need release discipline.

Microservices typically expose release and versioning pressure at:

- HTTP/API boundaries
- service-to-service contracts
- service-owned database schemas
- deployment ordering
- rolling deployment compatibility windows

Virtual actors typically expose release and versioning pressure at:

- grain interface boundaries
- serialized grain method arguments and return values
- persistent grain state schemas
- Orleans runtime and silo deployment behavior
- activation lifecycle and placement behavior

The most important lesson is that semantic behavior is part of the contract. A change can be breaking even when method signatures or JSON shapes remain technically compatible.

## Versioning boundaries

### HTTP/API contracts

Microservices expose explicit HTTP contracts.

Common contract changes include:

- adding fields
- removing fields
- renaming fields
- changing enum values
- changing response status codes
- changing error or reason semantics
- changing idempotency behavior

Additive changes are usually safer. Breaking changes require versioning, compatibility shims, or coordinated deployment.

Examples:

- Adding a new response field is usually additive if old clients ignore unknown fields.
- Renaming `RejectedOrders` to `RejectedSubmissions` is breaking if clients deserialize by property name.
- Changing `PaymentTimeout` from a rejected outcome to a pending outcome is a semantic breaking change even if the JSON shape remains the same.

### Grain interfaces

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

### Persistent state schemas

Both styles persist state.

Microservices usually persist state in service-owned databases. Virtual actors persist state as grain state. In both cases, persisted state must remain compatible across deployment and rollback windows.

## Safe evolution patterns

### Microservices database evolution

Each service may own one or more database schemas. Database migrations should support safe rollout.

A safer migration pattern is:

1. Add nullable columns or new tables.
2. Deploy code that tolerates both old and new shapes.
3. Optionally write both old and new shapes during a transition period.
4. Backfill data.
5. Deploy code that reads the new shape.
6. Remove old schema only after old code is no longer running.

A risky migration pattern is:

1. Rename or remove a column.
2. Deploy while old code still expects the old column.
3. Fail during rolling deployment because old and new versions cannot both run.

### Virtual actor grain state evolution

Grain state is also schema.

Persisted actor state must be compatible across code versions. New code may activate grains whose state was written by older code. Rolled-back code may need to read state written by newer code.

A safer grain-state evolution pattern is:

1. Add optional fields to grain state.
2. Make new code tolerate missing fields.
3. Populate new fields lazily or through a migration process.
4. Avoid changing the meaning of existing fields without a migration strategy.
5. Keep old code compatibility in mind until rollback is no longer required.

A risky grain-state evolution pattern is:

1. Change serialized field names or types without compatibility support.
2. Activate old persisted grains with new code that cannot read their state.
3. Fail during activation or workflow execution.

## Scenario behavior as contract

Scenario behavior should be treated as part of the public architecture contract for this repository.

The UI, tests, documentation, and users rely on the meaning of statuses, reason strings, counts, idempotency behavior, and inventory outcomes.

### Successful order

The happy path is the most widely consumed behavior. Any change to the successful order response can affect clients, tests, dashboards, and documentation.

Versioning risks include:

- changing `Completed` or `Fulfilled` semantics
- changing inventory decrement behavior
- changing payment authorization response shape
- changing the result fields shown by the dashboard

### Insufficient inventory

`InsufficientInventory` is a business outcome, not a technical error.

Versioning risks include:

- renaming the reason
- returning a generic error instead of a business rejection
- attempting payment after inventory rejection
- changing whether remaining inventory is included in the result

### Payment failure with compensation

Compensation semantics are behavior contracts. A client or operator may assume that inventory is released after payment failure.

Versioning risks include:

- changing release timing
- changing final order state
- changing whether payment failure is retryable
- changing how compensation appears in the timeline

### Payment timeout after reservation

Timeout behavior is sensitive because timeout is ambiguous in production systems.

The current sample policy is:

- timeout is treated as failed
- inventory is released
- order is rejected with `PaymentTimeout`

Versioning risks include:

- changing timeout from rejected to pending
- holding inventory after timeout
- adding retry or reconciliation behavior
- changing final reason semantics

These changes are semantic contract changes even if request and response DTOs remain unchanged.

### Concurrent orders

Concurrent outcomes depend on reservation strategy.

Versioning risks include:

- changing from immediate rejection to queueing
- changing retry behavior
- changing reservation consistency strategy
- changing status or reason mapping for partial fulfillment
- changing aggregate result wording

### Hot product contention

Optimizing hot product handling can require topology or identity changes.

Versioning risks include:

- sharding inventory ownership
- introducing reservation queues
- changing product identity or key strategy
- changing throughput behavior under load
- changing how partial fulfillment is reported

### Duplicate request

Idempotency behavior is a contract. Clients rely on repeat submissions being safe.

Versioning risks include:

- changing idempotency key rules
- changing duplicate response shape
- changing whether duplicate submissions return the original result or a special duplicate status
- changing how long idempotency records are retained
- changing behavior under concurrent duplicate submissions

## Rollback strategy

### Microservices rollback

Microservices allow targeted rollback, but rollback must be compatible with data and dependent services.

A safe rollback requires:

- old service version can read the current database schema
- old service version can call current downstream API versions
- downstream services still accept old request shapes
- idempotency and compensation semantics remain compatible
- old and new versions can coexist during the rollback window

Rollback is not safe if a migration removed fields or changed data in a way old code cannot read.

### Virtual actors rollback

Virtual actor rollback must consider persisted grain state and in-flight activations.

A safe rollback requires:

- old code can read current grain state
- grain method calls remain compatible
- serialized messages are understood by old code
- activation lifecycle does not leave grains in an incompatible state
- old and new silos can coexist safely during the rollback window, if rolling deployment is used

Rollback is not safe if new code persisted state in a shape old grain code cannot read.

## Rolling deployment concerns

### Microservices

During a rolling deployment:

- old and new instances may run at the same time
- callers may hit either version
- downstream services may be old or new
- databases must support both versions
- service-to-service contracts must remain compatible across the deployment window

This favors backward-compatible API changes and staged database migrations.

### Virtual actors

During a rolling deployment:

- old and new silos may coexist
- activations may move between silos
- grain calls may cross versions depending on deployment topology
- persisted state may be read by newly activated code
- runtime and serialization compatibility matter

This favors compatible grain interfaces, tolerant state deserialization, and careful rollout of state changes.

## Contract testing strategy

### Microservices

Recommended tests include:

- API contract tests between services
- consumer-driven contract tests for downstream dependencies
- database migration compatibility tests
- idempotency behavior tests
- compensation behavior tests
- timeout behavior tests
- scenario regression tests through the comparison layer

The regression tests in this repository protect scenario result semantics, but production service contracts would need additional boundary-level tests.

### Virtual actors

Recommended tests include:

- grain interface behavior tests
- Orleans test-cluster workflow tests
- grain state serialization and migration tests
- activation and concurrency tests
- idempotency tests by grain identity
- timeout and compensation policy tests
- scenario regression tests through the comparison layer

The actor model can reduce some HTTP contract testing inside the workflow, but it does not remove the need to test interface and state compatibility.

## Release checklist

Before releasing a scenario or workflow change, review:

- Does the request shape change?
- Does the response shape change?
- Do status values or reason strings change?
- Does idempotency behavior change?
- Does timeout behavior change?
- Does compensation behavior change?
- Does persistent state shape change?
- Can old and new versions run at the same time?
- Can the change be rolled back safely?
- Do regression tests need new expected outcomes?
- Do UI labels or result-card terminology need updates?
- Do documentation and the scenario guide need updates?

## Key takeaways

- Independent deployment is useful, but compatibility is the cost.
- Microservices make versioning visible at service, API, and database boundaries.
- Virtual actors make versioning visible at grain interface, runtime, serialization, and state boundaries.
- Semantic changes can be breaking even when DTO shapes remain unchanged.
- Rollback safety depends on both code compatibility and state compatibility.
- Scenario behavior should be treated as part of the public architecture contract.
