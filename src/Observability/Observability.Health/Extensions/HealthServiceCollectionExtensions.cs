namespace Observability.Health.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Observability.Health;

/// <summary>
/// Provides dependency-injection registration extensions for health-status
/// evaluation services.
/// </summary>
public static class HealthServiceCollectionExtensions {
    /// <summary>
    /// Registers the default health-status evaluator as a singleton service.
    /// </summary>
    /// <param name="services">
    /// The service collection that receives the registration.
    /// </param>
    /// <returns>The supplied service collection.</returns>
    /// <remarks>
    /// The singleton lifetime is appropriate because the evaluator is expected
    /// to be stateless. Repeated calls add repeated service descriptors; callers
    /// should invoke this method once during application composition.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddHealthStatusEvaluator(
        this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IHealthStatusEvaluator, HealthStatusEvaluator>();

        return services;
    }
}
