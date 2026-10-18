using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <remarks>
/// A ticket refers to its customer, category and agent by id only (they are other aggregates),
/// but the database still enforces that those ids point at real rows.
/// </remarks>
public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", t =>
        {
            t.HasCheckConstraint("CK_Tickets_Priority", "[Priority] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_Tickets_Status", "[Status] BETWEEN 1 AND 5");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Reference).HasMaxLength(20).IsRequired();

        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();

        builder.Property(t => t.Description).HasMaxLength(4000).IsRequired();

        builder.Property(t => t.Priority).HasConversion<int>();

        builder.Property(t => t.Status).HasConversion<int>();

        builder.Property(t => t.CreatedAtUtc).HasColumnType("datetime2(3)");

        builder.Property(t => t.UpdatedAtUtc).HasColumnType("datetime2(3)");

        builder.Property(t => t.DueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(t => t.ResolvedAtUtc).HasColumnType("datetime2(3)");

        // BR-8: the window start is reset to now on each escalation so AtRisk % stays correct.
        // Backfilled to CreatedAtUtc for existing rows (see migration).
        builder.Property(t => t.SlaWindowStartUtc).HasColumnType("datetime2(3)");

        // A ticket is never silently detached from the rows that give it meaning.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(t => t.IsOpen);

        builder.HasIndex(t => t.Reference).IsUnique().HasDatabaseName("UQ_Tickets_Reference");

        // Covers the list screen's most common filter pair.
        builder.HasIndex(t => new { t.Status, t.Priority })
            .HasDatabaseName("IX_Tickets_Status_Priority")
            .IncludeProperties(t => new { t.DueAtUtc, t.AssignedAgentId });

        builder.HasIndex(t => t.CustomerId).HasDatabaseName("IX_Tickets_CustomerId");

        builder.HasIndex(t => t.CategoryId).HasDatabaseName("IX_Tickets_CategoryId");

        builder.HasIndex(t => t.AssignedAgentId)
            .HasDatabaseName("IX_Tickets_AssignedAgentId")
            .HasFilter("[AssignedAgentId] IS NOT NULL");

        builder.HasIndex(t => t.DueAtUtc).HasDatabaseName("IX_Tickets_DueAtUtc");
        // Add this to the end of TicketConfiguration.Configure
        builder.HasMany(t => t.Escalations)
            .WithOne()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tell EF Core to read the private backing field
        builder.Navigation(t => t.Escalations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
