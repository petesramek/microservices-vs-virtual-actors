namespace Hosting.ServiceDefaults.Observability;

using Hosting.ServiceDefaults.Telemetry;
using OpenTelemetry.Trace;

/// <summary>
/// Samples scenario trace roots while allowing parent-based sampling to retain
/// their distributed descendants.
/// </summary>
public sealed class ScenarioTraceSampler : Sampler {
    /// <inheritdoc />
    public override SamplingResult ShouldSample(
        in SamplingParameters samplingParameters) {
        SamplingDecision decision = samplingParameters.Name.Equals(
            ScenarioTelemetry.RunScenarioActivityName,
            StringComparison.Ordinal)
            ? SamplingDecision.RecordAndSample
            : SamplingDecision.Drop;

        return new SamplingResult(decision);
    }
}
