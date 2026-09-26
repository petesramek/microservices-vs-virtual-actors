namespace Hosting.ServiceDefaults.Observability.Configuration;

using System;

/// <summary>
/// Specifies the metric sources enabled for shared OpenTelemetry collection.
/// </summary>
/// <remarks>
/// Values may be combined to enable multiple metric sources. Use
/// <see cref="None"/> to disable all optional metric sources or
/// <see cref="All"/> to enable every supported source.
/// </remarks>
[Flags]
public enum MetricSource {
    /// <summary>
    /// Disables all optional metric sources.
    /// </summary>
    None = 0,

    /// <summary>
    /// Enables .NET runtime metrics.
    /// </summary>
    Runtime = 1,

    /// <summary>
    /// Enables ASP.NET Core metrics.
    /// </summary>
    AspNetCore = 2,

    /// <summary>
    /// Enables outbound HTTP client metrics.
    /// </summary>
    HttpClient = 4,

    /// <summary>
    /// Enables metrics emitted by Entity Framework Core.
    /// </summary>
    EntityFrameworkCore = 8,

    /// <summary>
    /// Enables metrics emitted by Microsoft Orleans.
    /// </summary>
    MicrosoftOrleans = 16,

    /// <summary>
    /// Enables workbench scenario metrics emitted by the
    /// <c>Scenario.Workflows</c> meter.
    /// </summary>
    Scenario = 32,

    /// <summary>
    /// Enables every supported metric source.
    /// </summary>
    All = Runtime
        | AspNetCore
        | HttpClient
        | EntityFrameworkCore
        | MicrosoftOrleans
        | Scenario,
}
