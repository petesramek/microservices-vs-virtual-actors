namespace Comparison.Contracts;

/// <summary>
/// Represents the result of running a scenario against one implementation.
/// </summary>
/// <param name="Implementation">The compared implementation name.</param>
/// <param name="Status">The final order status.</param>
/// <param name="Reason">The final reason when applicable.</param>
/// <param name="CompletedOrders">The number of completed orders.</param>
/// <param name="RejectedOrders">The number of rejected orders.</param>
/// <param name="RemainingInventory">The remaining inventory quantity.</param>
/// <param name="ElapsedMilliseconds">The elapsed scenario execution time in milliseconds.</param>
/// <param name="Events">The explanatory scenario timeline.</param>
/// <param name="TotalRequestSubmissions">The total number of order request submissions.</param>
/// <param name="IdempotentResponses">The number of idempotent responses.</param>
public sealed record ScenarioExecutionResult(
    string Implementation,
    OrderStatus Status,
    string? Reason,
    int CompletedOrders,
    int RejectedOrders,
    int RemainingInventory,
    long ElapsedMilliseconds,
    IReadOnlyList<ScenarioEvent> Events,
    int TotalRequestSubmissions = 0,
    int IdempotentResponses = 0);
