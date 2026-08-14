namespace Observability.Topology.Validation;

/// <summary>
/// Represents topology validation results.
/// </summary>
public sealed class TopologyValidationResult {
    private readonly List<string> _errors = [];

    /// <summary>
    /// Validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Indicates whether validation passed.
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Adds a validation error.
    /// </summary>
    public void AddError(string error) {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        _errors.Add(error);
    }
}