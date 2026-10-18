using Microsoft.EntityFrameworkCore;

namespace Store.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class PagingExtensions
{
    public const int MaxPageSize = 100;

    // Keeps (page - 1) * pageSize inside int range - ?page=2147483647 would otherwise overflow
    // into a negative Skip() and end up as a 500.
    public const int MaxPage = int.MaxValue / MaxPageSize;

    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        page = Math.Clamp(page, 1, MaxPage);
        pageSize = pageSize switch
        {
            < 1 => 10,
            > MaxPageSize => MaxPageSize,
            _ => pageSize
        };
        return (page, pageSize);
    }

    /// <summary>Runs COUNT and the page query against the database.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        (page, pageSize) = Normalize(page, pageSize);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, total);
    }
}
