namespace ArchitectureComparison.Contracts;

/// <summary>
/// Represents the backend status returned by the comparison gateway.
/// </summary>
/// <param name="Gateway">The comparison gateway status.</param>
/// <param name="Microservices">The microservice-style backend entrypoint status.</param>
/// <param name="VirtualActors">The virtual actor-style backend entrypoint status.</param>
public sealed record BackendStatusResponse(
    ServiceStatus Gateway,
    ServiceStatus Microservices,
    ServiceStatus VirtualActors);

/// <summary>
/// Represents one backend service status.
/// </summary>
/// <param name="Name">The display name.</param>
/// <param name="Url">The checked URL.</param>
/// <param name="IsOnline">A value indicating whether the service is online.</param>
/// <param name="StatusCode">The HTTP status code or synthetic status value.</param>
/// <param name="Error">The error message, when unavailable.</param>
public sealed record ServiceStatus(
    string Name,
    string Url,
    bool IsOnline,
    string StatusCode,
    string? Error);
