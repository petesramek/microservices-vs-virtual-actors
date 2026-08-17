using Observability.Topology.Definitions;

namespace Observability.Topology.Validation;

/// <summary>
/// Validates static observability topology definitions.
/// </summary>
/// <remarks>
/// Validation is non-throwing for malformed topology content and reports all
/// detected errors in a <see cref="TopologyValidationResult"/>. Identifiers are
/// compared using ordinal, case-sensitive semantics to match the topology
/// definition contract.
/// </remarks>
public sealed class TopologyValidator {
    /// <summary>
    /// Validates a topology definition and its cross-reference invariants.
    /// </summary>
    /// <param name="topology">The topology definition to validate.</param>
    /// <returns>
    /// A validation result containing every detected error in validation order.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="topology"/> is <see langword="null"/>.
    /// </exception>
    public static TopologyValidationResult Validate(
        TopologyDefinition topology) {
        ArgumentNullException.ThrowIfNull(topology);

        TopologyValidationResult result = new();

        if (!ValidateCollections(topology, result)) {
            return result;
        }

        ValidateNodes(topology, result);
        ValidateEdges(topology, result);
        ValidateGroups(topology, result);
        ValidateHealthSources(topology, result);

        return result;
    }

    /// <summary>
    /// Verifies that the topology's root collections are available.
    /// </summary>
    /// <param name="topology">The topology definition to inspect.</param>
    /// <param name="result">The result that receives validation errors.</param>
    /// <returns>
    /// <see langword="true"/> when all root collections are non-null; otherwise
    /// <see langword="false"/>.
    /// </returns>
    private static bool ValidateCollections(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        bool isValid = true;

        if (topology.Nodes is null) {
            result.AddError("The topology node collection is null.");
            isValid = false;
        }

        if (topology.Edges is null) {
            result.AddError("The topology edge collection is null.");
            isValid = false;
        }

        if (topology.Groups is null) {
            result.AddError("The topology group collection is null.");
            isValid = false;
        }

        return isValid;
    }

