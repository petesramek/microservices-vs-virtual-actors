namespace Comparison.Gateway.Clients;

/// <summary>
/// Stores the current correlation identifier for the active asynchronous scenario execution flow.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>
    /// Gets or sets the current correlation identifier.
    /// </summary>
    public static string? CurrentCorrelationId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
