namespace Observability.Health.Abstraction;

/// <summary>
/// Defines an operation for aggregating health statuses into one overall
/// health status.
/// </summary>
public interface IHealthStatusEvaluator {
    /// <summary>
    /// Evaluates a collection of health statuses and returns their aggregate
    /// status.
    /// </summary>
    /// <param name="statuses">The health statuses to evaluate.</param>
    /// <returns>The aggregate health status.</returns>
    HealthStatus Evaluate(IReadOnlyCollection<HealthStatus> statuses);
}
