namespace VirtualActors.Tests;

using Xunit;

/// <summary>
/// Collection fixture definition for Orleans cluster tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OrleansClusterFixtureDefinition : ICollectionFixture<OrleansClusterFixture> {
    /// <summary>
    /// The collection name.
    /// </summary>
    public const string Name = $"OrleansCluster";
}
