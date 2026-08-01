namespace Comparison.Contracts;

/// <summary>
/// Represents one explanatory event in a scenario timeline.
/// </summary>
/// <param name="Source">The service or actor associated with the event.</param>
/// <param name="Message">The event message.</param>
public sealed record ScenarioEvent(
    string Source,
    string Message);
