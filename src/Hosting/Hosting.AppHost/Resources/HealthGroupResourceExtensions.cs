namespace Hosting.AppHost.Resources;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using global::Observability.Health;
using Microsoft.Extensions.DependencyInjection;
using FrameworkHealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

/// <summary>
/// Provides methods for adding health-aggregating visual groups to the Aspire application model.
/// </summary>
internal static class HealthGroupResourceExtensions {
    /// <summary>
    /// Adds a visual resource group whose state is derived from its child project resources.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="displayName">The display name shown in the Aspire Dashboard.</param>
    /// <param name="children">The project resources contained in the group.</param>
    /// <returns>The health group resource builder.</returns>
    public static IResourceBuilder<HealthGroupResource> AddHealthGroup(
        this IDistributedApplicationBuilder builder,
        string name,
        string displayName,
        params IResourceBuilder<ProjectResource>[] children) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(children);

        if (children.Length == 0) {
            throw new ArgumentException(
                "A health group must contain at least one child resource.",
                nameof(children));
        }

        var resource = new HealthGroupResource(name, displayName);
        IResource[] childResources = children
            .Select(child => (IResource)child.Resource)
            .ToArray();

        IResourceBuilder<HealthGroupResource> resourceBuilder = builder
            .AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot {
                ResourceType = displayName,
                State = CreateStateSnapshot(HealthGroupState.Unknown),
                Properties =
                [
                    new(
                        CustomResourceKnownProperties.Source,
                        "Aggregated child resource health"),
                ],
            })
            .ExcludeFromManifest()
            .OnInitializeResource((group, context, cancellationToken) => {
                ResourceNotificationService notificationService = context.Services
                    .GetRequiredService<ResourceNotificationService>();

                _ = MonitorChildrenAsync(
                    group,
                    childResources,
                    notificationService,
                    cancellationToken);

                return Task.CompletedTask;
            });

        foreach (IResourceBuilder<ProjectResource> child in children) {
            child.WithParentRelationship(resource);
        }

        return resourceBuilder;
    }

    private static async Task MonitorChildrenAsync(
        HealthGroupResource group,
        IReadOnlyCollection<IResource> children,
        ResourceNotificationService notificationService,
        CancellationToken cancellationToken) {
        HashSet<string> childNames = children
            .Select(child => child.Name)
            .ToHashSet(StringComparer.Ordinal);

        var childSnapshots = new Dictionary<string, CustomResourceSnapshot>(
            StringComparer.Ordinal);

        await foreach (ResourceEvent resourceEvent in notificationService
            .WatchAsync(cancellationToken)
            .ConfigureAwait(false)) {
            if (!childNames.Contains(resourceEvent.Resource.Name)) {
                continue;
            }

            childSnapshots[resourceEvent.Resource.Name] = resourceEvent.Snapshot;

            HealthGroupState state = EvaluateState(
                childNames,
                childSnapshots);

            await notificationService
                .PublishUpdateAsync(
                    group,
                    snapshot => snapshot with {
                        State = CreateStateSnapshot(state),
                    })
                .ConfigureAwait(false);
        }
    }

    private static HealthGroupState EvaluateState(
        IReadOnlyCollection<string> childNames,
        IReadOnlyDictionary<string, CustomResourceSnapshot> childSnapshots) {
        if (childSnapshots.Count == 0) {
            return HealthGroupState.Unknown;
        }

        HealthStatus[] statuses = childNames
            .Select(name => ResolveStatus(name, childSnapshots))
            .ToArray();

        return MapGroupState(HealthStatusEvaluator.Instance.Evaluate(statuses));
    }

    private static HealthStatus ResolveStatus(
        string childName,
        IReadOnlyDictionary<string, CustomResourceSnapshot> childSnapshots) {
        if (!childSnapshots.TryGetValue(
            childName,
            out CustomResourceSnapshot? snapshot)
            || IsStarting(snapshot)) {
            return HealthStatus.Starting;
        }

        return snapshot.HealthStatus switch {
            FrameworkHealthStatus.Healthy => HealthStatus.Healthy,
            FrameworkHealthStatus.Degraded => HealthStatus.Degraded,
            FrameworkHealthStatus.Unhealthy => HealthStatus.Unhealthy,
            null => HealthStatus.Unknown,
        };
    }

    private static bool IsStarting(CustomResourceSnapshot snapshot) {
        string? state = snapshot.State?.Text;

        return snapshot.HealthStatus is null
            && !KnownResourceStates.TerminalStates.Contains(state);
    }

    private static HealthGroupState MapGroupState(HealthStatus status) {
        return status switch {
            HealthStatus.Starting => HealthGroupState.Starting,
            HealthStatus.Healthy => HealthGroupState.Healthy,
            HealthStatus.Degraded => HealthGroupState.Degraded,
            HealthStatus.Unhealthy => HealthGroupState.Unhealthy,
            HealthStatus.Unknown => HealthGroupState.Unknown,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported observability health status."),
        };
    }

    private static ResourceStateSnapshot CreateStateSnapshot(
        HealthGroupState state) {
        return state switch {
            HealthGroupState.Starting => new(
                "Starting",
                KnownResourceStateStyles.Info),
            HealthGroupState.Healthy => new(
                "Healthy",
                KnownResourceStateStyles.Success),
            HealthGroupState.Degraded => new(
                "Degraded",
                KnownResourceStateStyles.Warn),
            HealthGroupState.Unhealthy => new(
                "Unhealthy",
                KnownResourceStateStyles.Error),
            _ => new(
                "Unknown",
                KnownResourceStateStyles.Info),
        };
    }
}
