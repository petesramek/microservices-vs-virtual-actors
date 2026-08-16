namespace Workbench.Contracts.Scenarios;

/// <summary>
/// Represents one explanatory event in a scenario execution timeline.
/// </summary>
/// <param name="Source">
/// The service, actor, or workflow component associated with the event.
/// </param>
/// <param name="Message">
/// The human-readable explanation of the event.
/// </param>
/// <remarks>
/// Timeline events are descriptive workbench output and should not contain
/// secrets, credentials, personal data, or other sensitive values.
/// </remarks>
public sealed record ScenarioEvent(
    string Source,
    string Message);
