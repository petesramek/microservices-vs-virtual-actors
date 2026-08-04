namespace Workbench.Contracts;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Represents a request to run a workbench scenario.
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
    public string ProductId { get; set; } = "product-001";

    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = "customer-001";

    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    public Guid OrderId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the requested order quantity.
    /// </summary>
    [Range(1, 100_000)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the initial inventory quantity used to prepare the scenario.
    /// </summary>
    [Range(0, 100_000)]
    public int InitialStock { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of concurrent order requests used by concurrency scenarios.
    /// </summary>
    [Range(1, 50)]
    public int ConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether payment failure should be simulated.
    /// </summary>
    public bool SimulatePaymentFailure { get; set; }
}
