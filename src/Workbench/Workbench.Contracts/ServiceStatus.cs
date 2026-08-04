namespace Workbench.Contracts;

/// <summary>
/// Represents the status of a service.
/// </summary>
/// <param name="Name">The display name of the service.</param>
/// <param name="Url">The health endpoint URL.</param>
/// <param name="IsOnline">A value indicating whether the service is online.</param>
/// <param name="Status">The reported or derived service status.</param>
/// <param name="Error">The error message when the service is unavailable.</param>
public sealed record ServiceStatus(
    string Name,
    string Url,
    bool IsOnline,
    string Status,
    string? Error);
