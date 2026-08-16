namespace Workbench.Gateway.Internal.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Represents the configured backend endpoints for the compared architecture
/// implementations.
/// </summary>
/// <remarks>
/// Both endpoint values must be absolute URLs accepted by
/// <see cref="UrlAttribute"/>. Configuration validation should run during
/// application startup so invalid or missing endpoints fail before requests are
/// processed.
/// </remarks>
internal sealed class ServiceEndpointOptions {
    /// <summary>
    /// Identifies the configuration section containing service endpoint values.
    /// </summary>
    public const string SectionName = "ServiceEndpoints";

    /// <summary>
    /// Gets the base URL of the microservices Orders API.
    /// </summary>
    /// <value>A required valid URL for the microservices implementation.</value>
    [Required]
    [Url]
    public string MicroservicesBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the base URL of the virtual actor Ordering API.
    /// </summary>
    /// <value>A required valid URL for the virtual actor implementation.</value>
    [Required]
    [Url]
    public string VirtualActorsBaseUrl { get; init; } = string.Empty;
}
