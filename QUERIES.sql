/* =====================================================================
   PART B - SQL Server queries against the Part A schema

   Tables (created by the EF Core migration):
     Users      (Id, FullName, Email, PasswordHash, Role, CreatedAt)
     Products   (Id, Sku, Name, Price, StockQuantity, IsActive, CreatedAt, RowVersion)
     Orders     (Id, CustomerId, Status, SubTotal, Discount, Total, CreatedAt, UpdatedAt, ...)
     OrderItems (Id, OrderId, ProductId, Quantity, UnitPrice)

   Notes
   - Orders.Status is stored as text: 'Pending' | 'Confirmed' | 'Shipped' | 'Cancelled'.
   - All dates are stored in UTC, so SYSUTCDATETIME() is used instead of GETDATE().
   - Date filters are written as ranges (>= start AND < end) on the raw column so the
     IX_Orders_Status_CreatedAt index can be used (no YEAR(CreatedAt) = ... in WHERE).
   ===================================================================== */

USE StoreDb;
GO

/* ---------------------------------------------------------------------
   1. Top 5 customers by total spend (Confirmed + Shipped) in the last 30 days
   ---------------------------------------------------------------------
   Revenue = Orders.Total (after discount). Cancelled and Pending orders are
   not "spend" yet. Ties on spend are broken by customer id so the result is
   deterministic; use TOP (5) WITH TIES if tied customers should all be shown.
   This is the query implemented with LINQ in GET /api/reports/top-customers.
*/
DECLARE @Since datetime2 = DATEADD(DAY, -30, SYSUTCDATETIME());

SELECT TOP (5)
    u.Id            AS CustomerId,
    u.FullName,
    u.Email,
    COUNT(*)        AS OrderCount,
    SUM(o.Total)    AS TotalSpend
FROM dbo.Orders AS o
JOIN dbo.Users  AS u ON u.Id = o.CustomerId
WHERE o.Status IN (N'Confirmed', N'Shipped')
  AND o.CreatedAt >= @Since
GROUP BY u.Id, u.FullName, u.Email
ORDER BY TotalSpend DESC, u.Id;
GO


/* ---------------------------------------------------------------------
   2. Products that have never been ordered
   ---------------------------------------------------------------------
   NOT EXISTS instead of NOT IN: NOT IN returns no rows at all if the
   sub-query ever yields a NULL, and NOT EXISTS is usually planned as an
   anti semi join. Any order counts as "ordered", including cancelled ones.
*/
SELECT
    p.Id,
    p.Sku,
    p.Name,
    p.Price,
    p.StockQuantity,
    p.IsActive
FROM dbo.Products AS p
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.OrderItems AS oi
    WHERE oi.ProductId = p.Id
)
ORDER BY p.Id;
GO


/* ---------------------------------------------------------------------
   3. Monthly revenue for the current year with a running total
   ---------------------------------------------------------------------
   - Revenue = Confirmed + Shipped order totals.
   - Every month from January up to the current month is listed, months
     without sales show 0 (recursive CTE builds the month list).
   - Running total uses SUM() OVER with an explicit ROWS frame; the default
     RANGE frame is slower and behaves differently on ties.
*/
DECLARE @YearStart datetime2 = DATEFROMPARTS(YEAR(SYSUTCDATETIME()), 1, 1);
DECLARE @NextYear  datetime2 = DATEADD(YEAR, 1, @YearStart);
DECLARE @CurrentMonth int    = MONTH(SYSUTCDATETIME());

