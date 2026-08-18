# Release, versioning, and rollback

Microservices and virtual actor systems expose different release boundaries, but both require deliberate compatibility, migration, and rollback strategies.

Microservices commonly expose versioning pressure through network contracts, independently deployed services, service-owned data, and rollout ordering. Virtual actor systems commonly expose it through actor interfaces, serialized messages, runtime compatibility, activation behavior, and persistent actor state.

Neither architecture removes versioning risk. Each moves that risk to different contracts and state boundaries.

## Summary

A release can be breaking even when method signatures and serialized payload shapes remain technically compatible. Changes to business meaning, idempotency, failure policy, state ownership, timeout handling, or terminal outcomes can also break callers and operators.

Safe evolution requires teams to understand:

- which versions may run at the same time
- which contracts cross deployment boundaries
- which old and new data shapes must coexist
- which semantic behaviors consumers rely on
- whether rollback remains possible after durable state changes
- how incomplete work is recovered or reconciled

The objective is not to avoid change. It is to make change compatible, observable, and recoverable across a defined release window.

## Versioning boundaries

### Network and message contracts

Microservices expose contracts through HTTP APIs, asynchronous messages, events, and shared integration schemas.

Potentially breaking changes include:

- removing or renaming fields
- changing field types or requiredness
- changing enum values
- changing status codes or error categories
- changing event meaning or ordering assumptions
- changing retry or idempotency behavior
- changing whether an operation is synchronous, asynchronous, terminal, or pending

Additive changes are usually safer when consumers tolerate unknown fields and optional values. They are not automatically safe. A new required behavior, changed default, or different semantic interpretation can still break consumers.

Versioning approaches may include:

- backward-compatible additive evolution
- tolerant readers
- explicit API or message versions
- compatibility adapters
- dual publishing during migration
- consumer-driven contract testing
- coordinated rollout when compatibility cannot be maintained

### Actor interfaces and calls

Virtual actor systems expose logical contracts through actor interfaces and serialized calls.

Potentially breaking changes include:

- removing or renaming methods
- changing method parameters or return types
- changing serialized data-transfer objects
- changing actor key strategy
- changing reentrancy or scheduling assumptions
- changing timeout, retry, or idempotency semantics
- changing which actor identity owns an operation

Adding a new actor method is generally safer than changing an existing one, but runtime and deployment compatibility still matter. During rolling upgrades, old and new silos may coexist, calls may cross versions, and activations may move between runtime nodes.

### Persistent data

Persisted data is part of the release contract.

Microservices usually persist state in service-owned databases. Virtual actor systems persist actor state through runtime storage providers. In both cases, new code may read old data, and rolled-back code may need to read data written by the newer version.

Potentially breaking changes include:

- removing or renaming fields or columns
- changing data types
- changing serialization identifiers
- changing field meaning without migration
- changing key or partition strategy
- introducing an invariant that existing data does not satisfy
- writing data that earlier code cannot interpret

### Semantic behavior

Behavior is also a contract.

Examples include:

- whether insufficient inventory is a business rejection or a technical failure
- whether a payment timeout is rejected, pending, retried, or reconciled later
- whether duplicate requests return the original result
- whether compensation completes before a rejection is returned
- whether partial completion is visible to callers
- whether reason values remain stable and machine-readable

A release that changes these meanings can be breaking even when every API and persisted-state shape remains unchanged.

## Safe evolution patterns

### Expand and contract

A common safe pattern is expand and contract:

1. Add the new contract or data shape without removing the old one.
2. Deploy code that can read both old and new forms.
3. Migrate producers and stored data gradually.
4. Confirm that old readers are no longer active.
5. Remove the old form in a later release.

This pattern can apply to API fields, message schemas, database columns, actor state, and state-ownership transitions.

### Microservices database evolution

A safer service-owned database migration can follow this sequence:

1. Add nullable columns, new tables, or compatible indexes.
2. Deploy code that tolerates both schemas.
3. Write both forms temporarily when necessary.
4. Backfill existing data.
5. Switch reads to the new form.
6. Remove the old schema only after the rollback window closes.

