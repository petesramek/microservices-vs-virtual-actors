### Workbench

Workbench contains the comparison surface for the **Microservices vs Virtual Actors** architecture workbench. It defines the shared scenario contracts, exposes a gateway that runs equivalent workflows against either architecture, and provides an interactive Blazor UI for configuring scenarios and reviewing the results side by side.

This folder does not implement the compared order-processing architectures themselves. The microservices and virtual actor folders own those implementations. Workbench provides the common language, orchestration boundary, and presentation layer used to exercise and compare them.

#### Architecture overview

The Workbench folder is divided into three projects:

```text
Workbench/
  Workbench.Contracts/
  Workbench.Gateway/
  Workbench.Ui/
```

The primary request flow is:

```text
Browser
  -> Workbench.Ui
      -> Workbench.Gateway
          -> Microservices ordering API
          -> Virtual actors ordering API
      <- RunScenarioResponse
  <- side-by-side result cards
```

The UI never calls the compared backends directly. It submits one shared scenario request to the gateway. The gateway selects one or both architecture clients, prepares deterministic backend state, executes the workflow, reads the final inventory state, and returns normalized results through the shared contracts.

#### Projects

##### Workbench.Contracts

Workbench.Contracts defines the request, response, status, and scenario types shared by the UI, gateway, and compared backends.

Its scenario contracts include:

- `ScenarioKind` for the supported deterministic scenarios;
- `RunScenarioRequest` for scenario inputs;
- `RunScenarioResponse` for optional microservices and virtual actor results;
- `ScenarioExecutionResult` for normalized architecture outcomes;
- `ScenarioEvent` for explanatory timeline entries.

Order, inventory, and payment contracts are also shared where the gateway or backend HTTP boundaries require the same payload shape.

Keep this project free of hosting, persistence, UI, and architecture-specific orchestration behavior. Contract changes can affect multiple projects even when the change appears local, so review serialization names, nullability, defaults, enum values, and compatibility before modifying public shapes.

##### Workbench.Gateway

Workbench.Gateway is the HTTP orchestration boundary for the comparison.

It:

- hosts the workbench ASP.NET Core API;
- exposes the scenario-run endpoint;
- accepts an architecture selection through the workbench request header;
- runs the microservices implementation, virtual actor implementation, or both;
- uses typed clients for each architecture boundary;
- prepares scenario-specific inventory and request data;
- normalizes backend responses into `ScenarioExecutionResult` values;
- records scenario traces, metrics, and structured logs;
- propagates correlation and scenario-run metadata;
- maps shared readiness and liveness endpoints.

The primary endpoint is:

```text
POST /api/scenarios/run
```

The gateway also maps:

```text
GET /health
GET /alive
```

`MicroservicesServiceClient` adapts the HTTP-service implementation to the common scenario runner contract. `VirtualActorsServiceClient` performs the equivalent adaptation for the Orleans-backed implementation. Architecture-specific path details belong in these clients rather than in the UI or shared contracts.

See `Workbench.Gateway/README.md` for detailed gateway behavior, configuration, observability, Docker, and endpoint guidance.

##### Workbench.Ui

Workbench.Ui is the interactive Blazor Server comparison experience.

It:

- renders the scenario runner at the root route;
- allows users to select both architectures or one implementation;
- exposes deterministic scenario choices;
- applies scenario-specific defaults;
- validates stock, quantity, identifiers, and concurrency inputs;
- submits requests through `ScenarioRunnerClient` to Workbench.Gateway;
- shows a visual progress sequence while a request is running;
- renders errors, an empty state, and completion metadata;
- presents each architecture result with a shared `ResultCard` component;
- displays totals, completed orders, rejected submissions, idempotent responses, inventory, elapsed time, reasons, and timeline events.

`ScenarioFormModel` owns editable state, validation, default values, and conversion to `RunScenarioRequest`. `ScenarioPage.razor` owns page interaction and presentation state. `ResultCard.razor` owns architecture-result presentation. Keep backend orchestration and result calculation out of the UI when that logic belongs in the gateway or contracts.

