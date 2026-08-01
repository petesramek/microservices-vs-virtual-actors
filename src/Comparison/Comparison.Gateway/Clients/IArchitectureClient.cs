namespace Comparison.Gateway.Clients;

using ArchitectureComparison.Contracts;

/// <summary>
/// Runs scenarios against one architecture implementation.
/// </summary>
public interface IArchitectureClient {
    /// <summary>
    /// Runs the specified scenario.
    /// </summary>
    /// <param name="request">The scenario request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The architecture run result.</returns>
    Task<ArchitectureRunResult> RunAsync(RunScenarioRequest request, CancellationToken cancellationToken);
}
