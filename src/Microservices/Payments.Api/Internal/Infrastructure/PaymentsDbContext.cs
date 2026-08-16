namespace Payments.Api.Internal.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Payments.Api.Models;

/// <summary>
/// Provides Entity Framework Core access to payment-attempt persistence.
/// </summary>
/// <param name="options">
/// The options that configure the database provider and context behavior.
/// </param>
public sealed class PaymentsDbContext(
    DbContextOptions<PaymentsDbContext> options)
    : DbContext(options) {
    /// <summary>
    /// Gets the payment attempts tracked by this context.
    /// </summary>
    /// <value>
    /// The set used to query and persist <see cref="PaymentAttempt"/> entities.
    /// </value>
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    /// <summary>
    /// Configures the payment-attempt entity model.
    /// </summary>
    /// <param name="modelBuilder">
    /// The builder used to configure entity mappings, constraints, and indexes.
    /// </param>
    /// <remarks>
    /// Payment identifiers are primary keys. Idempotency keys are unique across
    /// the payment-attempt table, and bounded string lengths are enforced for
    /// customer identifiers, idempotency keys, and failure reasons.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .ApplyConfiguration(new PaymentAttemptEntityConfiguration());
    }
}
