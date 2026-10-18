using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromPriority).HasConversion<int>();
        builder.Property(e => e.ToPriority).HasConversion<int>();

        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.EscalatedBy).HasMaxLength(100).IsRequired();

        // This is the index explicitly required by Task 2.3D to optimize the GET history query
        builder.HasIndex(e => e.TicketId).HasDatabaseName("IX_TicketEscalations_TicketId");
    }
}