using Microsoft.EntityFrameworkCore;
using Payments.Api.Models;

namespace Payments.Api.Data;

/// <summary>
/// Payments service database context.
/// </summary>
/// <param name="options">The context options.</param>
public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets payment attempts.
    /// </summary>
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentAttempt>(entity =>
        {
            entity.HasKey(payment => payment.PaymentId);
            entity.Property(payment => payment.CustomerId).HasMaxLength(100);
            entity.Property(payment => payment.IdempotencyKey).HasMaxLength(200);
            entity.Property(payment => payment.Reason).HasMaxLength(100);
            entity.HasIndex(payment => payment.IdempotencyKey).IsUnique();
        });
    }
}
