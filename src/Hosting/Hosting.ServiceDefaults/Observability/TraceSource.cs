namespace Hosting.ServiceDefaults.Observability;

using System;

[Flags]
public enum TraceSource {
    None = 0,
    Runtime = 1,
    AspNetCore = 2,
    HttpClient = 4,
    EntityFrameworkCore = 8,
    MicrosoftOrleans = 16,
    Scenario = 32,
    All = Runtime | AspNetCore | HttpClient | EntityFrameworkCore | MicrosoftOrleans | Scenario,
}