Risky changes include destructive migration before old instances have stopped, changing data meaning without a compatibility layer, and coupling several services to one coordinated database release.

### Actor-state evolution

A safer actor-state evolution can follow this sequence:

1. Add optional state fields with safe defaults.
2. Keep deserialization tolerant of older state.
3. Populate new values lazily or through an explicit migration process.
4. Preserve the meaning of existing fields during the compatibility window.
5. Validate that older code can still read state if rollback remains required.
6. Retire compatibility logic only after old versions and old state are no longer supported.

Risky changes include changing serialized identifiers, replacing state types without migration, or activating old state with new code that cannot interpret it.

### Ownership transitions

Moving responsibility between services or actor identities is more complex than changing a field.

A safe ownership transition may require:

- dual reads or writes
- data backfill
- consistency checks
- traffic shadowing
- cutover markers
- reconciliation
- a temporary compatibility boundary
- a plan for incomplete work during cutover

The release plan must define when the new owner becomes authoritative and how conflicting state is resolved.

## Rolling deployments

### Microservices

During a rolling microservices deployment:

- old and new instances of one service may coexist
- callers may reach either version
- downstream services may be upgraded in a different order
- messages can outlive the code version that produced them
- databases must support every active version
- retries may cross the deployment boundary

This favors backward-compatible contracts, staged database changes, version-tolerant consumers, and a clearly defined deployment order.

Independent deployment is useful only when compatibility allows services to change independently. Separate processes alone do not provide release independence.

### Virtual actors

During a rolling virtual actor deployment:

- old and new silos may coexist
- activations may be distributed across versions
- actor calls may cross runtime versions
- newly activated code may read state written by older code
- old code may be reactivated after new state has been written
- runtime and serializer compatibility may constrain upgrade order

This favors compatible actor interfaces, tolerant state deserialization, tested mixed-version behavior, and deliberate activation and state-migration strategies.

The runtime can move and reactivate actors, but it cannot make incompatible interfaces or state representations safe automatically.

## Rollback

Rollback is a compatibility operation, not simply a deployment action.

A rollback is safe only when the earlier version can operate against the current contracts, infrastructure, and durable state.

### Microservices rollback

A safe microservices rollback requires that:

- the earlier service version can read the current database schema
- current dependencies still accept the earlier request shape
- current callers can interpret the earlier response shape
- messages produced by the newer version remain understandable
- idempotency and compensation semantics remain compatible
- the earlier and newer versions can coexist during the rollback window

Rollback is unsafe when a release has removed required data, changed durable meaning, or emitted contracts that the earlier version cannot process.

### Virtual actor rollback

A safe virtual actor rollback requires that:

- the earlier code can deserialize current actor state
- actor interface calls remain compatible
- serialized messages remain understandable
- actor key and ownership strategies are unchanged or backward compatible
- runtime versions can coexist as required
- activations do not resume from a state the earlier workflow cannot handle

Rollback is unsafe when newer code has persisted an incompatible state shape or advanced a workflow into a state that older code does not understand.

### Roll forward instead of rollback

When durable state or contracts are no longer backward compatible, a corrected forward release may be safer than rollback.

A roll-forward plan should include:

- a minimal corrective change
- explicit migration or repair logic
- validation of affected durable state
- compatibility with partially upgraded components
- monitoring for recurrence
- a reconciliation plan for incomplete operations

Teams should decide before release which failures permit rollback and which require roll forward.

## Failure recovery and in-flight work

Deployment and rollback plans must consider work that is already running.

For microservices, in-flight work may exist in:

- active HTTP requests
- message queues
- workflow or saga state
- database transactions
- retry policies
- scheduled or background jobs

For virtual actors, in-flight work may exist in:

- active actor calls
- persisted workflow state
- reminders and timers
- pending external operations
- activated actors during silo shutdown
- messages routed across a mixed-version cluster

A safe release process defines how new work is drained, how incomplete work resumes, and how ambiguous outcomes are detected and reconciled.

## Testing release compatibility

