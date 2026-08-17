namespace Hosting.AppHost.Resources;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using global::Observability.Health;
using global::Observability.Health.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using FrameworkHealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

/// <summary>
/// Provides methods for adding health-aggregating visual groups to an Aspire
/// application model.
/// </summary>
internal static class HealthGroupResourceExtensions {
    /// <summary>
    /// Adds a visual resource group whose Dashboard state is derived from its
    /// child project resources.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="healthStatusEvaluator">
    /// The evaluator used to aggregate child resource health statuses.
    /// </param>
    /// <param name="name">The resource name used by the application model.</param>
    /// <param name="displayName">The name displayed in the Aspire Dashboard.</param>
    /// <param name="children">
    /// The project resources whose health contributes to the group state.
    /// </param>
    /// <returns>The builder for the added health group resource.</returns>
    /// <remarks>
    /// The group is excluded from the deployment manifest and exists only as a
    /// visual Dashboard resource. Each child is assigned the group as its
    /// parent. Child state changes are monitored for the lifetime of the
    /// resource initialization context.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="children"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="displayName"/> is empty or
    /// whitespace, or <paramref name="children"/> contains no resources.
    /// </exception>
    public static IResourceBuilder<HealthGroupResource> AddHealthGroup(
        this IDistributedApplicationBuilder builder,
        IHealthStatusEvaluator healthStatusEvaluator,
        string name,
        string displayName,
        params IResourceBuilder<ProjectResource>[] children) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(healthStatusEvaluator);
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
                    healthStatusEvaluator,
                    cancellationToken);

                return Task.CompletedTask;
            });

        foreach (IResourceBuilder<ProjectResource> child in children) {
            child.WithParentRelationship(resource);
        }

        return resourceBuilder;
    }

    /// <summary>
    /// Monitors child resource snapshots and publishes the resulting aggregate
    /// state for the health group.
    /// </summary>
    /// <param name="group">The health group to update.</param>
    /// <param name="children">The resources whose snapshots are monitored.</param>
    /// <param name="notificationService">
    /// The service used to observe child changes and publish group updates.
    /// </param>
    /// <param name="healthStatusEvaluator"></param>
    /// <param name="cancellationToken">
    /// The token that stops monitoring when resource initialization ends.
    /// </param>
    /// <returns>A task that represents the monitoring loop.</returns>
    /// <remarks>
    /// The most recent snapshot for each child is retained. Children without a
    /// snapshot are treated as starting once at least one child snapshot has
    /// been observed.
    /// </remarks>
    private static async Task MonitorChildrenAsync(
        HealthGroupResource group,
        IReadOnlyCollection<IResource> children,
        ResourceNotificationService notificationService,
        IHealthStatusEvaluator healthStatusEvaluator,
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
                childSnapshots,
                healthStatusEvaluator);

            await notificationService
                .PublishUpdateAsync(
                    group,
                    snapshot => snapshot with {
                        State = CreateStateSnapshot(state),
                    })
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Evaluates the aggregate state from the latest available child snapshots.
    /// </summary>
    /// <param name="childNames">The complete set of child resource names.</param>
    /// <param name="childSnapshots">
    /// The latest snapshots received for child resources.
    /// </param>
    /// <param name="healthStatusEvaluator"></param>
    /// <returns>The aggregate health group state.</returns>
    /// <remarks>
    /// Returns <see cref="HealthGroupState.Unknown"/> until any child snapshot
    /// is available. After monitoring begins, a child without a snapshot is
    /// evaluated as starting.
    /// </remarks>
    private static HealthGroupState EvaluateState(
        IReadOnlyCollection<string> childNames,
        IReadOnlyDictionary<string, CustomResourceSnapshot> childSnapshots,
        IHealthStatusEvaluator healthStatusEvaluator) {
        if (childSnapshots.Count == 0) {
            return HealthGroupState.Unknown;
        }

        HealthStatus[] statuses = childNames
            .Select(name => ResolveStatus(name, childSnapshots))
            .ToArray();

        return MapGroupState(healthStatusEvaluator.Evaluate(statuses));
    }

    /// <summary>
    /// Resolves one child's observability health status from its latest Aspire
    /// resource snapshot.
    /// </summary>
    /// <param name="childName">The child resource name.</param>
    /// <param name="childSnapshots">
    /// The latest snapshots received for child resources.
    /// </param>
    /// <returns>
    /// The resolved health status. A missing or nonterminal snapshot without a
    /// health status resolves to <see cref="HealthStatus.Starting"/>.
    /// </returns>
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
            _ => HealthStatus.Unknown,
        };
    }

    /// <summary>
    /// Determines whether a snapshot represents a resource that is still
    /// progressing toward a terminal state without reporting health.
    /// </summary>
    /// <param name="snapshot">The resource snapshot to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when no health status is available and the
    /// resource state is nonterminal; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsStarting(CustomResourceSnapshot snapshot) {
        string? state = snapshot.State?.Text;

        return snapshot.HealthStatus is null
            && !KnownResourceStates.TerminalStates.Contains(state, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps an observability health status to the corresponding Dashboard group
    /// state.
    /// </summary>
    /// <param name="status">The observability health status to map.</param>
    /// <returns>The corresponding health group state.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="status"/> is not a supported observability health status.
    /// </exception>
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

    /// <summary>
    /// Creates the Aspire Dashboard state text and style for an aggregate health
    /// group state.
    /// </summary>
    /// <param name="state">The aggregate health group state.</param>
    /// <returns>The Dashboard resource state snapshot.</returns>
    /// <remarks>
    /// Unsupported values are displayed as unknown so the Dashboard remains
    /// usable if the enum is extended without adding a dedicated visual style.
    /// </remarks>
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
