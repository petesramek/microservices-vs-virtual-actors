namespace Hosting.ServiceDefaults.Observability.Tracing;

using OpenTelemetry.Trace;

/// <summary>
/// Selects scenario root spans for recording and export.
/// </summary>
/// <remarks>
/// A root span is selected only when its initial attributes contain
/// <see cref="ScenarioInstrumentation.TagNames.ScenarioRun"/> with the Boolean value
/// <see langword="true"/>. Other roots are dropped.
///
/// <para>
/// This sampler is intended to be used as the root sampler of a
/// <see cref="ParentBasedSampler"/>. The parent-based sampler preserves the
/// sampling decision for local and distributed descendants, so this type does
/// not inspect parent context directly.
/// </para>
///
/// <para>
/// The scenario attribute must be present when the span is created because
/// head-based sampling runs before attributes added later in the span lifetime
/// are available to the sampler.
/// </para>
/// </remarks>
public sealed class ScenarioTraceSampler : Sampler {
    /// <summary>
    /// Determines whether a span should be recorded and sampled as a scenario
    /// trace root.
    /// </summary>
    /// <param name="samplingParameters">
    /// The span creation data available to the sampler.
    /// </param>
    /// <returns>
    /// <see cref="SamplingDecision.RecordAndSample"/> when the initial span
    /// attributes contain the enabled scenario-run marker; otherwise
    /// <see cref="SamplingDecision.Drop"/>.
    /// </returns>
    /// <remarks>
    /// Attribute-name comparison is ordinal, and the marker value must be a
    /// Boolean <see langword="true"/> rather than a textual representation.
    /// </remarks>
    public override SamplingResult ShouldSample(
        in SamplingParameters samplingParameters) {
        bool isScenarioRoot = samplingParameters.Tags?.Any(
            static tag =>
                tag.Key.Equals(
                    ScenarioInstrumentation.TagNames.ScenarioRun,
                    StringComparison.Ordinal)
                && tag.Value is true) == true;

        return new SamplingResult(
            isScenarioRoot
                ? SamplingDecision.RecordAndSample
                : SamplingDecision.Drop);
    }
}
