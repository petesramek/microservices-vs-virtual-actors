# Workbench.Ui

Workbench.Ui is the interactive presentation layer for the **Microservices vs Virtual Actors** architecture workbench. It provides a Blazor UI for running deterministic scenarios, comparing architecture-specific results, viewing system health, inspecting topology, and reviewing architectural trade-offs.

The UI does not own order, inventory, payment, topology, or health state. It calls Workbench.Gateway for scenario execution and uses dedicated UI services to retrieve and present operational information.

## Repository context

The repository implements the same ordering workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Workbench.Ui provides the human-facing comparison experience. It lets a user execute the same scenario against both architectures, then presents workflow outcomes, inventory results, explanatory timelines, system health, and topology information.

See the repository-level README and docs directory for scenario definitions, architecture discussions, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The project performs eight main tasks:

- Hosts the Blazor web application
- Provides navigation and the shared application layout
- Collects and validates scenario input
- Calls Workbench.Gateway to run selected scenarios
- Presents architecture-specific results side by side
- Displays system health and dependency information
- Displays topology and architectural trade-off information
- Uses shared contracts without exposing backend persistence models

## Startup flow

`Program.cs` performs application composition:

- creates the `WebApplicationBuilder`
- applies shared service defaults
- registers Razor components and interactive server rendering
- registers the scenario HTTP client
- registers system-health services and configuration
- registers topology services and configuration
- builds the web application
- configures the request pipeline
- maps static assets and Razor components
- maps shared readiness and liveness endpoints when supplied by the service defaults
- runs until shutdown

Keep `Program.cs` focused on composition. Scenario transport belongs in `ScenarioRunnerClient`, health retrieval belongs in `SystemHealthService`, topology construction belongs in `TopologyDefinitionProvider`, and UI behavior belongs in focused components.

## Application composition

`Components/App.razor` defines the application document and root component composition.

`Components/Routes.razor` owns route resolution and layout selection.

`Components/_Imports.razor` centralizes component namespaces and common framework imports. Update it when components move between feature folders so pages do not accumulate repeated `@using` directives.

`Components/Layout/MainLayout.razor` provides the shared page shell and navigation.

## Scenario runner

`ScenarioPage.razor` is the interactive scenario workbench at:

```http
GET /
```

The page uses interactive server rendering and calls `ScenarioRunnerClient` when the form is submitted.

The user can select:

```text
both
microservices
virtual-actors
```

The selected value is passed to Workbench.Gateway and determines which architecture implementation is executed. `both` produces the side-by-side comparison experience.

The page supports these shared scenario kinds:

- successful order
- insufficient inventory
- payment failure compensation
- payment timeout after reservation
- concurrent orders
- duplicate request
- hot product contention

Scenario names shown by the UI are presentation labels for the shared `ScenarioKind` values.

## Scenario form

`ScenarioFormModel` stores editable UI state for the scenario form. It is a presentation model, not a shared transport contract or persistence entity.

The form includes:

- scenario selection
- initial stock
- requested quantity
- concurrent request count
- product identifier
- customer identifier
- idempotency key

Advanced settings are hidden by default. Changing the selected scenario resets advanced values to that scenario's defaults.

Fields that do not apply to the selected scenario are disabled or ignored. Concurrent-request guidance differs for concurrent-order, hot-product-contention, and duplicate-request scenarios.

Validation is performed through Blazor form validation and data annotations on the form model. Keep validation messages synchronized with the limits accepted by Workbench.Gateway and the shared contracts.

## Scenario execution state

The page prevents a second submission while a scenario is running.

At the start of a run, it:

- clears the previous response and error
- creates architecture-specific progress labels
- starts a visual progress loop
- starts a UI stopwatch
- calls `ScenarioRunnerClient`
- keeps the running state visible for a minimum display interval

On success, it stores the shared `RunScenarioResponse`, request duration, and completion time. On failure, it displays the exception message returned to the UI. The progress loop is canceled and awaited in the completion path.

The progress steps are illustrative UI state. They are not backend workflow events and must not be presented as distributed trace evidence.

## Scenario results

When no scenario has run, the page presents an empty state explaining how to begin.

While a request is active, it presents a progress card.

After completion, it presents:

- a scenario-specific summary
- a scenario description
- the local completion time
- the UI-observed request duration
- one result card for each executed architecture

`ResultCard.razor` renders an architecture-specific `ScenarioExecutionResult`. It should remain independent of the scenario input form and should receive all display data through component parameters.

The UI request duration includes client-side and gateway-call overhead. It is useful for workbench feedback but is not a controlled performance benchmark.

## Gateway client

