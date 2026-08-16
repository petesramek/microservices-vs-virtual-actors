namespace Hosting.ServiceDefaults.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Records metrics for workflow runs that reach a terminal state.
/// </summary>
/// <remarks>
/// Measurements are partitioned by architecture and scenario kind. Callers
/// must use stable, bounded values for both dimensions to avoid unbounded
/// metric cardinality. Register this type with a singleton lifetime so its
/// instruments are created once and reused.
/// </remarks>
public sealed class ScenarioMetrics {
    /// <summary>
    /// Records the number of workflow runs that reached a terminal state.
    /// </summary>
    private readonly Counter<long> _workflowRunCount;

    /// <summary>
    /// Records the duration of workflow runs that reached a terminal state.
    /// </summary>
    private readonly Histogram<double> _workflowRunDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">
    /// The factory used to create the shared workflow meter.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="meterFactory"/> is <see langword="null"/>.
    /// </exception>
    public ScenarioMetrics(IMeterFactory meterFactory) {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(ScenarioInstrumentation.MeterName);

        _workflowRunCount = meter.CreateCounter<long>(
            name: ScenarioInstrumentation.InstrumentNames.RunCount,
            unit: "{run}",
            description: "The number of workflow runs that reached a terminal state.");

        _workflowRunDuration = meter.CreateHistogram<double>(
            name: ScenarioInstrumentation.InstrumentNames.RunDuration,
            unit: "s",
            description: "The duration of workflow runs that reached a terminal state.");
    }

    /// <summary>
    /// Records one workflow run that reached a terminal state.
    /// </summary>
    /// <param name="duration">The non-negative elapsed workflow duration.</param>
    /// <param name="architecture">
    /// The stable, bounded architecture value used to partition measurements.
    /// </param>
    /// <param name="scenarioKind">
    /// The stable, bounded scenario-kind value used to partition measurements.
    /// </param>
    /// <remarks>
    /// The method increments
    /// <see cref="ScenarioInstrumentation.InstrumentNames.RunCount"/> and records
    /// <paramref name="duration"/> in seconds to
    /// <see cref="ScenarioInstrumentation.InstrumentNames.RunDuration"/>. Both
    /// measurements use the same tags in the same order.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="duration"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="architecture"/> or <paramref name="scenarioKind"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="architecture"/> or <paramref name="scenarioKind"/> is
    /// empty or consists only of white-space characters.
    /// </exception>
    public void RecordWorkflowRun(
        TimeSpan duration,
        string architecture,
        string scenarioKind) {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKind);

        TagList tags = new() {
            { ScenarioInstrumentation.TagNames.Architecture, architecture },
            { ScenarioInstrumentation.TagNames.ScenarioKind, scenarioKind },
        };

        _workflowRunCount.Add(1, tags);
        _workflowRunDuration.Record(duration.TotalSeconds, tags);
    }
}
