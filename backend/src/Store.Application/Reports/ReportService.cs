using Microsoft.EntityFrameworkCore;
using Store.Application.Abstractions;
using Store.Application.Common;
using Store.Domain.Enums;

namespace Store.Application.Reports;

public sealed record TopCustomerDto(int CustomerId, string FullName, string Email, int OrderCount, decimal TotalSpend);

public interface IReportService
{
    Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(int days, int top, CancellationToken cancellationToken);
}

public sealed class ReportService : IReportService
{
    public const int MaxDays = 365;

    private readonly IAppDbContext _db;
    private readonly TimeProvider _clock;

    public ReportService(IAppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Top customers by spend on Confirmed + Shipped orders. Aggregation, TOP and the join
    /// to Users all happen in one SQL statement - nothing is loaded into memory first.
    /// </summary>
    public async Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(int days, int top, CancellationToken cancellationToken)
    {
        if (days is < 1 or > MaxDays)
            throw new ValidationException($"days must be between 1 and {MaxDays}.");
        if (top is < 1 or > 100)
            throw new ValidationException("top must be between 1 and 100.");

        var since = _clock.GetUtcNow().UtcDateTime.AddDays(-days);

        var spendPerCustomer = _db.Orders
            .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped) && o.CreatedAt >= since)
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                TotalSpend = g.Sum(o => o.Total),
                OrderCount = g.Count()
            })
            .OrderByDescending(x => x.TotalSpend)
            .ThenBy(x => x.CustomerId)
            .Take(top);

        var query =
            from s in spendPerCustomer
            join u in _db.Users on s.CustomerId equals u.Id
            orderby s.TotalSpend descending, s.CustomerId
            select new TopCustomerDto(u.Id, u.FullName, u.Email, s.OrderCount, s.TotalSpend);

        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}