`Internal/Clients/ScenarioRunnerClient.cs` owns HTTP communication with Workbench.Gateway.

Its responsibilities should remain limited to:

- constructing the scenario request
- serializing shared request contracts
- deserializing `RunScenarioResponse`
- propagating cancellation when supported by the caller
- detecting unsuccessful or invalid responses

Razor components should not construct gateway URLs, create raw `HttpRequestMessage` instances, or duplicate JSON response handling.

Environment-specific gateway addresses should come from standard ASP.NET Core configuration or service discovery. Do not hard-code production endpoints in components or stylesheets.

## Health presentation

The health feature presents system, group, node, and dependency state using topology definitions and health snapshots.

Current health components include:

```text
AvailabilityBadge.razor
HealthMessageCard.razor
HealthStatusBadge.razor
HealthSummary.razor
GroupHealthCard.razor
NodeHealthCard.razor
SystemHealthDependency.razor
```

`HealthStatusBadge` presents health values such as healthy, degraded, unhealthy, or unknown.

`AvailabilityBadge` should remain separate only when it represents availability semantics that differ from health status. If both components render the same value domain, consolidate them to avoid duplicate presentation logic.

`HealthMessageCard` presents health-related explanatory or failure messages.

`HealthSummary` presents aggregate system state.

## Group health cards

`GroupHealthCard` renders one topology group and its ordered nodes.

It receives:

- a `TopologyGroupDefinition`
- an optional `TopologyGroupSnapshot`
- node definitions indexed by stable node identifier
- optional node snapshots indexed by stable node identifier
- outgoing edge definitions grouped by source-node identifier
- edge snapshots indexed by source and target identifiers

When no group snapshot exists, the component displays `HealthStatus.Unknown`.

When a group references a node that is absent from the topology definition, the component renders an explicit unknown-resource card instead of silently omitting it.

The group section is associated with its heading through `aria-labelledby`. Its generated heading identifier is sanitized for HTML use.

## Node and dependency health

`NodeHealthCard` renders a topology node, its current snapshot, and its outgoing dependencies.

Dependency presentation should use the edge definition and edge snapshot together so the UI can distinguish configured dependency intent from observed dependency state.

Missing snapshots are expected operational states and should be shown as unknown rather than treated as component failures.

Component names should follow this pattern:

```text
<Domain concept><Visual role>
```

Examples include `GroupHealthCard`, `NodeHealthCard`, and `HealthStatusBadge`. Rename `SystemHealthDependency` when its actual visual responsibility is confirmed, for example to `DependencyHealthRow` or `DependencyHealthCard`.

## Health service

`SystemHealthService` retrieves and prepares health information for the UI.

`HealthEndpointOptions` configures the endpoint or endpoints used by the service.

`SystemHealthServiceCollectionExtensions` registers the health feature and its configuration.

Keep transport and snapshot preparation out of Razor components. Components should render supplied state and dispatch user actions, not own long-lived HTTP polling or configuration binding.

If the health feature refreshes periodically, cancellation and disposal must be tied to the component lifecycle so navigation does not leave background refresh work running.

## Topology presentation

`Topology.razor` presents the configured architecture topology.

`TopologyDefinitionProvider` prepares the topology definition consumed by the UI.

`TopologyOptions` contains topology-specific configuration.

Topology definitions describe configured relationships. Topology snapshots and health snapshots describe observed state. Keep these concepts distinct in naming, rendering, and explanatory text.

Do not infer security, transactional, or runtime guarantees solely from a visual topology edge.

## Trade-offs page

`Tradeoffs.razor` presents the architectural comparison and explanatory material for the workbench.

Keep trade-off statements aligned with the demonstrated scenario and repository documentation. Avoid presenting workbench observations as universal performance or reliability conclusions.

## Accessibility

Use semantic HTML and accessible names throughout the UI.

Current patterns to preserve include:

- `fieldset` for disabling related controls during execution
- `aria-expanded` on the advanced-settings toggle
- `aria-label` on progress steps
- `aria-labelledby` between health cards and headings
- `aria-hidden` on decorative progress symbols
- explicit status content for missing topology resources

Every form label should be programmatically associated with its input through `for` and `id`, or by wrapping the input. Visual proximity alone is not sufficient.

Dynamic error, completion, and health updates should use an appropriate live-region strategy when they need to be announced to assistive technology. Avoid adding ARIA roles when native semantic elements already provide the correct behavior.

## Error handling

The scenario page distinguishes four UI states:

- ready, before the first run
- running
- failed
- completed

Do not expose stack traces, credentials, internal endpoint secrets, or raw backend response bodies in user-visible errors.

