namespace Hosting.ServiceDefaults.Observability;

using OpenTelemetry.Trace;

/// <summary>
/// Samples scenario trace roots while allowing parent-based sampling to retain
/// their distributed descendants.
/// </summary>
public sealed class ScenarioTraceSampler : Sampler {
    /// <inheritdoc />
    public override SamplingResult ShouldSample(
        in SamplingParameters samplingParameters) {
        bool isScenarioRoot = samplingParameters.Tags?.Any(
            static tag =>
                tag.Key.Equals(
                    ScenarioTelemetry.ScenarioRunTagName,
                    StringComparison.Ordinal)
                && tag.Value is true) == true;

        return new SamplingResult(
            isScenarioRoot
                ? SamplingDecision.RecordAndSample
                : SamplingDecision.Drop);
    }
}
