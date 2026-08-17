namespace Observability.Health.Abstraction;

public interface IHealthStatusEvaluator {
    HealthStatus Evaluate(IReadOnlyCollection<HealthStatus> statuses);
}