namespace Observability.Health;
/// <summary>
/// Calculates aggregate health from a collection of health observations.
/// </summary>
public class HealthStatusEvaluator {
    public static readonly HealthStatusEvaluator Instance = new();

    /// <summary>
    /// Calculates the aggregate health represented by the supplied observations.
    /// </summary>
    /// <param name="statuses">The health observations to aggregate.</param>
    /// <returns>
    /// <see cref="HealthStatus.Unknown"/> when no observations are available,
    /// <see cref="HealthStatus.Starting"/> when at least one observation is still
    /// starting, <see cref="HealthStatus.Healthy"/> when all observations are
    /// healthy, <see cref="HealthStatus.Degraded"/> when at least part of the
    /// observed system remains available, or <see cref="HealthStatus.Unhealthy"/>
    /// when no observed part is available.
    /// </returns>
    public HealthStatus Evaluate(
        IReadOnlyCollection<HealthStatus> statuses) {
        ArgumentNullException.ThrowIfNull(statuses);

        if (statuses.Count == 0) {
            return HealthStatus.Unknown;
        }

        int healthyCount = 0;
        int degradedCount = 0;
        int unhealthyCount = 0;
        int unknownCount = 0;

        foreach (HealthStatus status in statuses) {
            switch (status) {
                case HealthStatus.Starting:
                    return HealthStatus.Starting;

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
                    unknownCount++;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(statuses),
                        status,
                        "Unsupported observability health status.");
            }
        }

        if (healthyCount == statuses.Count) {
            return HealthStatus.Healthy;
        }

        if (healthyCount > 0 || degradedCount > 0) {
            return HealthStatus.Degraded;
        }

        if (unhealthyCount > 0) {
            return HealthStatus.Unhealthy;
        }

        return unknownCount > 0
            ? HealthStatus.Unknown
            : throw new InvalidOperationException(
                "The health observations could not be aggregated.");
    }
}
