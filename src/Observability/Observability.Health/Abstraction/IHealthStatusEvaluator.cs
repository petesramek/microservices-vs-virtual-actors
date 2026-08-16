namespace Observability.Health;

public interface IHealthStatusEvaluator {
    HealthStatus Evaluate(IReadOnlyCollection<HealthStatus> statuses);
}