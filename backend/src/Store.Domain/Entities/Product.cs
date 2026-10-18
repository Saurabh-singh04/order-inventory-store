using Store.Domain.Exceptions;

namespace Store.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    // SQL Server rowversion - used as the optimistic concurrency token.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool HasStock(int quantity) => StockQuantity >= quantity;

    public void ReserveStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        if (!HasStock(quantity))
            throw new DomainException($"Not enough stock for '{Name}'. Available: {StockQuantity}, requested: {quantity}.");

        StockQuantity -= quantity;
    }

    public void ReleaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        StockQuantity += quantity;
    }
}
