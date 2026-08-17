# Contributing

Thank you for your interest in improving **Microservices vs Virtual Actors**.

This repository is an architecture workbench that compares the same order workflow implemented with explicit microservice boundaries and virtual actors. Contributions should help readers understand the trade-offs between the approaches rather than attempt to prove that one architecture is universally better.

## Before you start

Choose the appropriate GitHub channel:

- Use a **bug report** for reproducible incorrect behavior.
- Use a **feature request** for a concrete improvement, new scenario, or new comparison.
- Use **GitHub Discussions** for questions, early ideas, observations, and open-ended architecture conversations.

Search existing issues and discussions before creating a new one.

For larger changes, open an issue or discussion before implementation so the scope and intended outcome can be agreed first.

## Development environment

The supported development experience uses the .NET Aspire AppHost.

From the repository root, run:

```bash
dotnet run --project src/Hosting/Hosting.AppHost/Hosting.AppHost.csproj
```

Use the Aspire dashboard to inspect:

- application resources and dependencies
- resource lifecycle and endpoints
- structured logs
- distributed traces
- metrics

Use Workbench.Ui to run comparison scenarios and inspect the curated scenario, health, topology, and trade-off views.

## Repository areas

```text
src/
  Hosting/         Aspire composition and shared service defaults
  Microservices/   HTTP service implementation
  Observability/   Shared health and topology models
  VirtualActors/   Orleans-based implementation
  Workbench/       Shared contracts, gateway, and Blazor UI
tests/             Workflow, persistence, acceptance, and regression tests
docs/              Architecture and validation documentation
```

Read the nearest folder or project README before changing an unfamiliar area.

## Making changes

Keep contributions focused and proportional to the problem.

- Avoid unrelated formatting or refactoring.
- Prefer the minimum useful abstraction.
- Preserve the observable meaning of shared scenarios and results.
- Keep both architecture implementations aligned when changing comparison semantics.
- Propagate cancellation through asynchronous operations.
- Do not expose credentials, connection strings, personal data, or sensitive configuration in code, logs, traces, screenshots, tests, or issues.
- Do not commit generated output, local databases, SQLite WAL or SHM files, IDE user files, or Aspire runtime state.

### Shared contracts

Changes under `src/Workbench/Workbench.Contracts` can affect both implementations, the gateway, the UI, acceptance tests, regression tests, and documentation.

When changing a shared contract:

- preserve serialization compatibility unless a deliberate breaking change is agreed;
- review nullability and default values
- update every affected producer and consumer
- update tests and documentation in the same pull request

### Scenario behavior

When adding or changing a scenario, keep these areas synchronized:

- `ScenarioKind` and shared scenario contracts
- Workbench.Gateway runner selection and execution
- both architecture implementations
- Workbench.Ui form defaults, guidance, and result presentation
- acceptance and scenario regression tests
- `docs/12-scenario-guide.md`

### Health and topology

Keep these concepts distinct:

- the **Topology page** explains the intended architecture
- the **Health page** combines live observations with topology definitions
- the **Aspire dashboard** provides detailed development diagnostics for resources, logs, traces, and metrics

Changes to AppHost resources, health groups, topology definitions, endpoint configuration, or UI presentation must remain aligned.

## Code style

Follow the repository build configuration and analyzer rules.

General conventions include:

- file-scoped namespaces
- nullable reference types enabled
- explicit types where they improve clarity
- cancellation tokens for asynchronous I/O
- structured logging with stable message templates
- XML documentation for declared source types and members
- comments that explain decisions rather than narrate statements

Fix relevant compiler and analyzer warnings instead of suppressing them globally. Use a narrow suppression with a clear justification only when a rule does not apply to the framework or runtime behavior involved.

## Tests and validation

Before opening a pull request, run from the repository root:

```bash
dotnet restore
dotnet build microservices-vs-virtual-actors.slnx --configuration Release
dotnet test microservices-vs-virtual-actors.slnx --configuration Release --no-build
```

Also validate the affected behavior through the Aspire AppHost when the change involves startup, service discovery, runtime communication, UI behavior, health, topology, logs, traces, or metrics.

Relevant test projects include:

- `Microservices.Tests`;
- `VirtualActors.Tests`;
- `Workbench.AcceptanceTests`;
- `Workbench.ScenarioRegressionTests`.

Add or update tests when changing observable behavior. Do not weaken existing assertions merely to make a change pass.

## Documentation

Update documentation in the same pull request when changing:

- architecture or project structure
- startup or configuration behavior
- scenarios or result semantics
- routes or shared contracts
- health, topology, logging, tracing, or metrics
- known limitations or scope

Prefer updating the narrowest relevant document instead of repeating the same detail across multiple READMEs.

## Pull requests

Keep pull requests small enough to review coherently.

A pull request should explain:

- what changed
- why the change is needed
- how it was validated
- which issue it closes, when applicable

Complete the repository pull request template and ensure:

- the change contains no unrelated modifications
- relevant tests pass
- documentation is current
- no sensitive information or generated local artifacts are included

Maintainers may ask for changes when a pull request expands the repository beyond its architecture-comparison purpose, duplicates existing documentation, introduces unnecessary abstraction, or changes scenario semantics without corresponding tests and explanation.

## Reporting security issues

Do not report suspected security vulnerabilities through a public issue or discussion. Follow the repository security policy when one is available.

Until a security policy is published, avoid sharing exploit details or sensitive information publicly and contact the repository owner privately through their GitHub profile.

## License

By contributing, you agree that your contributions will be licensed under the repository's existing license.
