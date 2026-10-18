using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredAt).HasColumnType("datetime2");
        builder.Property(m => m.ProcessedAt).HasColumnType("datetime2");
        builder.Property(m => m.LastError).HasMaxLength(2000);

        builder.HasIndex(m => m.Id)
            .HasFilter("[ProcessedAt] IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Unprocessed");
    }
}
