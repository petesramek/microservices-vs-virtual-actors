using Observability.Topology.Definitions;

namespace Observability.Topology.Validation;

/// <summary>
/// Validates topology definitions.
/// </summary>
public sealed class TopologyValidator {
    public TopologyValidationResult Validate(
        TopologyDefinition topology) {
        ArgumentNullException.ThrowIfNull(topology);

        var result = new TopologyValidationResult();

        ValidateNodes(topology, result);
        ValidateEdges(topology, result);
        ValidateGroups(topology, result);
        ValidateHealthSources(topology, result);

        return result;
    }

    private static void ValidateNodes(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        var duplicateNodeIds = topology.Nodes
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key);

        foreach (var nodeId in duplicateNodeIds) {
            result.AddError(
                $"Duplicate node id '{nodeId}'.");
        }
    }

    private static void ValidateEdges(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        var nodeIds = topology.Nodes
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in topology.Edges) {
            if (!nodeIds.Contains(edge.SourceNodeId)) {
                result.AddError(
                    $"Edge source '{edge.SourceNodeId}' does not exist.");
            }

            if (!nodeIds.Contains(edge.TargetNodeId)) {
                result.AddError(
                    $"Edge target '{edge.TargetNodeId}' does not exist.");
            }

            if (string.Equals(
                    edge.SourceNodeId,
                    edge.TargetNodeId,
                    StringComparison.OrdinalIgnoreCase)) {
                result.AddError(
                    $"Self dependency '{edge.SourceNodeId}' is not allowed.");
            }
        }

        var duplicates = topology.Edges
            .GroupBy(
                x => $"{x.SourceNodeId}->{x.TargetNodeId}",
                StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1);

        foreach (var duplicate in duplicates) {
            result.AddError(
                $"Duplicate edge '{duplicate.Key}'.");
        }
    }

    private static void ValidateGroups(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        var nodeIds = topology.Nodes
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var duplicateGroups = topology.Groups
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key);

        foreach (var groupId in duplicateGroups) {
            result.AddError(
                $"Duplicate group id '{groupId}'.");
        }

        foreach (var group in topology.Groups) {
            foreach (var nodeId in group.NodeIds) {
                if (!nodeIds.Contains(nodeId)) {
                    result.AddError(
                        $"Group '{group.Id}' references unknown node '{nodeId}'.");
                }
            }
        }
    }

    private static void ValidateHealthSources(
        TopologyDefinition topology,
        TopologyValidationResult result) {
        var serviceNodes = topology.Nodes
            .Where(x => x.Kind == TopologyNodeKind.Service)
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var node in topology.Nodes) {
            if (node.HealthSource is null) {
                continue;
            }

            if (!serviceNodes.Contains(node.HealthSource.ProviderNodeId)) {
                result.AddError(
                    $"Health provider '{node.HealthSource.ProviderNodeId}' is not a service node.");
            }

            if (string.IsNullOrWhiteSpace(
                    node.HealthSource.EntryKey)) {
                result.AddError(
                    $"Node '{node.Id}' has an empty health entry key.");
            }
        }
    }
}