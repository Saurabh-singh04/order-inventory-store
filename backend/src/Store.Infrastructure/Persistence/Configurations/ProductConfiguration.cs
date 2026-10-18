using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;
using Store.Infrastructure.Persistence.Seed;

namespace Store.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            // Last line of defence: even a buggy code path can't push stock below zero.
            t.HasCheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0");
            t.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.CreatedAt).HasColumnType("datetime2");
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => new { p.IsActive, p.Price });

        builder.HasData(ProductSeed.Products);
    }
}