WITH Months AS (
    SELECT 1 AS MonthNo
    UNION ALL
    SELECT MonthNo + 1 FROM Months WHERE MonthNo < @CurrentMonth
),
MonthlyRevenue AS (
    SELECT
        MONTH(o.CreatedAt) AS MonthNo,
        COUNT(*)           AS OrderCount,
        SUM(o.Total)       AS Revenue
    FROM dbo.Orders AS o
    WHERE o.Status IN (N'Confirmed', N'Shipped')
      AND o.CreatedAt >= @YearStart
      AND o.CreatedAt <  @NextYear
    GROUP BY MONTH(o.CreatedAt)
)
SELECT
    DATEFROMPARTS(YEAR(@YearStart), m.MonthNo, 1)                      AS MonthStart,
    DATENAME(MONTH, DATEFROMPARTS(YEAR(@YearStart), m.MonthNo, 1))     AS [Month],
    COALESCE(r.OrderCount, 0)                                          AS OrderCount,
    COALESCE(r.Revenue, 0)                                             AS Revenue,
    SUM(COALESCE(r.Revenue, 0)) OVER (
        ORDER BY m.MonthNo
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)              AS RunningTotal
FROM Months AS m
LEFT JOIN MonthlyRevenue AS r ON r.MonthNo = m.MonthNo
ORDER BY m.MonthNo
OPTION (MAXRECURSION 12);
GO


/* ---------------------------------------------------------------------
   4. Each customer's second-highest order total
   ---------------------------------------------------------------------
   - Cancelled orders are ignored (they were never really bought).
   - ROW_NUMBER is used, so a customer with totals 500, 500 gets 500 as the
     second-highest. Customers with fewer than 2 orders have no row number 2
     and drop out automatically.
   - If "second-highest" should mean the second *distinct* amount, swap
     ROW_NUMBER for DENSE_RANK (then 500, 500 would return nothing).
*/
WITH RankedOrders AS (
    SELECT
        o.CustomerId,
        o.Id     AS OrderId,
        o.Total,
        o.CreatedAt,
        ROW_NUMBER() OVER (
            PARTITION BY o.CustomerId
            ORDER BY o.Total DESC, o.Id) AS RowNo
    FROM dbo.Orders AS o
    WHERE o.Status <> N'Cancelled'
)
SELECT
    u.Id        AS CustomerId,
    u.FullName,
    r.OrderId,
    r.Total     AS SecondHighestTotal,
    r.CreatedAt AS OrderDate
FROM RankedOrders AS r
JOIN dbo.Users    AS u ON u.Id = r.CustomerId
WHERE r.RowNo = 2
ORDER BY SecondHighestTotal DESC, u.Id;
GO


/* ---------------------------------------------------------------------
   5. Products whose stock is below the average quantity sold per order
   ---------------------------------------------------------------------
   - "Quantity sold per order" = total quantity of the product in one order
     (summed first in case a product appears on more than one line).
   - Cancelled orders are not counted as sold.
   - Quantity is an int, so it is cast before AVG - AVG(int) in SQL Server
     does integer division (AVG of 1 and 2 would be 1, not 1.5).
   - Products that were never sold are not returned (no average to compare).
*/
WITH QuantityPerOrder AS (
    SELECT
        oi.ProductId,
        oi.OrderId,
        SUM(oi.Quantity) AS Quantity
    FROM dbo.OrderItems AS oi
    JOIN dbo.Orders     AS o ON o.Id = oi.OrderId
    WHERE o.Status <> N'Cancelled'
    GROUP BY oi.ProductId, oi.OrderId
),
AverageSold AS (
    SELECT
        ProductId,
        COUNT(*)                                    AS OrdersWithProduct,
        AVG(CAST(Quantity AS decimal(18, 2)))       AS AvgQuantityPerOrder
    FROM QuantityPerOrder
    GROUP BY ProductId
)
SELECT
    p.Id,
    p.Sku,
    p.Name,
    p.StockQuantity,
    CAST(a.AvgQuantityPerOrder AS decimal(18, 2)) AS AvgQuantityPerOrder,
    a.OrdersWithProduct
FROM dbo.Products AS p
JOIN AverageSold  AS a ON a.ProductId = p.Id
WHERE p.StockQuantity < a.AvgQuantityPerOrder
ORDER BY (a.AvgQuantityPerOrder - p.StockQuantity) DESC, p.Id;
GO
