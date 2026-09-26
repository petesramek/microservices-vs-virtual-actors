namespace Workbench.Contracts.Scenarios;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Represents a request to execute a workbench scenario.
/// </summary>
/// <remarks>
/// The request contains both the workflow input and the deterministic setup
/// values used to prepare comparable microservices and virtual actor runs.
/// Validation attributes define the input limits enforced by participating
/// ASP.NET Core applications.
/// </remarks>
public sealed record RunScenarioRequest {
    /// <summary>
    /// Gets or sets the scenario to execute.
    /// </summary>
    /// <value>
    /// The selected scenario. The default is
    /// <see cref="ScenarioKind.SuccessfulOrder"/>.
    /// </value>
    [Required]
    public ScenarioKind Scenario { get; set; } = ScenarioKind.SuccessfulOrder;

    /// <summary>
    /// Gets or sets the product identifier used by the workflow.
    /// </summary>
    /// <value>
    /// A required product identifier containing at most 100 characters.
    /// </value>
    [Required]
    [MaxLength(100)]
    public string ProductId { get; set; } = "product-001";

    /// <summary>
    /// Gets or sets the customer identifier used by the workflow.
    /// </summary>
    /// <value>
    /// A required customer identifier containing at most 100 characters.
    /// </value>
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = "customer-001";

    /// <summary>
    /// Gets or sets the order identifier used for the scenario run.
    /// </summary>
    /// <value>
    /// The order identifier. A new identifier is generated for each request
    /// instance by default.
    /// </value>
    public Guid OrderId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the key used to identify repeated order requests.
    /// </summary>
    /// <value>
    /// A required idempotency key containing at most 200 characters. A new
    /// compact GUID value is generated for each request instance by default.
    /// </value>
    [Required]
    [MaxLength(200)]
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the product quantity requested by each order.
    /// </summary>
    /// <value>A value from 1 through 100,000. The default is 1.</value>
    [Range(1, 100_000)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the inventory quantity used to prepare the scenario.
    /// </summary>
    /// <value>A value from 0 through 100,000. The default is 10.</value>
    [Range(0, 100_000)]
    public int InitialStock { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of order requests issued by concurrency
    /// scenarios.
    /// </summary>
    /// <value>A value from 1 through 50. The default is 10.</value>
    [Range(1, 50)]
    public int ConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether the deterministic payment-failure
    /// path is requested.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to request simulated payment failure; otherwise,
    /// <see langword="false"/>.
    /// </value>
    public bool SimulatePaymentFailure { get; set; }
}
