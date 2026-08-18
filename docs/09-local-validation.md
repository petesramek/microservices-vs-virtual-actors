# Local validation

This checklist verifies that the repository works from a clean local checkout and that the two architecture implementations expose equivalent scenario semantics through the Workbench.

Use it after changes to:

- shared contracts
- scenario workflows or defaults
- persistence
- health or topology definitions
- Aspire composition
- observability
- tests
- project or solution structure

This document covers local repository validation. Detailed scenario expectations are maintained in the [Scenario guide](12-scenario-guide.md), while broader operational interpretation belongs in [Observability and operations](16-observability-and-operations.md).

## Prerequisites

Before validating the repository, confirm that:

- the required .NET SDK is installed
- the repository has been cloned locally
- no stale application processes are using resources required by the Aspire topology
- the working tree contains the changes you intend to validate

Check the installed SDKs with:

```bash
dotnet --list-sdks
```

Do not rely on fixed localhost ports from older documentation. Use the resource endpoints displayed by the Aspire dashboard for the current run.

## Build and test validation

Run the standard validation sequence from the repository root:

```bash
dotnet restore microservices-vs-virtual-actors.slnx
dotnet build microservices-vs-virtual-actors.slnx \
  --configuration Release \
  --no-restore
dotnet test microservices-vs-virtual-actors.slnx \
  --configuration Release \
  --no-build
```

Expected result:

- package restore succeeds
- the complete solution builds in Release configuration
- compiler and analyzer warnings are reviewed rather than hidden by broad suppressions
- all tests pass

The test suite includes focused coverage for:

- the microservices workflow
- the virtual actor workflow and grain persistence
- Workbench Gateway acceptance behavior
- normalized scenario-result regression behavior

## Start the repository with Aspire

Start the supported development topology from the repository root:

```bash
dotnet run --project src/Hosting/Hosting.AppHost/Hosting.AppHost.csproj
```

Open the Aspire dashboard URL printed by the AppHost. Use the dashboard to confirm that the composed resources start and become ready.

The topology should include:

