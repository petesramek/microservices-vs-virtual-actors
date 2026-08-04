namespace Hosting.AppHost.Resources;

using Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a visual resource group whose state is derived from its child resources.
/// </summary>
internal sealed class HealthGroupResource : Resource {
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthGroupResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="displayName">The display name shown in the Aspire Dashboard.</param>
    public HealthGroupResource(
        string name,
        string displayName)
        : base(name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the display name shown in the Aspire Dashboard.
    /// </summary>
    public string DisplayName { get; }
}