### Microservices

Useful release-focused tests include:

- producer and consumer contract tests
- backward-compatible API tests
- message-schema compatibility tests
- database migration and rollback tests
- mixed-version integration tests
- idempotency and duplicate-delivery tests
- failure and compensation tests
- scenario regression tests

### Virtual actors

Useful release-focused tests include:

- actor-interface compatibility tests
- mixed-version cluster tests
- actor-state serialization and migration tests
- activation from older persisted state
- rollback reads after newer-state writes
- idempotency tests by actor identity
- failure and compensation tests
- scenario regression tests

No single test layer is sufficient. Contract tests protect boundaries, migration tests protect state, and scenario tests protect externally visible business meaning.

## Release checklist

Before releasing a contract, workflow, or state change, review the following areas.

### Contracts

- Does a request, response, actor interface, message, or event change?
- Are fields removed, renamed, retyped, or made required?
- Do status, reason, or error semantics change?
- Can old and new producers and consumers coexist?

### State

- Does a database or actor-state shape change?
- Can new code read old state?
- Can old code read state written by new code?
- Is a migration, backfill, or reconciliation step required?
- Does ownership or partitioning change?

### Behavior

- Does idempotency behavior change?
- Does timeout, retry, or compensation behavior change?
- Does terminal workflow meaning change?
- Could in-flight work be interpreted differently across versions?

### Deployment

- Can old and new versions run at the same time?
- Is deployment order constrained?
- Can traffic be drained safely?
- Are runtime, serializer, and infrastructure versions compatible?
- Is rollback safe after the release writes durable state?
- If rollback is unsafe, is a roll-forward plan ready?

### Validation and communication

- Are compatibility, migration, and mixed-version tests present?
- Are scenario regression expectations updated?
- Are observability signals sufficient to detect release failures?
- Are operators and dependent teams aware of the compatibility window?
- Are documentation and operational guidance updated?

## How this repository illustrates release contracts

The repository uses normalized scenario behavior as a practical compatibility surface.

Examples include:

- `SuccessfulOrder` remains a completed logical order with the expected inventory change
- `InsufficientInventory` remains a business rejection rather than a generic technical error
- payment failure and timeout scenarios retain their documented compensation behavior
- concurrent scenarios preserve the inventory invariant
- duplicate submissions preserve one logical result and one inventory reservation
- reason values, counts, and remaining inventory retain stable meaning across both implementations

The microservices implementation illustrates compatibility across HTTP contracts and service-owned SQLite state. The virtual actor implementation illustrates compatibility across actor interfaces, Orleans runtime boundaries, and persisted actor state.

The repository's regression tests protect scenario semantics, but they do not replace production contract testing, migration rehearsal, mixed-version deployment testing, or recovery validation.

See [Scenario guide](12-scenario-guide.md) and [End-to-end validation](11-end-to-end-validation.md) for repository-specific expectations.

## Key takeaways

- Independent deployment creates compatibility obligations
- Microservices emphasize network contracts, service versions, and service-owned data
- Virtual actors emphasize actor interfaces, runtime compatibility, activation, and persistent actor state
- Semantic behavior can be breaking even when technical shapes remain compatible
- Rollback is safe only when earlier code can operate against current contracts and durable state
- Some releases require roll forward because durable changes cannot be reversed safely
- Mixed-version behavior, in-flight work, and recovery must be considered before deployment
- Scenario behavior can serve as a useful compatibility contract, but production release safety requires additional boundary and migration testing

## Related documentation

- [Microservices design](02-microservices-design.md)
- [Virtual actors design](03-virtual-actors-design.md)
- [Deployment comparison](05-deployment-comparison.md)
- [Trade-offs](07-tradeoffs.md)
- [Scenario guide](12-scenario-guide.md)
- [End-to-end validation](11-end-to-end-validation.md)
- [Maintenance and evolution](15-maintenance-and-evolution.md)
- [Observability and operations](16-observability-and-operations.md)
- [Known limitations](17-known-limitations.md)
- [Out of scope](18-out-of-scope.md)
