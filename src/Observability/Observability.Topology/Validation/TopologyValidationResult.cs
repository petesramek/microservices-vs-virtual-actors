namespace Observability.Topology.Validation;

/// <summary>
/// Collects errors produced while validating a topology definition.
/// </summary>
/// <remarks>
/// A newly created result is valid. Adding any validation error changes
/// <see cref="IsValid"/> to <see langword="false"/>. Errors are retained in the
/// order in which validation rules report them.
/// </remarks>
public sealed class TopologyValidationResult {
    /// <summary>
    /// Stores validation errors in reporting order.
    /// </summary>
    private readonly List<string> _errors = [];

    /// <summary>
    /// Gets the validation errors in reporting order.
    /// </summary>
    /// <value>A read-only view of the collected validation errors.</value>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Gets a value indicating whether validation completed without errors.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when <see cref="Errors"/> is empty; otherwise
    /// <see langword="false"/>.
    /// </value>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Adds a validation error to the result.
    /// </summary>
    /// <param name="error">The non-empty validation error message.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="error"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is empty or consists only of white-space
    /// characters.
    /// </exception>
    public void AddError(string error) {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        _errors.Add(error);
    }
}
