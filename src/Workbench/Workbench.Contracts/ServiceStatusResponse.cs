namespace Workbench.Contracts;

/// <summary>
/// Represents the statuses of the services used by the workbench application.
/// </summary>
/// <param name="Gateway">The Gateway service status.</param>
/// <param name="Microservices">The Microservices service status.</param>
/// <param name="VirtualActors">The Virtual Actors service status.</param>
public sealed record ServiceStatusResponse(
    ServiceStatus Gateway,
    ServiceStatus Microservices,
    ServiceStatus VirtualActors);