The current page displays `Exception.Message`. As the implementation is hardened, prefer a user-safe message while logging the detailed exception through structured application logging.

Cancellation should be handled separately from unexpected failures. A user navigation or disconnected interactive circuit should not be presented as a backend scenario failure.

## Styling

`wwwroot/app.css` contains global application styles.

`wwwroot/health.css` contains health-specific styles.

Global styles should be limited to:

- design tokens
- typography
- layout shell
- shared controls
- reusable utility classes

Prefer scoped component CSS for feature-specific markup when selectors are owned by one Razor component:

```text
GroupHealthCard.razor
GroupHealthCard.razor.css
```

Migrate selectors only after confirming their consumers. Removing a global selector before every dependent component is updated can cause silent visual regressions.

## Configuration

`appsettings.json` contains UI configuration consumed through ASP.NET Core configuration providers.

`Properties/launchSettings.json` contains local launch profiles.

Configuration may include:

- gateway service identity or base address
- health endpoint settings
- topology settings
- shared service-default settings

Environment-specific values should be supplied through standard configuration providers. Do not commit credentials, access tokens, private endpoints, or other secrets.

## Docker

The project includes a `Dockerfile` for container builds. Keep it aligned with the target framework, repository build layout, project references, static assets, exposed ports, and runtime user.

Container-specific ports, filesystem permissions, user configuration, health checks, and image-stage details should be reviewed directly in the `Dockerfile`, those details are not duplicated here.

A gateway address configured as `localhost` inside the UI container points to the UI container itself, not to a separate gateway container.

## Local development

The preferred way to run the complete workbench is through the repository AppHost so service discovery, endpoints, observability, and environment variables are configured consistently.

From the Workbench.Ui project directory:

```console
dotnet run
```

From the repository root:

```console
dotnet run --project <path-to-Workbench.Ui.csproj>
```

Local URLs are defined by `Properties/launchSettings.json` or runtime configuration.

Scenario execution requires Workbench.Gateway and the selected backend architecture services. Health and topology views require their configured data sources.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

UI changes should cover at least:

- application startup and root routing
- interactive server rendering
- default scenario selections
- scenario changes resetting advanced defaults
- advanced-settings visibility
- form validation
- disabled controls during execution
- duplicate-submit prevention
- successful gateway responses
- unsuccessful and bodyless gateway responses
- cancellation and component disposal
- minimum progress-state display
- progress-step advancement and completion
- microservices-only results
- virtual-actors-only results
- side-by-side results
- empty, running, error, and completed states
- nullable health snapshots
- missing topology node definitions
- groups with no configured nodes
- unknown health fallbacks
- accessible labels, names, headings, and live updates
- scoped and global CSS ownership
- configuration validation
- readiness and liveness endpoints where mapped

## Adding or changing UI behavior

When modifying this project:

- Keep `Program.cs` focused on application composition
- Keep gateway transport in `ScenarioRunnerClient`
- Keep health transport and preparation in `SystemHealthService`
- Keep topology preparation in `TopologyDefinitionProvider`
- Keep persistence entities out of the UI project
- Use shared Workbench.Contracts types at process boundaries
- Keep form state in UI-specific models
- Preserve cancellation through asynchronous UI services
- Separate cancellation from unexpected failures
- Avoid displaying raw internal exception details
- Use semantic HTML before adding ARIA
- Associate every label with its input
- Prefer focused components over oversized page-level code blocks
- Prefer scoped CSS for selectors owned by one component
- Update `_Imports.razor`, namespaces, component references, and CSS together when moving files
- Update this README when routes, feature structure, service boundaries, or configuration change

## Naming conventions

- Routable components use the `Page` suffix
- Visual containers use the `Card` suffix
- Compact status indicators use the `Badge` suffix
- HTTP boundary types use the `Client` suffix
- UI data-retrieval and preparation types use the `Service` or `Provider` suffix according to responsibility
- Configuration binding types use the `Options` suffix
- Form-state types use the `FormModel` suffix
- Async operations use the `Async` suffix
- Component names follow `<Domain concept><Visual role>`
- Component parameters use PascalCase
- Private component state uses camelCase until the project applies a different repository-wide field convention

## Scope

Workbench.Ui demonstrates a human-facing architecture comparison experience. It is not a production administration portal, benchmark dashboard, authentication boundary, authorization model, monitoring platform, incident-management console, or accessibility certification.

Scenario timing is useful for workbench feedback but is not a controlled benchmark. Production use would require independent decisions for authentication, authorization, user-safe error handling, localization, accessibility testing, browser support, telemetry, caching, deployment, scaling, content security, and recovery.
