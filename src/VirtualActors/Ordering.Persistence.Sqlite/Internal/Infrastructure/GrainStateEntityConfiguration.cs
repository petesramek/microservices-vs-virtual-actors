namespace Ordering.Persistence.Sqlite.Internal.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configures the persisted Orleans grain state entity.
/// </summary>
internal sealed class GrainStateEntityConfiguration
    : IEntityTypeConfiguration<GrainStateEntity> {
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GrainStateEntity> builder) {
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
            .HasMaxLength(150)
            .IsRequired();

        builder
            .Property(entity => entity.ProviderName)
            .HasMaxLength(150)
            .IsRequired();

        builder
            .Property(entity => entity.StateName)
            .HasMaxLength(150)
            .IsRequired();

        builder
            .Property(entity => entity.GrainType)
            .HasMaxLength(512)
            .IsRequired();

        builder
            .Property(entity => entity.GrainId)
            .HasMaxLength(512)
            .IsRequired();

        builder
            .Property(entity => entity.Payload)
            .IsRequired();

        builder
            .Property(entity => entity.Version)
            .IsConcurrencyToken();

        builder
            .Property(entity => entity.ModifiedUtc)
            .IsRequired();
    }
}
