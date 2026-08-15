using Hosting.ServiceDefaults.Observability;
using Microsoft.Extensions.Configuration;

namespace Workbench.AppHost.Extensions;

/// <summary>
/// Provides observability configuration for Aspire resources.
/// </summary>
public static class ObservabilityResourceExtensions {
    /// <summary>
    /// Passes the AppHost Observability configuration section to a resource
    /// through environment variables.
    /// </summary>
    /// <typeparam name="TResource">
    /// The type of resource receiving the configuration.
    /// </typeparam>
    /// <param name="resourceBuilder">
    /// The resource builder receiving the configuration.
    /// </param>
    /// <param name="configuration">
    /// The AppHost configuration.
    /// </param>
    /// <returns>
    /// The supplied resource builder.
    /// </returns>
    public static IResourceBuilder<TResource> WithObservabilityConfiguration<TResource>(
        this IResourceBuilder<TResource> resourceBuilder,
        IConfiguration configuration)
        where TResource : IResourceWithEnvironment {
        ArgumentNullException.ThrowIfNull(resourceBuilder);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetRequiredSection(ObservabilityOptions.SectionName);

        AddSection(
            resourceBuilder,
            section,
            environmentVariablePrefix: ObservabilityOptions.SectionName);

        return resourceBuilder;
    }

    private static void AddSection<TResource>(
        IResourceBuilder<TResource> resourceBuilder,
        IConfigurationSection section,
        string environmentVariablePrefix)
        where TResource : IResourceWithEnvironment {
        var children = section.GetChildren().ToArray();

        if (children.Length == 0) {
            if (section.Value is not null) {
                resourceBuilder.WithEnvironment(
                    environmentVariablePrefix,
                    section.Value);
            }

            return;
        }

        foreach (var child in children) {
            AddSection(
                resourceBuilder,
                child,
                $"{environmentVariablePrefix}__{child.Key}");
        }
    }
}