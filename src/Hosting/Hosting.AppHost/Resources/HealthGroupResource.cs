namespace Hosting.AppHost.Resources;

using Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an Aspire Dashboard group whose displayed state is derived from
/// its child resources.
/// </summary>
/// <remarks>
/// The resource is a visual, AppHost-only grouping construct. State aggregation
/// is configured when the resource is added to the application model.
/// </remarks>
internal sealed class HealthGroupResource : Resource {
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthGroupResource"/> class.
    /// </summary>
    /// <param name="name">The resource name used by the Aspire application model.</param>
    /// <param name="displayName">The name displayed in the Aspire Dashboard.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="displayName"/> is empty or whitespace.
    /// </exception>
    public HealthGroupResource(
        string name,
        string displayName)
        : base(name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the name displayed in the Aspire Dashboard.
    /// </summary>
    public string DisplayName { get; }
}
