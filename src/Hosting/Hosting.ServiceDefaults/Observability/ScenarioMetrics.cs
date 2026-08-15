namespace Hosting.ServiceDefaults.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Provides metrics for completed workflow runs.
/// </summary>
public sealed class ScenarioMetrics {
    /// <summary>
    /// The name of the meter that contains workflow metrics.
    /// </summary>
    public const string MeterName = "Scenario.Workflows";

    private readonly Counter<long> _workflowRunCount;
    private readonly Histogram<double> _workflowRunDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">The factory used to create the application meter.</param>
    public ScenarioMetrics(IMeterFactory meterFactory) {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);

        _workflowRunCount = meter.CreateCounter<long>(
            name: "workflow.run.count",
            unit: "{run}",
            description: "The number of workflow runs that reached a terminal state.");

        _workflowRunDuration = meter.CreateHistogram<double>(
            name: "workflow.run.duration",
            unit: "s",
            description: "The duration of workflow runs that reached a terminal state.");
    }

    /// <summary>
    /// Records a workflow run that reached a terminal state.
    /// </summary>
    /// <param name="duration">The elapsed duration of the workflow run.</param>
    /// <param name="architecture">The architecture used to execute the workflow.</param>
    /// <param name="workflowKind">The bounded kind of workflow that was executed.</param>
    public void RecordWorkflowRun(
        TimeSpan duration,
        string architecture,
        string workflowKind) {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowKind);

        var tags = new TagList
        {
            { "architecture", architecture },
            { "workflow.kind", workflowKind },
        };

        _workflowRunCount.Add(1, tags);
        _workflowRunDuration.Record(duration.TotalSeconds, tags);
    }
}
