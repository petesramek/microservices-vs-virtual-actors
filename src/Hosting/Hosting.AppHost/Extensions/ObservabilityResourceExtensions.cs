namespace Hosting.AppHost.Extensions;

using Hosting.ServiceDefaults.Observability;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Provides extensions that copy AppHost observability configuration into
/// .NET Aspire resource environments.
/// </summary>
/// <remarks>
/// Configuration is flattened into environment-variable names that use
/// double underscores to represent the .NET configuration hierarchy.
/// </remarks>
public static class ObservabilityResourceExtensions {
    /// <summary>
    /// Adds the configured observability settings to a resource as environment
    /// variables.
    /// </summary>
    /// <typeparam name="TResource">
    /// The type of resource that supports environment variables.
    /// </typeparam>
    /// <param name="resourceBuilder">
    /// The resource builder to configure.
    /// </param>
    /// <param name="configuration">
    /// The AppHost configuration containing the required observability section.
    /// </param>
    /// <returns>
    /// The supplied resource builder.
    /// </returns>
    /// <remarks>
    /// The section named by <see cref="ObservabilityOptions.SectionName"/> is
    /// required. Nested keys are separated by <c>__</c>, and only non-null leaf
    /// values are added. Empty sections and null-valued leaves produce no
    /// environment variable.
    ///
    /// <para>
    /// Values are copied when this method executes; subsequent configuration
    /// changes are not propagated automatically. The observability section
    /// should contain only values that are appropriate to expose to the
    /// resource process as environment variables.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="resourceBuilder"/> or <paramref name="configuration"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The observability configuration section is missing.
    /// </exception>
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

    /// <summary>
    /// Recursively adds the non-null leaves of a configuration section as
    /// environment variables on a resource.
    /// </summary>
    /// <typeparam name="TResource">
    /// The type of resource that supports environment variables.
    /// </typeparam>
    /// <param name="resourceBuilder">
    /// The resource builder that receives the environment variables.
    /// </param>
    /// <param name="section">
    /// The current configuration section to flatten.
    /// </param>
    /// <param name="environmentVariablePrefix">
    /// The environment-variable name assigned to the current section; child
    /// keys are appended with <c>__</c>.
    /// </param>
    /// <remarks>
    /// A section with children contributes only its descendants. A leaf is
    /// added only when its value is non-null.
    /// </remarks>
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