- `Workbench.Ui`
- `Workbench.Gateway`
- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`
- `Ordering.Api`
- `Ordering.Silo`

Expected result:

- all required resources start successfully
- project references and service discovery resolve correctly
- required dependencies reach a ready state
- the Workbench UI endpoint is available from the Aspire dashboard
- no resource enters a repeated failure or restart cycle

If a resource does not start, inspect its console output, structured logs, health state, environment, and dependency references in the Aspire dashboard before changing application code.

## Workbench validation

Open `Workbench.Ui` from its Aspire dashboard endpoint.

Confirm that:

- the application shell and navigation render correctly
- the Scenario page loads at the root route
- the Health page loads
- the Topology page loads
- the Trade-offs page loads
- interactive controls respond without a circuit or browser error

The Workbench pages have distinct responsibilities:

- the Scenario page executes and compares deterministic workflows
- the Health page combines live observations with the shared topology model
- the Topology page explains the intended architecture and is not a live availability view
- the Trade-offs page provides a concise comparison summary

## Scenario validation

Run every supported scenario from the Scenario page:

- successful order
- insufficient inventory
- payment failure compensation
- payment timeout after reservation
- concurrent orders
- duplicate request
- hot product contention

For each scenario, confirm that both implementations return normalized results and that the UI presents them consistently.

Validate the following semantics:

- total request submissions reflect the submitted workload
- unique successful orders represent logical orders rather than successful HTTP responses
- rejected submissions are reported separately
- idempotent duplicate responses are reported separately from unique outcomes
- remaining inventory matches the expected terminal state
- compensation restores inventory when the scenario policy requires it
- inventory never becomes negative
- duplicate submissions reserve inventory at most once for one logical order
- timeline events and reason values match the scenario outcome

Use the [Scenario guide](12-scenario-guide.md) for exact defaults, expected counts, reason values, and interpretation.

## Health validation

Open the Health page after all resources have started.

Confirm that it presents:

- the system health summary
- architecture and service groups
- configured nodes
- required and optional dependencies
- resource availability
- direct and aggregate health
- unknown state when an observation is unavailable

Validate the distinction between:

- `/alive`, which represents process liveness
- `/health`, which represents readiness and can include dependency or persistence checks
- resource availability, which represents whether a configured service endpoint can be reached
- evaluated health, which can include direct node and dependency observations

To validate failure presentation, stop or restart a resource from the Aspire dashboard when safe to do so, refresh the Health page, and confirm that the affected availability and dependency state changes without breaking the page. Restore the resource and confirm that health recovers.

Health is not a proof of business correctness. A ready resource can still return an incorrect scenario result, so health checks and scenario validation must both pass.

## Topology validation

Open the Topology page and confirm that it explains the intended application structure and dependency relationships.

Verify that:

- the displayed services and actor-runtime components match the current repository topology
- the microservices and virtual actor paths are distinguishable
- dependency relationships are understandable
- the page does not present live availability or health as topology facts

Runtime availability and dependency health belong on the Health page.

## Observability validation

Run a scenario and inspect the Aspire dashboard.

### Logs

Confirm that structured logs are available for the relevant path, including:

- `Workbench.Gateway`
- `Orders.Api`, `Inventory.Api`, and `Payments.Api` for the microservices path
- `Ordering.Api` and `Ordering.Silo` for the virtual actor path

Verify that logs contain useful operation context without exposing:

- credentials
- connection strings
- complete request bodies
- idempotency keys
- personal or sensitive data

### Traces

Open the trace view and locate the scenario execution.

Confirm that:

- the gateway activity is present
- downstream HTTP and runtime activities are connected where instrumentation applies
- service and scenario context is useful for following the workflow
- failures and cancellations are represented accurately
- trace collection follows the configured sampling mode

### Metrics

Open the metrics view and confirm that scenario metrics are emitted after completed runs.

Validate that metric dimensions remain bounded and identify the service client and scenario without including unbounded request identifiers.

The Aspire dashboard and Workbench UI are complementary:

- the Aspire dashboard provides detailed resource, log, trace, and metric diagnostics
- the Workbench UI provides normalized scenario results and application-specific health interpretation

## Cancellation and recovery validation

Where practical, validate cancellation and recovery behavior:

- cancel a request or stop a required resource while a scenario is running
- confirm cancellation is not reported as a successful result
- confirm the UI returns to an interactive state
- restart the affected resource through Aspire
- confirm readiness recovers
- rerun the scenario and verify the expected result

Do not treat every interrupted workflow as safely recoverable. The sample demonstrates deterministic scenario behavior, not a complete production reconciliation system.

## Final validation sequence

Use this order for a complete local validation pass:

1. Restore, build, and test the solution in Release configuration.
2. Start the complete topology through `Hosting.AppHost`.
3. Confirm all required Aspire resources become ready.
4. Open the Workbench UI from the Aspire dashboard.
5. Validate the Scenario, Health, Topology, and Trade-offs pages.
6. Run all supported scenarios and compare normalized results.
7. Inspect relevant logs, traces, and metrics in the Aspire dashboard.
8. Validate a safe health failure and recovery when appropriate.
9. Stop the AppHost cleanly.
10. Run the Release build and test sequence again if validation required code or configuration changes.

The goal is to verify both implementations through the same externally visible business semantics while also confirming that the development topology and diagnostics behave consistently.

## Related documentation

- [Problem](01-problem.md)
- [Microservices design](02-microservices-design.md)
- [Virtual actors design](03-virtual-actors-design.md)
- [UI dashboard](10-ui-dashboard.md)
- [End-to-end validation](11-end-to-end-validation.md)
- [Scenario guide](12-scenario-guide.md)
- [Correlation ID logging](13-correlation-id-logging.md)
- [Observability and operations](16-observability-and-operations.md)
- [Known limitations](17-known-limitations.md)
