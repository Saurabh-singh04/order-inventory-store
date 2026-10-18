using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Abstractions;
using Store.Application.Common;
using Store.Domain.Entities;

namespace Store.Application.Products;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, bool isAdmin, CancellationToken cancellationToken);
    Task<ProductDto> GetByIdAsync(int id, bool isAdmin, CancellationToken cancellationToken);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken);
}

public sealed class ProductService : IProductService
{
    private readonly IAppDbContext _db;
    private readonly IProductCache _cache;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IAppDbContext db,
        IProductCache cache,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        ILogger<ProductService> logger)
    {
        _db = db;
        _cache = cache;
        _dbErrors = dbErrors;
        _clock = clock;
        _logger = logger;
    }

    public Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, bool isAdmin, CancellationToken cancellationToken)
    {
        if (query.MinPrice is not null && query.MaxPrice is not null && query.MinPrice > query.MaxPrice)
            throw new ValidationException("minPrice cannot be greater than maxPrice.");

        var includeInactive = isAdmin && query.IncludeInactive;
        var (page, pageSize) = PagingExtensions.Normalize(query.Page, query.PageSize);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var sortBy = (query.SortBy ?? "name").ToLowerInvariant();
        var desc = string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase);

        var key = string.Join('|',
            "products",
            search?.ToLowerInvariant(),
            query.MinPrice?.ToString(CultureInfo.InvariantCulture),
            query.MaxPrice?.ToString(CultureInfo.InvariantCulture),
            query.InStock?.ToString(),
            sortBy,
            desc ? "desc" : "asc",
            page.ToString(CultureInfo.InvariantCulture),
            pageSize.ToString(CultureInfo.InvariantCulture),
            includeInactive ? "all" : "active");

        return _cache.GetOrCreateAsync(key, async ct =>
        {
            // Everything below is composed on IQueryable and executed by SQL Server.
            var products = _db.Products.AsNoTracking();

            if (!includeInactive)
                products = products.Where(p => p.IsActive);

            if (search is not null)
                products = products.Where(p => p.Name.Contains(search) || p.Sku.Contains(search));

            if (query.MinPrice is not null)
                products = products.Where(p => p.Price >= query.MinPrice);

            if (query.MaxPrice is not null)
                products = products.Where(p => p.Price <= query.MaxPrice);

            if (query.InStock == true)
                products = products.Where(p => p.StockQuantity > 0);
            else if (query.InStock == false)
                products = products.Where(p => p.StockQuantity == 0);

            products = (sortBy, desc) switch
            {
                ("price", false) => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
                ("price", true) => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
                ("stock", false) => products.OrderBy(p => p.StockQuantity).ThenBy(p => p.Id),
                ("stock", true) => products.OrderByDescending(p => p.StockQuantity).ThenBy(p => p.Id),
                ("newest", _) => products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
                (_, true) => products.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
                _ => products.OrderBy(p => p.Name).ThenBy(p => p.Id)
            };

            var rows = await products
                .Select(p => new ProductRow(p.Id, p.Sku, p.Name, p.Price, p.StockQuantity, p.IsActive, p.CreatedAt, p.RowVersion))
                .ToPagedResultAsync(page, pageSize, ct);

            return new PagedResult<ProductDto>(rows.Items.Select(ToDto).ToList(), rows.Page, rows.PageSize, rows.TotalCount);
        }, cancellationToken);
    }

    public async Task<ProductDto> GetByIdAsync(int id, bool isAdmin, CancellationToken cancellationToken)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null || (!product.IsActive && !isAdmin))
            throw new NotFoundException("Product", id);

        return ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(p => p.Sku == sku, cancellationToken))
            throw new ConflictException($"SKU '{sku}' already exists.", "sku_taken");

        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Price = Math.Round(request.Price, 2, MidpointRounding.AwayFromZero),
            StockQuantity = request.StockQuantity,
            IsActive = request.IsActive,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        _db.Products.Add(product);

        await SaveProductAsync(sku, cancellationToken);

        _logger.LogInformation("Product {ProductId} ({Sku}) created", product.Id, product.Sku);
        return ToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Product", id);

        byte[] clientVersion;
        try
        {
            clientVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationException("rowVersion is not a valid base64 string.");
        }

        var sku = request.Sku.Trim().ToUpperInvariant();
        if (sku != product.Sku && await _db.Products.AnyAsync(p => p.Sku == sku && p.Id != id, cancellationToken))
            throw new ConflictException($"SKU '{sku}' already exists.", "sku_taken");

        // Compare against the version the admin saw, not the one we just loaded.
        _db.Entry(product).Property(p => p.RowVersion).OriginalValue = clientVersion;

        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Price = Math.Round(request.Price, 2, MidpointRounding.AwayFromZero);
        product.StockQuantity = request.StockQuantity;
        product.IsActive = request.IsActive;

        try
        {
            await SaveProductAsync(sku, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "This product was changed by someone else (or by a new order). Reload it and try again.",
                "concurrency");
        }

        _logger.LogInformation("Product {ProductId} updated", product.Id);
        return ToDto(product);
    }

    private async Task SaveProductAsync(string sku, CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException && _dbErrors.IsUniqueConstraintViolation(ex))
        {
            throw new ConflictException($"SKU '{sku}' already exists.", "sku_taken");
        }

        _cache.Invalidate();
    }

    private sealed record ProductRow(int Id, string Sku, string Name, decimal Price, int StockQuantity, bool IsActive, DateTime CreatedAt, byte[] RowVersion);

    private static ProductDto ToDto(ProductRow p) =>
        new(p.Id, p.Sku, p.Name, p.Price, p.StockQuantity, p.IsActive, p.CreatedAt, Convert.ToBase64String(p.RowVersion ?? Array.Empty<byte>()));

    private static ProductDto ToDto(Product p) =>
        new(p.Id, p.Sku, p.Name, p.Price, p.StockQuantity, p.IsActive, p.CreatedAt, Convert.ToBase64String(p.RowVersion ?? Array.Empty<byte>()));
}
