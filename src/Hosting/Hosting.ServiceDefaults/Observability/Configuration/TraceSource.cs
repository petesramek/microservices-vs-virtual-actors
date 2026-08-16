namespace Hosting.ServiceDefaults.Observability.Configuration;

using System;

/// <summary>
/// Specifies the trace sources enabled for shared OpenTelemetry collection.
/// </summary>
/// <remarks>
/// Values may be combined to enable multiple trace sources. Use
/// <see cref="None"/> to disable all optional sources or <see cref="All"/> to
/// enable every supported source. The application activity source registered
/// from the host application name is configured independently of these flags.
/// </remarks>
[Flags]
public enum TraceSource {
    /// <summary>
    /// Disables all optional trace sources.
    /// </summary>
    None = 0,

    /// <summary>
    /// Enables ASP.NET Core server request tracing.
    /// </summary>
    AspNetCore = 1,

    /// <summary>
    /// Enables outbound HTTP client tracing.
    /// </summary>
    HttpClient = 2,

    /// <summary>
    /// Enables Entity Framework Core tracing.
    /// </summary>
    EntityFrameworkCore = 4,

    /// <summary>
    /// Enables activity sources emitted by Microsoft Orleans.
    /// </summary>
    MicrosoftOrleans = 8,

    /// <summary>
    /// Enables workbench scenario tracing emitted by
    /// <see cref="ScenarioInstrumentation.ActivitySource"/>.
    /// </summary>
    Scenario = 16,

    /// <summary>
    /// Enables every supported trace source.
    /// </summary>
    All = AspNetCore
        | HttpClient
        | EntityFrameworkCore
        | MicrosoftOrleans
        | Scenario,
}
