namespace Ordering.Persistence.Sqlite.Internal.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configures the SQLite persistence mapping for
/// <see cref="GrainStateEntity"/>.
/// </summary>
internal sealed class GrainStateEntityConfiguration
    : IEntityTypeConfiguration<GrainStateEntity> {
    /// <summary>
    /// Defines the maximum length of service, provider, and state names.
    /// </summary>
    private const int NameMaxLength = 150;

    /// <summary>
    /// Defines the maximum length of grain type and grain identifiers.
    /// </summary>
    private const int GrainIdentifierMaxLength = 512;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GrainStateEntity> builder) {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("GrainStates");

        builder.HasKey(entity => new {
            entity.ServiceId,
            entity.ProviderName,
            entity.StateName,
            entity.GrainType,
            entity.GrainId,
        });

        builder
            .Property(entity => entity.ServiceId)
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder
            .Property(entity => entity.ProviderName)
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder
            .Property(entity => entity.StateName)
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder
            .Property(entity => entity.GrainType)
            .HasMaxLength(GrainIdentifierMaxLength)
            .IsRequired();

        builder
            .Property(entity => entity.GrainId)
            .HasMaxLength(GrainIdentifierMaxLength)
            .IsRequired();

        builder
            .Property(entity => entity.Payload)
            .IsRequired();

        builder
            .Property(entity => entity.Version)
            .ValueGeneratedNever()
            .IsConcurrencyToken();

        builder
            .Property(entity => entity.ModifiedUtc)
            .IsRequired();
    }
}
