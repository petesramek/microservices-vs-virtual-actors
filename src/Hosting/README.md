# Hosting

This folder contains the shared .NET Aspire hosting projects for the **Microservices vs Virtual Actors** architecture workbench.

## Projects

### `Hosting.AppHost`

Defines and runs the distributed application model. It registers the Workbench, microservices, and virtual actor projects, supplies Aspire-managed service endpoints, configures health checks and startup dependencies; and publishes the observability topology used by the Workbench UI.

See [`Hosting.AppHost/README.md`](Hosting.AppHost/README.md) for the application model, topology, configuration, and local-run guidance.

### `Hosting.ServiceDefaults`

Provides the shared hosting conventions consumed by application services. It centralizes service discovery, outbound HTTP resilience, health endpoints, OpenTelemetry configuration, and scenario-specific tracing and metrics.

See [`Hosting.ServiceDefaults/README.md`](Hosting.ServiceDefaults/README.md) for configuration options, telemetry contracts, health endpoint behavior, and consumer setup.

## How they fit together

`Hosting.AppHost` composes and runs the local distributed application. `Hosting.ServiceDefaults` configures the cross-cutting behavior used inside the participating services.

```text
Hosting.AppHost
    |
    +-- registers and connects application projects
    +-- supplies endpoints and observability configuration

Hosting.ServiceDefaults
    |
    +-- configures each participating service
    +-- provides discovery, resilience, health, tracing, and metrics
```

Application and domain behavior belongs in the Workbench, microservices, and virtual actor projects. The projects in this folder should remain focused on orchestration and shared hosting infrastructure.
