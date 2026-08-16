namespace Workbench.Contracts.Scenarios;

using Workbench.Contracts.Orders;

/// <summary>
/// Represents the result of executing a scenario against one architecture
/// implementation.
/// </summary>
/// <param name="Implementation">
/// The stable display name of the compared implementation.
/// </param>
/// <param name="Status">The final order status.</param>
/// <param name="Reason">
/// The optional terminal reason, or <see langword="null"/> when no reason
/// applies.
/// </param>
/// <param name="CompletedOrders">
/// The number of orders that completed successfully.
/// </param>
/// <param name="RejectedOrders">The number of rejected orders.</param>
/// <param name="RemainingInventory">
/// The inventory quantity remaining after scenario execution.
/// </param>
/// <param name="ElapsedMilliseconds">
/// The total scenario execution duration in milliseconds.
/// </param>
/// <param name="Events">
/// The explanatory events in timeline order.
/// </param>
/// <param name="TotalRequestSubmissions">
/// The total number of order request submissions, including repeated requests.
/// </param>
/// <param name="IdempotentResponses">
/// The number of responses served from previously established idempotent
/// outcomes.
/// </param>
/// <remarks>
/// The supplied event collection is exposed through a read-only interface but
/// is not defensively copied by this positional record.
/// </remarks>
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
