namespace Workbench.Ui.Models;

using System.ComponentModel.DataAnnotations;
using Workbench.Contracts.Scenarios;

/// <summary>
/// Represents the editable UI state and validation rules used to configure a
/// scenario execution request.
/// </summary>
public sealed class ScenarioFormModel : IValidatableObject {
    /// <summary>
    /// Gets or sets the scenario selected for execution.
    /// </summary>
    /// <value>
    /// The selected scenario. The default is
    /// <see cref="ScenarioKind.SuccessfulOrder"/>.
    /// </value>
    public ScenarioKind Scenario { get; set; } = ScenarioKind.SuccessfulOrder;

    /// <summary>
    /// Gets or sets the product identifier used by the scenario.
    /// </summary>
    /// <value>
    /// A non-empty product identifier containing no more than 100 characters.
    /// The value is trimmed when the request contract is created.
    /// </value>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ProductId { get; set; } = "product-001";

    /// <summary>
    /// Gets or sets the customer identifier used by the scenario.
    /// </summary>
    /// <value>
    /// A non-empty customer identifier containing no more than 100 characters.
    /// The value is trimmed when the request contract is created.
    /// </value>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CustomerId { get; set; } = "customer-001";

    /// <summary>
    /// Gets or sets the quantity requested by each order.
    /// </summary>
    /// <value>An order quantity from 1 through 100,000.</value>
    [Range(1, 100_000, ErrorMessage = "Quantity must be between 1 and 100000.")]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the inventory quantity available before scenario execution.
    /// </summary>
    /// <value>An initial stock quantity from 0 through 100,000.</value>
    [Range(0, 100_000, ErrorMessage = "Initial stock must be between 0 and 100000.")]
    public int InitialStock { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of request submissions used by scenarios that
    /// execute concurrently.
    /// </summary>
    /// <value>
    /// The concurrent request count. When concurrency applies, validation
    /// requires a value from 1 through 50. The default is 10.
    /// </value>
    public int ConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets the idempotency key associated with the scenario request.
    /// </summary>
    /// <value>
    /// A non-empty idempotency key containing no more than 200 characters. The
    /// value is trimmed when the request contract is created.
    /// </value>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string IdempotencyKey { get; set; } = $"SuccessfulOrder-{Guid.NewGuid():N}";

    /// <summary>
    /// Gets a value indicating whether the selected scenario uses the
    /// concurrent request count.
    /// </summary>
    /// <value>
    /// <see langword="true"/> for concurrent-order, hot-product-contention,
    /// and duplicate-request scenarios; otherwise, <see langword="false"/>.
    /// </value>
    public bool UsesConcurrentRequests => Scenario is
        ScenarioKind.ConcurrentOrders or
        ScenarioKind.HotProductContention or
        ScenarioKind.DuplicateRequest;

    /// <summary>
    /// Creates a scenario request contract from the current form values.
    /// </summary>
    /// <returns>
    /// A request populated with trimmed identifiers, the current scenario
    /// values, the applicable concurrent request count, and the payment-failure
    /// simulation setting derived from the selected scenario.
    /// </returns>
    public RunScenarioRequest ToRequest() {
        var defaults = ScenarioDefaults.For(Scenario);

        return new RunScenarioRequest {
            Scenario = Scenario,
            ProductId = ProductId.Trim(),
            CustomerId = CustomerId.Trim(),
            Quantity = Quantity,
            InitialStock = InitialStock,
            ConcurrentRequests = UsesConcurrentRequests
                ? ConcurrentRequests
                : defaults.ConcurrentRequests,
            IdempotencyKey = IdempotencyKey.Trim(),
            SimulatePaymentFailure = Scenario is
                ScenarioKind.PaymentFailureCompensation or
                ScenarioKind.PaymentTimeoutAfterReservation,
        };
    }

    /// <summary>
    /// Validates scenario-specific form rules that are not expressed by data
    /// annotation attributes.
    /// </summary>
    /// <param name="validationContext">
    /// The context that describes the object being validated.
    /// </param>
    /// <returns>
    /// A sequence containing a validation error when a concurrency-based
    /// scenario specifies fewer than 1 or more than 50 requests; otherwise, an
    /// empty sequence.
    /// </returns>
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext) {
        if (UsesConcurrentRequests && ConcurrentRequests is < 1 or > 50) {
            yield return new ValidationResult(
                "Concurrent requests must be between 1 and 50 for local demo safety.",
                [nameof(ConcurrentRequests)]);
        }
    }

    /// <summary>
    /// Resets scenario-dependent form fields to the defaults for the selected
    /// scenario and generates a new idempotency key.
    /// </summary>
    public void ResetAdvancedSettings() {
        var defaults = ScenarioDefaults.For(Scenario);

        InitialStock = defaults.InitialStock;
        Quantity = defaults.Quantity;
        ConcurrentRequests = defaults.ConcurrentRequests;
        ProductId = "product-001";
        CustomerId = "customer-001";
        IdempotencyKey = $"{Scenario}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Creates a form model initialized with the defaults for a scenario.
    /// </summary>
    /// <param name="scenario">The scenario for which to create the model.</param>
    /// <returns>
    /// A form model whose scenario-dependent values have been reset to the
    /// defaults associated with <paramref name="scenario"/>.
    /// </returns>
    public static ScenarioFormModel Create(ScenarioKind scenario) {
        var model = new ScenarioFormModel { Scenario = scenario };
        model.ResetAdvancedSettings();
        return model;
    }

    /// <summary>
    /// Represents the default numeric form values associated with a scenario.
    /// </summary>
    /// <param name="InitialStock">
    /// The inventory quantity available before scenario execution.
    /// </param>
    /// <param name="Quantity">The quantity requested by each order.</param>
    /// <param name="ConcurrentRequests">
    /// The number of request submissions used by concurrent scenarios.
    /// </param>
    public sealed record ScenarioDefaults(
        int InitialStock,
        int Quantity,
        int ConcurrentRequests) {
        /// <summary>
        /// Gets the default numeric form values for a scenario.
        /// </summary>
        /// <param name="scenario">
        /// The scenario whose defaults should be returned.
        /// </param>
        /// <returns>
        /// The configured defaults for <paramref name="scenario"/>, or the
        /// successful-order defaults when the scenario has no dedicated
        /// mapping.
        /// </returns>
        public static ScenarioDefaults For(ScenarioKind scenario) {
            return scenario switch {
                ScenarioKind.InsufficientInventory =>
                    new ScenarioDefaults(1, 2, 10),
                ScenarioKind.PaymentFailureCompensation =>
                    new ScenarioDefaults(10, 2, 10),
                ScenarioKind.PaymentTimeoutAfterReservation =>
                    new ScenarioDefaults(10, 2, 10),
                ScenarioKind.ConcurrentOrders =>
                    new ScenarioDefaults(3, 1, 10),
                ScenarioKind.HotProductContention =>
                    new ScenarioDefaults(25, 1, 50),
                ScenarioKind.DuplicateRequest =>
                    new ScenarioDefaults(10, 2, 20),
                _ => new ScenarioDefaults(10, 1, 10),
            };
        }
    }
}