#### Supported scenarios

The shared scenario set is:

- **Successful order**: inventory is available and payment succeeds.
- **Insufficient inventory**: the order is rejected before payment authorization.
- **Payment failure compensation**: inventory is reserved, payment fails, and the reservation is released.
- **Payment timeout after reservation**: payment times out after reservation and the demo treats the outcome as failed.
- **Concurrent orders**: multiple orders compete for limited inventory.
- **Duplicate request**: concurrent submissions reuse the same order identity and idempotency key.
- **Hot product contention**: many requests target one product inventory identity.

The gateway is responsible for making these scenarios deterministic enough to compare. The two architecture implementations can use different internal mechanics, but the scenario intent and normalized result meaning must remain aligned.

#### Architecture selection

The UI sends one of these architecture values:

```text
both
microservices
virtual-actors
```

The gateway uses the selection to decide which service clients to invoke. A `RunScenarioResponse` can therefore contain:

- both `Microservices` and `VirtualActors` results;
- only the `Microservices` result;
- only the `VirtualActors` result.

Consumers must continue to handle nullable per-architecture results. Do not infer that both values are always present.

#### Request and result model

`RunScenarioRequest` carries the selected scenario and its inputs, including product, customer, quantity, initial stock, concurrent request count, idempotency key, and payment-failure simulation behavior.

`ScenarioExecutionResult` normalizes architecture output into:

- implementation name;
- final order status;
- optional terminal reason;
- completed and rejected order counts;
- remaining inventory;
- elapsed milliseconds;
- ordered timeline events;
- total request submissions;
- idempotent response count.

The normalized result is a comparison model, not a complete backend diagnostic payload. Architecture-specific internal state should not leak into this contract solely for presentation convenience.

#### Scenario defaults and validation

The UI resets advanced values whenever the selected scenario changes. Defaults intentionally vary by scenario so the expected behavior is easy to observe.

Concurrency applies only to concurrent orders, hot product contention, and duplicate requests. For other scenarios, the form ignores the edited concurrency value and sends the scenario default. The UI model validates concurrency-based scenarios for local demo safety in addition to its data annotation rules.

Keep displayed guidance, form validation, gateway limits, and backend behavior synchronized. A mismatch can make the UI advertise inputs that the gateway or backend handles differently.

#### Progress and result presentation

The UI uses a timed visual progress loop while the gateway request is active. The progress sequence is presentation only. It does not represent server-pushed workflow state and must not be interpreted as confirmation that a listed backend step has completed.

When a run succeeds, the UI records:

- the gateway response;
- the UI-observed request duration;
- the local completion timestamp.

Each non-null architecture result is rendered independently. Mixed completed and rejected counts are presented as partially fulfilled. Known terminal reason codes are converted to user-facing explanations, while unknown reason values remain visible for diagnostics.

#### Idempotency and concurrency

Idempotency is part of the comparison contract, not a UI-only label.

The duplicate-request scenario reuses the same order identity and idempotency key across concurrent submissions. Results distinguish total submissions from unique completed orders and idempotent responses. Preserve this distinction when changing contracts, gateway aggregation, backend clients, or result rendering.

Concurrent scenarios intentionally create competition for shared inventory. The workbench compares observable outcomes, especially over-reservation prevention, rather than claiming that either implementation removes contention for a single hot identity.

#### Error and cancellation behavior

The UI prevents a second form submission while a run is active. It clears the previous result, starts the visual progress loop, and records the exception message if the gateway call fails.

When execution finishes, the UI cancels and awaits the progress task. `OperationCanceledException` from that visual loop is expected and is not displayed as a scenario failure.

The gateway returns validation or unsupported-architecture failures separately from unexpected server failures. Cancellation should continue to propagate through the gateway and architecture clients rather than being converted into a generic internal-server error.

#### Configuration and service discovery

Workbench.Gateway uses configured base addresses for the microservices and virtual actor backends. Workbench.Ui uses its configured gateway address through `ScenarioRunnerClient`.

