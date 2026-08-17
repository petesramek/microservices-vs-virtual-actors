namespace Observability.Health;

using Observability.Health.Abstraction;

/// <summary>
/// Evaluates aggregate health from a collection of health observations.
/// </summary>
/// <remarks>
/// The evaluator is stateless and can be reused across evaluations. A
/// <see cref="HealthStatus.Starting"/> observation takes precedence over every
/// other status. Otherwise, an entirely healthy collection is healthy; a
/// collection containing a healthy or degraded observation is degraded; a
/// collection containing an unhealthy observation and no healthy or degraded
/// observation is unhealthy; and a collection containing only unknown
/// observations is unknown.
/// </remarks>
public sealed class HealthStatusEvaluator : IHealthStatusEvaluator {
    /// <summary>
    /// Evaluates the aggregate health represented by the supplied observations.
    /// </summary>
    /// <param name="statuses">The health observations to aggregate.</param>
    /// <returns>
    /// The aggregate health status according to the precedence rules documented
    /// by this evaluator.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="statuses"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="statuses"/> contains an unsupported
    /// <see cref="HealthStatus"/> value.
    /// </exception>
    public HealthStatus Evaluate(
        IReadOnlyCollection<HealthStatus> statuses) {
        ArgumentNullException.ThrowIfNull(statuses);

        if (statuses.Count == 0) {
            return HealthStatus.Unknown;
        }

        bool hasStarting = false;
        int healthyCount = 0;
        int degradedCount = 0;
        int unhealthyCount = 0;

        foreach (HealthStatus status in statuses) {
            switch (status) {
                case HealthStatus.Starting:
                    hasStarting = true;
                    break;
                case HealthStatus.Healthy:
                    healthyCount++;
                    break;
                case HealthStatus.Degraded:
                    degradedCount++;
                    break;
                case HealthStatus.Unhealthy:
                    unhealthyCount++;
                    break;
                case HealthStatus.Unknown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(statuses),
                        status,
                        "Unsupported observability health status.");
            }
        }

        if (hasStarting) {
            return HealthStatus.Starting;
        }

        if (healthyCount == statuses.Count) {
            return HealthStatus.Healthy;
        }

        if (healthyCount > 0 || degradedCount > 0) {
            return HealthStatus.Degraded;
        }

        return unhealthyCount > 0
            ? HealthStatus.Unhealthy
            : HealthStatus.Unknown;
    }
}
