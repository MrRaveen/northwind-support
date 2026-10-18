using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Data.Configurations;

public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents", t =>
            t.HasCheckConstraint("CK_Agents_MaxOpenTickets", "[MaxOpenTickets] > 0"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FullName).HasMaxLength(200).IsRequired();

        builder.Property(a => a.Email).HasMaxLength(256).IsRequired();

        builder.Property(a => a.CreatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasIndex(a => a.Email).IsUnique().HasDatabaseName("UQ_Agents_Email");
        
        builder.Property(a => a.PasswordHash).HasMaxLength(256);

        builder.HasMany(a => a.Specializations)
            .WithOne()
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Specializations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
