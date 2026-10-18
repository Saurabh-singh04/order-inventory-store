using System.ComponentModel.DataAnnotations;

namespace Store.Application.Products;

public sealed record ProductDto(
    int Id,
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    DateTime CreatedAt,
    string RowVersion);

public sealed class ProductQuery
{
    /// <summary>Matches product name or SKU.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxPrice { get; set; }

    /// <summary>true = only products with stock, false = only out of stock, null = all.</summary>
    public bool? InStock { get; set; }

    /// <summary>name | price | stock | newest</summary>
    public string? SortBy { get; set; }

    /// <summary>asc | desc</summary>
    public string? SortDir { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;

    /// <summary>Admin only - include inactive products.</summary>
    public bool IncludeInactive { get; set; }
}

public class CreateProductRequest
{
    [Required, StringLength(32, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "SKU may only contain letters, digits and '-'.")]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "10000000")]
    public decimal Price { get; set; }

    [Range(0, 1_000_000)]
    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class UpdateProductRequest : CreateProductRequest
{
    /// <summary>Base64 row version the client last saw - stale edits get 409.</summary>
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
