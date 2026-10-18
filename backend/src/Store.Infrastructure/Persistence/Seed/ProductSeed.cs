namespace Store.Infrastructure.Persistence.Seed;

/// <summary>
/// 15 catalogue products, inserted by the initial migration (HasData).
/// Anonymous objects are used so the rowversion column is left to SQL Server.
/// </summary>
internal static class ProductSeed
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static object[] Products =>
    [
        Item(1, "LAP-1001", "Lenovo IdeaPad Slim 5 Laptop", 58999.00m, 12),
        Item(2, "LAP-1002", "HP Pavilion 14 Laptop", 64990.00m, 8),
        Item(3, "MOB-2001", "Samsung Galaxy M35 5G", 17999.00m, 25),
        Item(4, "MOB-2002", "Redmi Note 13 Pro", 23999.00m, 20),
        Item(5, "AUD-3001", "boAt Rockerz 450 Headphones", 1499.00m, 60),
        Item(6, "AUD-3002", "Sony WH-1000XM5 Headphones", 29990.00m, 5),
        Item(7, "AUD-3003", "JBL Flip 6 Bluetooth Speaker", 9999.00m, 15),
        Item(8, "ACC-4001", "Logitech MX Master 3S Mouse", 8995.00m, 30),
        Item(9, "ACC-4002", "Mechanical Keyboard (Blue Switch)", 3499.00m, 40),
        Item(10, "ACC-4003", "USB-C 65W GaN Charger", 1899.00m, 75),
        Item(11, "ACC-4004", "1TB Portable SSD", 7499.00m, 18),
        Item(12, "MON-5001", "Dell 27\" 4K Monitor", 32499.00m, 6),
        Item(13, "WEA-6001", "Noise ColorFit Smartwatch", 2499.00m, 50),
        Item(14, "TAB-7001", "Apple iPad 10th Gen", 34900.00m, 1),
        Item(15, "CAM-8001", "Webcam Full HD 1080p", 2199.00m, 0)
    ];

    private static object Item(int id, string sku, string name, decimal price, int stock) => new
    {
        Id = id,
        Sku = sku,
        Name = name,
        Price = price,
        StockQuantity = stock,
        IsActive = true,
        CreatedAt = SeededAt
    };
}
