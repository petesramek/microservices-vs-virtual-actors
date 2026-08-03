namespace Comparison.Ui.Models;

using Comparison.Contracts;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// UI form model used by the scenario runner page.
/// </summary>
public sealed class ScenarioFormModel : IValidatableObject {
    /// <summary>
    /// Gets or sets the selected scenario.
    /// </summary>
    public ScenarioKind Scenario { get; set; } = ScenarioKind.SuccessfulOrder;

    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ProductId { get; set; } = $"product-001";

    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CustomerId { get; set; } = $"customer-001";

    /// <summary>
    /// Gets or sets the quantity requested by each order.
    /// </summary>
    [Range(1, 100_000, ErrorMessage = $"Quantity must be between 1 and 100000.")]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the initial inventory stock.
    /// </summary>
    [Range(0, 100_000, ErrorMessage = $"Initial stock must be between 0 and 100000.")]
    public int InitialStock { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of concurrent requests. This only applies to concurrent scenarios.
    /// </summary>
    public int ConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string IdempotencyKey { get; set; } = $"SuccessfulOrder-{Guid.NewGuid():N}";

    /// <summary>
    /// Gets a value indicating whether the concurrent requests field applies to the current scenario.
    /// </summary>
    public bool UsesConcurrentRequests => Scenario is ScenarioKind.ConcurrentOrders or ScenarioKind.HotProductContention or ScenarioKind.DuplicateRequest;

    /// <summary>
    /// Creates a request contract from this form model.
    /// </summary>
    /// <returns>The scenario request.</returns>
    public RunScenarioRequest ToRequest() {
        var defaults = ScenarioDefaults.For(Scenario);

        return new RunScenarioRequest {
            Scenario = Scenario,
            ProductId = ProductId.Trim(),
            CustomerId = CustomerId.Trim(),
            Quantity = Quantity,
            InitialStock = InitialStock,
            ConcurrentRequests = UsesConcurrentRequests ? ConcurrentRequests : defaults.ConcurrentRequests,
            IdempotencyKey = IdempotencyKey.Trim(),
            SimulatePaymentFailure = Scenario is ScenarioKind.PaymentFailureCompensation or ScenarioKind.PaymentTimeoutAfterReservation,
        };
    }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
        if (UsesConcurrentRequests && ConcurrentRequests is < 1 or > 50) {
            yield return new ValidationResult(
$"Concurrent requests must be between 1 and 50 for local demo safety.",
                [nameof(ConcurrentRequests)]);
        }
    }

    /// <summary>
    /// Resets scenario-specific advanced values to defaults.
    /// </summary>
    public void ResetAdvancedSettings() {
        var defaults = ScenarioDefaults.For(Scenario);
        InitialStock = defaults.InitialStock;
        Quantity = defaults.Quantity;
        ConcurrentRequests = defaults.ConcurrentRequests;
        ProductId = $"product-001";
        CustomerId = $"customer-001";
        IdempotencyKey = $"{Scenario}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Creates a form model for a scenario.
    /// </summary>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The form model.</returns>
    public static ScenarioFormModel Create(ScenarioKind scenario) {
        var model = new ScenarioFormModel { Scenario = scenario };
        model.ResetAdvancedSettings();
        return model;
    }
}

/// <summary>
/// Default values used by the scenario runner form.
/// </summary>
/// <param name="InitialStock">The initial inventory stock.</param>
/// <param name="Quantity">The quantity requested by each order.</param>
/// <param name="ConcurrentRequests">The number of concurrent requests.</param>
public sealed record ScenarioDefaults(int InitialStock, int Quantity, int ConcurrentRequests) {
    /// <summary>
    /// Gets defaults for the specified scenario.
    /// </summary>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The defaults.</returns>
    public static ScenarioDefaults For(ScenarioKind scenario) {
        return scenario switch {
            ScenarioKind.InsufficientInventory => new ScenarioDefaults(1, 2, 10),
            ScenarioKind.PaymentFailureCompensation => new ScenarioDefaults(10, 2, 10),
            ScenarioKind.PaymentTimeoutAfterReservation => new ScenarioDefaults(10, 2, 10),
            ScenarioKind.ConcurrentOrders => new ScenarioDefaults(3, 1, 10),
            ScenarioKind.HotProductContention => new ScenarioDefaults(25, 1, 50),
            ScenarioKind.DuplicateRequest => new ScenarioDefaults(10, 2, 20),
            _ => new ScenarioDefaults(10, 1, 10),
        };
    }
}





