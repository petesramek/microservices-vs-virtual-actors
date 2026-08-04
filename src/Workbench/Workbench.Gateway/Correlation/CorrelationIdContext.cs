namespace Workbench.Gateway.Correlation;

/// <summary>
/// Provides access to the correlation identifier for the current asynchronous execution flow.
/// </summary>
internal static class CorrelationIdContext {
    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>
    /// Gets or sets the correlation identifier for the current asynchronous execution flow.
    /// </summary>
    public static string? CurrentId {
        get => Current.Value;
        set => Current.Value = value;
    }
}