    /// <summary>
    /// Validates node identifiers, display names, kinds, and uniqueness.
    /// </summary>
    /// <param name="topology">The topology definition to inspect.</param>
    /// <param name="result">The result that receives validation errors.</param>
    private static void ValidateNodes(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        foreach (TopologyNodeDefinition node in topology.Nodes) {
            if (string.IsNullOrWhiteSpace(node.Id)) {
                result.AddError("A topology node has an empty identifier.");
            }

            if (string.IsNullOrWhiteSpace(node.DisplayName)) {
                result.AddError(
                    $"Node '{node.Id}' has an empty display name.");
            }

            if (!Enum.IsDefined(node.Kind)) {
                result.AddError(
                    $"Node '{node.Id}' has unsupported kind '{node.Kind}'.");
            }
        }

        IEnumerable<string> duplicateNodeIds = topology.Nodes
            .Where(static node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(static node => node.Id, StringComparer.Ordinal)
            .Where(static group => group.Skip(1).Any())
            .Select(static group => group.Key);

        foreach (string nodeId in duplicateNodeIds) {
            result.AddError(
                $"Duplicate node id '{nodeId}'.");
        }
    }

    /// <summary>
    /// Validates directed dependency endpoints, requirements, self-references,
    /// and uniqueness.
    /// </summary>
    /// <param name="topology">The topology definition to inspect.</param>
    /// <param name="result">The result that receives validation errors.</param>
    private static void ValidateEdges(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        HashSet<string> nodeIds = topology.Nodes
            .Where(static node => !string.IsNullOrWhiteSpace(node.Id))
            .Select(static node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (TopologyEdgeDefinition edge in topology.Edges) {
            if (string.IsNullOrWhiteSpace(edge.SourceNodeId)) {
                result.AddError("A topology edge has an empty source node id.");
            } else if (!nodeIds.Contains(edge.SourceNodeId)) {
                result.AddError(
                    $"Edge source '{edge.SourceNodeId}' does not exist.");
            }

            if (string.IsNullOrWhiteSpace(edge.TargetNodeId)) {
                result.AddError("A topology edge has an empty target node id.");
            } else if (!nodeIds.Contains(edge.TargetNodeId)) {
                result.AddError(
                    $"Edge target '{edge.TargetNodeId}' does not exist.");
            }

            if (!Enum.IsDefined(edge.Requirement)) {
                result.AddError(
                    $"Edge '{edge.SourceNodeId}->{edge.TargetNodeId}' has " +
                    $"unsupported requirement '{edge.Requirement}'.");
            }

            if (string.Equals(
                    edge.SourceNodeId,
                    edge.TargetNodeId,
                    StringComparison.Ordinal)) {
                result.AddError(
                    $"Self dependency '{edge.SourceNodeId}' is not allowed.");
            }

            if (edge.HealthEntryKey is not null
                && string.IsNullOrWhiteSpace(edge.HealthEntryKey)) {
                result.AddError(
                    $"Edge '{edge.SourceNodeId}->{edge.TargetNodeId}' has an " +
                    "empty health entry key.");
            }
        }

        IEnumerable<(string SourceNodeId, string TargetNodeId)> duplicateEdges =
            topology.Edges
                .Where(static edge =>
                    !string.IsNullOrWhiteSpace(edge.SourceNodeId)
                    && !string.IsNullOrWhiteSpace(edge.TargetNodeId))
                .GroupBy(static edge =>
                    (edge.SourceNodeId, edge.TargetNodeId))
                .Where(static group => group.Skip(1).Any())
                .Select(static group => group.Key);

        foreach ((string sourceNodeId, string targetNodeId) in duplicateEdges) {
            result.AddError(
                $"Duplicate edge '{sourceNodeId}->{targetNodeId}'.");
        }
    }

    /// <summary>
    /// Validates group identifiers, display names, members, and uniqueness.
    /// </summary>
    /// <param name="topology">The topology definition to inspect.</param>
    /// <param name="result">The result that receives validation errors.</param>
    private static void ValidateGroups(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        HashSet<string> nodeIds = topology.Nodes
            .Where(static node => !string.IsNullOrWhiteSpace(node.Id))
            .Select(static node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (TopologyGroupDefinition group in topology.Groups) {
            if (string.IsNullOrWhiteSpace(group.Id)) {
                result.AddError("A topology group has an empty identifier.");
            }

            if (string.IsNullOrWhiteSpace(group.DisplayName)) {
                result.AddError(
                    $"Group '{group.Id}' has an empty display name.");
            }

            if (group.NodeIds is null) {
                result.AddError(
                    $"Group '{group.Id}' has a null node collection.");
                continue;
            }

            foreach (string nodeId in group.NodeIds) {
                if (string.IsNullOrWhiteSpace(nodeId)) {
                    result.AddError(
                        $"Group '{group.Id}' contains an empty node id.");
                } else if (!nodeIds.Contains(nodeId)) {
                    result.AddError(
                        $"Group '{group.Id}' references unknown node " +
                        $"'{nodeId}'.");
                }
            }

            IEnumerable<string> duplicateMembers = group.NodeIds
                .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .GroupBy(static nodeId => nodeId, StringComparer.Ordinal)
                .Where(static members => members.Skip(1).Any())
                .Select(static members => members.Key);

            foreach (string nodeId in duplicateMembers) {
                result.AddError(
                    $"Group '{group.Id}' contains duplicate node '{nodeId}'.");
            }
        }

        IEnumerable<string> duplicateGroupIds = topology.Groups
            .Where(static group => !string.IsNullOrWhiteSpace(group.Id))
            .GroupBy(static group => group.Id, StringComparer.Ordinal)
            .Where(static group => group.Skip(1).Any())
            .Select(static group => group.Key);

        foreach (string groupId in duplicateGroupIds) {
            result.AddError(
                $"Duplicate group id '{groupId}'.");
        }
    }

    /// <summary>
    /// Validates node health-provider references and health-entry keys.
    /// </summary>
    /// <param name="topology">The topology definition to inspect.</param>
    /// <param name="result">The result that receives validation errors.</param>
    private static void ValidateHealthSources(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        HashSet<string> serviceNodeIds = topology.Nodes
            .Where(static node =>
                node.Kind == TopologyNodeKind.Service
                && !string.IsNullOrWhiteSpace(node.Id))
            .Select(static node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (TopologyNodeDefinition node in topology.Nodes) {
            if (node.HealthSource is null) {
                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    node.HealthSource.ProviderNodeId)) {
                result.AddError(
                    $"Node '{node.Id}' has an empty health provider id.");
            } else if (!serviceNodeIds.Contains(
                           node.HealthSource.ProviderNodeId)) {
                result.AddError(
                    $"Health provider " +
                    $"'{node.HealthSource.ProviderNodeId}' is not a service " +
                    "node.");
            }

            if (string.IsNullOrWhiteSpace(node.HealthSource.EntryKey)) {
                result.AddError(
                    $"Node '{node.Id}' has an empty health entry key.");
            }
        }
    }
}
