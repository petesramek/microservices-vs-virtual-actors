namespace Comparison.Contracts;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request used by the comparison gateway and UI to run a scenario.
/// </summary>
public sealed record RunScenarioRequest {
    /// <summary>
    /// Gets or sets the scenario to run.
    /// </summary>
    [Required]
    public ScenarioKind Scenario { get; set; } = ScenarioKind.SuccessfulOrder;

    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ProductId { get; set; } = $"product-001";

    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = $"customer-001";

    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    public Guid OrderId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString($"N");

    /// <summary>
    /// Gets or sets the requested order quantity.
    /// </summary>
    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the initial stock used by scenario reset/setup.
    /// </summary>
    [Range(0, 100000)]
    public int InitialStock { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of concurrent order attempts for concurrency scenarios.
    /// </summary>
    [Range(1, 100)]
    public int ConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether payment failure should be simulated.
    /// </summary>
    public bool SimulatePaymentFailure { get; set; }
}

/// <summary>
/// Response returned by the comparison gateway.
/// </summary>
/// <param name="Scenario">The scenario that was run.</param>
/// <param name="Microservices">The microservices result, when requested.</param>
/// <param name="VirtualActors">The virtual actors result, when requested.</param>
public sealed record RunScenarioResponse(
    ScenarioKind Scenario,
    ArchitectureRunResult? Microservices,
    ArchitectureRunResult? VirtualActors);

/// <summary>
/// Result for one architecture implementation.
/// </summary>
/// <param name="Architecture">The architecture name.</param>
/// <param name="Status">The final order status.</param>
/// <param name="Reason">The final reason, when applicable.</param>
/// <param name="CompletedOrders">The number of completed orders.</param>
/// <param name="RejectedOrders">The number of rejected orders.</param>
/// <param name="RemainingInventory">The remaining inventory quantity.</param>
/// <param name="ElapsedMilliseconds">The simulated elapsed time.</param>
/// <param name="Events">The event timeline.</param>
public sealed record ArchitectureRunResult(
    string Architecture,
    OrderStatus Status,
    string? Reason,
    int CompletedOrders,
    int RejectedOrders,
    int RemainingInventory,
    long ElapsedMilliseconds,
    IReadOnlyList<ScenarioEvent> Events,
    int TotalRequestSubmissions = 0,
    int IdempotentResponses = 0);

/// <summary>
/// Timeline event emitted by a scenario run.
/// </summary>
/// <param name="Source">The source service or actor.</param>
/// <param name="Message">The event message.</param>
public sealed record ScenarioEvent(string Source, string Message);

