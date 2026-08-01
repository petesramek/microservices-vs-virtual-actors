namespace Comparison.Gateway.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Represents backend endpoint configuration for architecture implementations.
/// </summary>
public sealed class ServiceEndpointOptions {
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = $"ServiceEndpoints";

    /// <summary>
    /// Gets the base URL of the microservice-style Orders API.
    /// </summary>
    [Required]
    [Url]
    public string MicroservicesBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the base URL of the virtual actor-style Ordering API.
    /// </summary>
    [Required]
    [Url]
    public string VirtualActorsBaseUrl { get; init; } = string.Empty;
}