The preferred complete-workbench startup path is the repository AppHost. It configures project references, service discovery, endpoint relationships, environment variables, health checks, and observability resources together.

When projects are run individually, ensure:

- both compared backends are running when `both` is selected;
- Workbench.Gateway can resolve the selected backend addresses;
- Workbench.Ui can reach Workbench.Gateway;
- configured ports match launch settings or environment configuration.

Do not commit secrets, credentials, private endpoints, or production connection strings to project configuration files.

#### Observability

Workbench.Gateway uses the shared service defaults for logging, metrics, tracing, health reporting, service discovery, resilience, and exporter configuration.

Scenario instrumentation records architecture, scenario, outcome, duration, request counts, and idempotent responses. Trace sampling can use workbench-specific request metadata so comparison traffic remains observable during local runs.

Preserve stable metric names, activity names, tag names, event IDs, and structured logging property names when changing instrumentation. Do not place customer identifiers, idempotency keys, request bodies, credentials, or persisted state in telemetry unless the repository explicitly defines a safe redaction policy.

#### Health model

The shared endpoints distinguish readiness from liveness:

- `/health` evaluates registered dependency checks;
- `/alive` evaluates process liveness.

Gateway dependency checks should reflect whether the gateway is ready to accept new comparison work. Temporary backend unavailability belongs on readiness rather than liveness. Health checks indicate connectivity or reachability only; they do not prove scenario correctness, idempotency safety, compensation success, or equivalent behavior across the two architectures.

#### Local development

The preferred way to run the comparison is through the repository AppHost. After startup, open the Workbench.Ui endpoint, choose an architecture and scenario, and run the workflow.

When starting the Workbench projects individually, start the compared backend services before Workbench.Gateway, then start Workbench.Ui.

Typical project commands are:

```bash
dotnet run --project <path-to-Workbench.Gateway.csproj>
dotnet run --project <path-to-Workbench.Ui.csproj>
```

The contracts project is a class library and is built as a dependency rather than run directly.

#### Validate changes

From the repository root:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Workbench changes should verify at least:

- contract serialization and nullable architecture results;
- successful execution against each architecture independently;
- side-by-side execution with `both`;
- every `ScenarioKind` mapping;
- scenario-specific defaults and form validation;
- insufficient-inventory rejection;
- payment-failure and payment-timeout compensation presentation;
- concurrent order counts and remaining inventory;
- duplicate request totals and idempotent response counts;
- hot-product contention behavior;
- gateway validation and unsupported architecture handling;
- cancellation propagation;
- progress-loop cancellation in the UI;
- error, empty, running, and completed UI states;
- scenario reason and timeline rendering;
- correlation, tracing, metrics, and structured logging;
- readiness and liveness behavior.

#### Adding or changing behavior

When modifying this folder:

- Keep shared transport models in Workbench.Contracts.
- Keep architecture selection and comparison orchestration in Workbench.Gateway.
- Keep architecture-specific HTTP adaptation in the corresponding service client.
- Keep editable state and validation in `ScenarioFormModel`.
- Keep page interaction in `ScenarioPage.razor` and result presentation in focused components.
- Preserve nullable per-architecture results.
- Preserve total submissions, unique outcomes, and idempotent response semantics.
- Keep scenario defaults, validation, UI guidance, and gateway behavior synchronized.
- Propagate cancellation through asynchronous boundaries.
- Treat visual progress as presentation rather than backend workflow state.
- Preserve observability names and avoid sensitive telemetry.
- Update this README and the affected project README when contracts, scenario behavior, configuration, or architecture selection changes.

#### Scope

The Workbench folder is a comparison and demonstration surface. It is not a production API gateway, workflow engine, load-testing system, benchmark harness, administration portal, authentication system, authorization model, or operational control plane.

Displayed elapsed times include implementation and environment effects and are not a controlled performance benchmark. Production use would require independent decisions for security, rate limiting, durable recovery, reconciliation, deployment, scaling, data protection, telemetry governance, and performance methodology.
