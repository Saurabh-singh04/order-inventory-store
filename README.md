# Online Store – Order & Inventory API + Angular client

Machine test for the Full-Stack Developer (C# / .NET) role.

- **Backend:** ASP.NET Core Web API (.NET 8), EF Core 8 (code first), SQL Server
- **Frontend:** Angular 19 (standalone components, signals, reactive forms)
- **Extras:** SignalR live order status, transactional outbox, Docker Compose, GitHub Actions

```
backend/
  src/
    Store.Domain           entities, order state machine, discount rules (no dependencies)
    Store.Application      use cases (services), DTOs, interfaces, app exceptions
    Store.Infrastructure   EF Core DbContext, migrations, seeding, JWT, caching, background jobs
    Store.Api              controllers, auth/rate limiting setup, ProblemDetails, Swagger, SignalR hub
  tests/
    Store.UnitTests        xUnit – rules 1, 3, 4, 5 (+ auto-cancel job)
    Store.IntegrationTests WebApplicationFactory + real SQL Server – rule 2 race, idempotency race, auth
frontend/                    Angular app
QUERIES.sql                Part B
REVIEW.md                  Part C
docker-compose.yml
```

---

## 1. Running it

### Prerequisites

- .NET 8 SDK (or newer SDK that can target net8.0)
- SQL Server (local instance, Developer/Express, or the Docker image)
- Node.js 20+ (22 recommended) for the client

### Backend

The default connection string uses a local SQL Server with Windows authentication:
`Server=localhost;Database=StoreDb;Trusted_Connection=True;TrustServerCertificate=True`.
To use something else, override it without touching the repo:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Default" "Server=.;Database=StoreDb;User Id=sa;Password=...;TrustServerCertificate=True" --project src/Store.Api
dotnet user-secrets set "Jwt:Key" "<any random string of 32+ characters>" --project src/Store.Api
```

(If `Jwt:Key` is not set and the environment is Development, the API generates a random key at
startup and logs a warning – everything works, tokens just don't survive a restart. Outside
Development a missing key stops the app.)

```bash
cd backend
dotnet run --project src/Store.Api --launch-profile http
```

- API: http://localhost:5080
- Swagger: http://localhost:5080/swagger (log in with `/api/auth/login`, click **Authorize**, paste `accessToken`)

On startup the API applies migrations and seeds the users (Development only), so a fresh database
is ready to use.

### Frontend (Angular)

```bash
cd frontend
npm install
npm start          # ng serve on http://localhost:4200
```

`ng serve` proxies `/api` and `/hubs` to `http://localhost:5080` (`frontend/proxy.conf.json`), so there is
no CORS setup needed in development.

### Docker (bonus)

```bash
cp .env.example .env      # set your own SQL and JWT values
docker compose up --build
```

- Frontend: http://localhost:8080 (nginx also proxies `/api` and `/hubs` to the API)
- API / Swagger: http://localhost:5080/swagger
- SQL Server: `localhost,14333`

---

## 2. Migrations

The `dotnet-ef` tool is pinned in `backend/.config/dotnet-tools.json`.

```bash
cd backend
dotnet tool restore

# apply migrations (the API also does this on startup)
dotnet ef database update -p src/Store.Infrastructure -s src/Store.Infrastructure

# add a new migration
dotnet ef migrations add <Name> -p src/Store.Infrastructure -s src/Store.Infrastructure -o Persistence/Migrations
```

A design-time factory (`DesignTimeDbContextFactory`) lets the tools work without booting the API.
It reads `ConnectionStrings__Default` from the environment, otherwise uses the local default.

Seed data:
- **15 products** – part of the `InitialCreate` migration (`HasData`), so they exist as soon as the schema does.
- **Users (1 admin, 3 customers)** – inserted at startup by `DatabaseInitializer`, because password
  hashes are salted and can't be static migration data. Passwords come from configuration (`Seed:*`).

## 3. Tests

```bash
cd backend
dotnet test tests/Store.UnitTests
dotnet test tests/Store.IntegrationTests

cd ../frontend
npm test            # Karma, watches
npm run test:ci     # single headless run
```

**Integration tests need SQL Server.** Rule 2 depends on `rowversion` and a filtered unique index,
which the EF in-memory provider doesn't have, so the tests run against a real SQL Server.
Each run creates a database named `StoreDb_Tests_<guid>` and drops it at the end. The server comes
from the `STORE_TEST_SQL` environment variable (database name is replaced), default
`Server=localhost;Trusted_Connection=True;TrustServerCertificate=True`. CI uses a SQL Server
service container.

Current results:

| Suite | Tests |
|---|---|
| Store.UnitTests | 40 passing (DiscountCalculator, status transitions, place order, idempotency, status change + auto-cancel) |
| Store.IntegrationTests | 18 passing (last-unit race ×5 rounds → one 201 / one 409, parallel same-key requests, rate limit, ownership, 400 on invalid transition, role checks, refresh rotation + reuse detection, parallel register/refresh, paging edge cases, DB health check) |
| Angular (Karma/Jasmine) | 19 passing (interceptor refresh/retry, auth token refresh for SignalR, cart service, catalogue URL filters, error mapping) |
| Postman / Newman | 65 requests, 124 assertions passing against the running API |

### Postman collection (manual / end-to-end testing)

`postman/` contains a ready-to-import collection and environment:

- `Store.postman_collection.json` – every endpoint with real JSON bodies and test scripts, grouped as
  Health → Auth → Products → Orders (rules 1, 3) → Status transitions (rule 4) → Reports → Rate limit.
  Tokens, ids and row versions are captured into collection variables as the run goes, so it works
  top to bottom without editing anything.
- `Store.local.postman_environment.json` – `baseUrl` (`http://localhost:5080`) and the seeded logins.

**[docs/API_REFERENCE.md](docs/API_REFERENCE.md)** lists every endpoint (same order as Swagger) with the
request JSON and every response it can return – all real bodies captured from a run against the local API,
including the last-unit race (201 + 409). Swagger's "Try it out" is also pre-filled with working example
bodies, and each endpoint lists all of its status codes.

Import both into Postman and run the collection with the Collection Runner, or from the command line
while the API is running:

```bash
npx newman run postman/Store.postman_collection.json -e postman/Store.local.postman_environment.json
```

Each run uses a unique suffix for SKUs/emails, so it can be repeated against the same database. The
login endpoint is limited to 5 calls a minute and the last folder deliberately hits that limit, so
wait about a minute between two full runs.

## 4. Seeded logins

Development only (configured in `appsettings.Development.json` / `.env`):

| Role | Email | Password |
|---|---|---|
| Admin | admin@store.local | Admin@123 |
| Customer | aarav@store.local | Customer@123 |
| Customer | priya@store.local | Customer@123 |
| Customer | rohan@store.local | Customer@123 |

Product `TAB-7001` (iPad) is seeded with **1 unit** and `CAM-8001` (webcam) with **0**, so the
out-of-stock and last-unit cases can be tried from the UI straight away.

---

## 5. Architecture

```
Store.Api ──► Store.Infrastructure ──► Store.Application ──► Store.Domain
   (HTTP)        (EF Core, JWT, jobs)      (use cases)          (rules)
```

- **Domain** – plain entities plus the rules that don't need a database: the order state machine
  (`Order.ChangeStatus`), stock reserve/release on `Product`, and `DiscountCalculator`.
- **Application** – one service per area (`AuthService`, `ProductService`, `OrderService`,
  `ReportService`) working against `IAppDbContext`. DTOs and request models live here, and
  application exceptions (`NotFoundException`, `ConflictException`, ...) carry their HTTP status.
- **Infrastructure** – `AppDbContext` + entity configurations, migrations, seeding, JWT/refresh token
  service, password hasher (ASP.NET Core Identity's PBKDF2), `IMemoryCache` product cache, SQL Server
  error classification and the two hosted services.
- **Api** – thin controllers, JWT bearer setup, the login rate limiter, `GlobalExceptionHandler`
  (everything becomes RFC 7807 ProblemDetails), Swagger, Serilog, SignalR hub.

I let the Application layer use EF Core's `DbSet`/`IQueryable` through `IAppDbContext` instead of
adding a repository per entity. It keeps queries composable (pagination/filtering stays in SQL) and
the services are still testable with the in-memory provider. The only provider-specific bit
(SQL error numbers) is behind `IDbExceptionClassifier`.

Status codes used: 200, 201, 204, 400 (validation / business rule), 401, 403, 404,
409 (stock / concurrency / duplicate SKU or email), 422 (idempotency key reused with a different body),
429 (login rate limit). Unexpected errors return a generic 500 ProblemDetails and are logged with the
stack trace; nothing internal leaks to the client.

### Client

- `core/api/*` – one typed service per API area; `core/models/api.models.ts` – TypeScript interfaces for every DTO
- `core/auth/auth.interceptor.ts` – attaches the JWT; on 401 refreshes the token (one shared refresh
  call for parallel requests) and retries the original request **once**; if refresh fails, signs out
- `core/auth/auth.guards.ts` – `authGuard`, `roleGuard('Admin' | 'Customer')`, `guestGuard`
- `core/cart/cart.service.ts` – signal based cart saved to `localStorage` (also synced across tabs)
- `core/realtime/order-hub.service.ts` – SignalR connection tied to the login state
- `shared/` – reusable loading / empty / error views, pagination, status badge, order lines, toasts
- `features/` – login, register, catalogue, cart, my orders, admin (orders, products, product form, report)

Every page has loading, empty and error states. The layout is responsive: the nav collapses into a
menu and tables become stacked cards on mobile.

---

## 6. Concurrency and idempotency

### Rule 1 – all items or nothing

`OrderService.PlaceOrderAsync` opens a transaction, loads all requested products, and **checks
every line before changing anything**. If one product is missing, inactive or short, it throws and
the transaction is disposed without commit. Stock is decremented and the order inserted in the
same `SaveChanges`. As an extra safety net the `Products` table has a check constraint
`StockQuantity >= 0`.

Duplicate lines for the same product are merged first, so `[{p1, 1}, {p1, 1}]` is checked as 2 units.

### Rule 2 – last unit, two buyers

`Product.RowVersion` is a SQL Server `rowversion` configured with `IsRowVersion()`, so EF adds it to
the `WHERE` clause of the stock update:

```sql
UPDATE [Products] SET [StockQuantity] = @p0
OUTPUT INSERTED.[RowVersion]
WHERE [Id] = @p1 AND [RowVersion] = @p2;
```

Both requests read stock = 1 with the same row version. The first commit changes the row version,
so the second `UPDATE` matches 0 rows and EF throws `DbUpdateConcurrencyException`. The service
catches it, rolls back and throws `ConflictException("...", "out_of_stock")`, which the exception
handler returns as **409 ProblemDetails** (not 500). If the second request reads after the first
commit, it sees stock 0 and gets the same 409 from the normal stock check.

EF Core creates the database with `READ_COMMITTED_SNAPSHOT ON`, so readers don't block each other
and the race really goes through the RowVersion path. In the integration test all 5 rounds hit
`DbUpdateConcurrencyException` and returned one 201 and one 409.

`Order` also has a `RowVersion`: without it, the customer cancelling and the background job
cancelling the same order at the same moment could both restore stock. Status changes retry up to
3 times on a conflict, re-reading the order each time, so the second cancel sees "already Cancelled"
and stock is restored exactly once.

### Rule 3 – Idempotency-Key

- The key is stored on the order: unique filtered index on `(CustomerId, IdempotencyKey)`.
  Keys are scoped per customer, so two customers can't collide or read each other's orders.
- A SHA-256 hash of the normalised items is stored with it.
- Flow:
  1. Order with this key exists → return it (**200**, header `Idempotent-Replayed: true`). Same key
     but a different body → **422**.
  2. Otherwise place the order normally (**201**).
  3. If two identical requests race (double click), both pass step 1, but the unique index lets only
     one insert succeed. The loser gets a unique-constraint violation (2601/2627), its transaction
     (including its stock change) is rolled back, and it returns the winner's order. The parallel
     integration test sends 3 requests with the same key: one 201, two 200, same order id, stock
     reduced once.
- The key is required (400 without it). I chose that over "optional" because a checkout without
  protection is exactly the bug the header is meant to prevent.

On the client, the cart keeps a `checkoutKey` (UUID) that only changes when the cart contents change.
Retries, double clicks and even a page refresh during checkout all send the same key. The
"Place order" button is also disabled while the request is in flight.

### Rule 4 – status transitions

The allowed transitions are a table in `Order` (`Pending → Confirmed → Shipped`,
`Pending/Confirmed → Cancelled`); anything else throws `InvalidStatusTransitionException`
→ **400** with a message like *"Order is Shipped and can no longer be changed."*
Admins can do any valid transition. Customers can only cancel **their own Pending** order
(another customer's order → 403, confirming → 403, cancelling a Confirmed order → 400).
Cancelling puts the stock back in the same `SaveChanges` as the status change.

### Rule 5 – discounts

`DiscountCalculator` (pure function, decimal only):
- 5% when `SubTotal > 10,000` (exactly 10,000 doesn't qualify)
- +10% when this is the customer's 5th, 10th, 15th... order
- both are percentages of the sub total, so they add up to 15%
- rounded to 2 decimals with `MidpointRounding.AwayFromZero`

The order number counts the customer's previous orders that are **not Cancelled**, plus one. Prices
come from the database at order time (`OrderItem.UnitPrice`), never from the client.

### Rule 6 – auto-cancel

`PendingOrderCleanupService` (`BackgroundService` + `PeriodicTimer`, every 60 s) creates a scope and
calls `OrderService.CancelExpiredPendingOrdersAsync`. It cancels orders Pending for more than
30 minutes (configurable: `Orders:PendingTimeoutMinutes`) in batches of 100, each through the same
status-change path, so stock is restored and concurrency is handled. The order is re-checked after
loading, so one an admin confirmed a second ago is left alone. Failures are logged and the loop keeps
running.

---

## 7. Other requirements

- **JWT + refresh tokens** – 15 min access token (HS256), 7 day refresh token. Refresh tokens are
  random 64-byte values; only their SHA-256 hash is stored. Every refresh rotates the token. If an
  already-rotated token is used again, all of that user's sessions are revoked (token theft detection).
- **Role-based authorization** – `[Authorize(Roles = "Admin")]` / `"Customer"`; role is a JWT claim.
  Registration always creates Customers.
- **Rate limiting** – built-in `AddRateLimiter`, fixed window, 5 requests / minute / IP on
  `POST /api/auth/login`, 429 ProblemDetails with `Retry-After` and `retryAfterSeconds`.
- **Product cache** – `IMemoryCache`. The key includes every filter/sort/page value. All entries share a
  `CancellationTokenSource`, so one `Invalidate()` evicts every cached page. It is called on product
  create/update, and also when orders change stock, so the "in stock" filter never shows stale data.
  There is also a 5 minute absolute expiry.
- **Pagination / filtering / sorting** – composed on `IQueryable` and executed by SQL Server
  (`WHERE ... ORDER BY ... OFFSET/FETCH` + `COUNT`). Filters: search (name or SKU), min/max price,
  in stock; sort by name, price, stock, newest; page size capped at 100.
- **Product edit concurrency** – the admin form sends back the `rowVersion` it loaded; if the product
  changed in the meantime (another admin, or an order changing stock) the update is a 409 and the
  form offers "Reload latest".
- **Logging** – Serilog with structured properties (`OrderId`, `CustomerId`, `Total`, ...), request
  logging, and EF SQL command logging in Development.
- **Swagger** – with a JWT bearer security scheme.
- **Health** – `/health` includes a database check (`AddDbContextCheck`), so it reports Unhealthy when SQL Server is down.

### Edge cases handled

These came out of reviewing the code against production traffic rather than the happy path; each
one has a test.

| Case | Without handling | Now |
|---|---|---|
| Two sign-ups with the same email at the same time | both pass the "email exists?" check, second insert hits the unique index → 500 | unique violation mapped to 409 `email_taken` |
| Two refreshes with the same refresh token at the same time | both rotate it → two valid refresh tokens | `RevokedAt` is a concurrency token; the loser gets 401 |
| Login with an unknown email | returns faster than a wrong password (no hash check) → reveals which emails exist | a dummy hash is verified so both paths cost the same |
| `?page=2147483647` | `(page-1)*pageSize` overflows → negative `Skip` → 500 | page is clamped |
| `{"status": 99}` | enum binds to an undefined value | `[EnumDataType]` → 400 |
| Very long / random search strings | unbounded cache keys and memory | `search` max 100 chars, cache has a `SizeLimit` |
| Order with thousands of lines | huge `IN (...)` query | max 50 distinct products per order |
| Two browser tabs and rotating refresh tokens | a tab refreshing with the old token trips reuse detection and logs the user out everywhere | tabs share the session through the `storage` event |
| SignalR reconnect after the access token expired | negotiate fails with 401 | the hub's `accessTokenFactory` refreshes the token first |
| Validation errors on controllers with `[Produces("application/json")]` | returned as `application/json` | always `application/problem+json` |

---

## 8. Part B – top customers in LINQ

`ReportService.GetTopCustomersAsync` (`GET /api/reports/top-customers?days=30`):

```csharp
var spendPerCustomer = _db.Orders
    .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped) && o.CreatedAt >= since)
    .GroupBy(o => o.CustomerId)
    .Select(g => new { CustomerId = g.Key, TotalSpend = g.Sum(o => o.Total), OrderCount = g.Count() })
    .OrderByDescending(x => x.TotalSpend).ThenBy(x => x.CustomerId)
    .Take(top);

var query =
    from s in spendPerCustomer
    join u in _db.Users on s.CustomerId equals u.Id
    orderby s.TotalSpend descending, s.CustomerId
    select new TopCustomerDto(u.Id, u.FullName, u.Email, s.OrderCount, s.TotalSpend);
```

Generated SQL, copied from the API log (`Microsoft.EntityFrameworkCore.Database.Command`):

```sql
Executed DbCommand (17ms) [Parameters=[@__p_1='?' (DbType = Int32), @__since_0='?' (DbType = DateTime2)], CommandType='Text', CommandTimeout='30']
SELECT [u].[Id], [u].[FullName], [u].[Email], [t].[OrderCount], [t].[TotalSpend]
FROM (
    SELECT TOP(@__p_1) [o].[CustomerId], COALESCE(SUM([o].[Total]), 0.0) AS [TotalSpend], COUNT(*) AS [OrderCount]
    FROM [Orders] AS [o]
    WHERE [o].[Status] IN (N'Confirmed', N'Shipped') AND [o].[CreatedAt] >= @__since_0
    GROUP BY [o].[CustomerId]
    ORDER BY COALESCE(SUM([o].[Total]), 0.0) DESC, [o].[CustomerId]
) AS [t]
INNER JOIN [Users] AS [u] ON [t].[CustomerId] = [u].[Id]
ORDER BY [t].[TotalSpend] DESC, [t].[CustomerId]
```

One statement: grouping, `TOP` and the join all run in SQL Server, nothing is loaded into memory and
there is no N+1. Aggregating before the join means only 5 user rows are joined.

`QUERIES.sql` has the five raw SQL queries with notes on the choices (NOT EXISTS vs NOT IN,
ROW_NUMBER vs DENSE_RANK, integer AVG, sargable date ranges).

---

## 9. API endpoints

| Method | Route | Access |
|---|---|---|
| POST | /api/auth/register | anonymous |
| POST | /api/auth/login | anonymous, 5/min/IP |
| POST | /api/auth/refresh | anonymous (refresh token in body) |
| POST | /api/auth/logout | anonymous (revokes the refresh token) |
| GET | /api/products?search=&minPrice=&maxPrice=&inStock=&sortBy=&sortDir=&page=&pageSize= | anonymous |
| GET | /api/products/{id} | anonymous |
| POST | /api/products | Admin |
| PUT | /api/products/{id} | Admin |
| POST | /api/orders (header `Idempotency-Key`) | Customer |
| GET | /api/orders/my?page=&pageSize= | signed in |
| GET | /api/orders?status=&page=&pageSize= | Admin (order list for the admin screen) |
| GET | /api/orders/{id} | owner or Admin |
| PATCH | /api/orders/{id}/status | Admin; Customer only to cancel own Pending order |
| GET | /api/reports/top-customers?days=30&top=5 | Admin |
| WS | /hubs/orders | signed in (SignalR: `orderStatusChanged`, `orderPlaced`) |
| GET | /health | anonymous |

---

## 10. Bonus items

- **Docker Compose** – SQL Server, API and nginx-served client with one command (see above).
- **SignalR** – customers get their own order's status changes live on *My orders*; admins get all
  status changes and a "new orders" banner. The token goes in the `access_token` query string for
  the WebSocket only.
- **Outbox** – placing an order writes an `OutboxMessages` row in the same transaction.
  `OutboxProcessor` publishes pending messages every 5 s (SignalR `orderPlaced` to admins plus a log
  entry), marks them processed, and records attempts/errors (max 5 attempts). Delivery is
  at-least-once.
- **GitHub Actions** – `.github/workflows/ci.yml` builds the backend, runs unit tests and the
  integration tests against a SQL Server service container, then builds and tests the client.
- **Frontend unit tests** – 13 Jasmine specs.

---

## 11. Assumptions

- **Discounts:** the two discounts add up (5% + 10% = 15% of the sub total). "Every 5th order" counts
  the customer's non-cancelled orders, including the one being placed.
- **409 for out of stock:** insufficient stock and losing the race both return 409, because in both
  cases the request conflicts with the current stock and the client handles them the same way.
- **Idempotency replay** returns 200 (not 201) with the original order and `Idempotent-Replayed: true`,
  so a client can tell a replay from a new order. Keys are per customer, up to 100 characters.
- **Customer cancelling a non-Pending order** is a 400 (business rule, with a message); trying to change
  someone else's order is a 403. `GET /api/orders/{id}` for another customer's order is 403, as the spec
  lists it (404 would hide that the order exists; that's a one-line change).
- **Product list is public** – browsing doesn't need an account; checkout does. Admins see inactive
  products with `includeInactive=true`.
- **Admins don't place orders** – `POST /api/orders` is Customer only.
- **Revenue / spend** means Confirmed + Shipped orders, using `Total` (after discount).
- An extra admin endpoint `GET /api/orders` was added because the admin screen needs an order list.
- Prices are in INR in the UI; the API itself is currency agnostic.
- All timestamps are stored and compared in UTC.

## 12. What I would improve with more time

- Store the refresh token in an httpOnly, SameSite cookie instead of `localStorage` (XSS exposure),
  and add a CSRF token for the refresh call.
- A cleanup job for expired / revoked refresh tokens (the table only grows today), and a short
  grace period for a just-rotated token so a request that was in flight during a refresh isn't
  treated as theft.
- `EnableRetryOnFailure` for transient SQL errors – it needs the order transaction wrapped in
  `Database.CreateExecutionStrategy().ExecuteAsync(...)`, which I left out to keep the flow readable.
- Access tokens stay valid until they expire (15 min) after logout or a role change; a token
  version per user (checked in `OnTokenValidated`) would cut that off immediately.
- Outbox: claim messages with `UPDLOCK, READPAST` so several API instances can run the processor, and
  publish to a real broker (Service Bus / RabbitMQ) instead of SignalR + logs. Same for the cleanup job
  (only one instance should run it – a distributed lock).
- Swap `IMemoryCache` for a distributed cache (Redis) when there is more than one API instance.
- "Every 5th order" can be raced by the same customer placing two orders at the same moment; a per-customer
  order counter row (also with a rowversion) would make it exact.
- Trust `X-Forwarded-For` only from the known proxy network, not from any address (now only enabled in Docker).
- Full-text search for product names (`LIKE '%x%'` can't use an index), plus product images and categories.
- Playwright end-to-end tests for checkout and the concurrency flow.
- API versioning, health checks for the database, and OpenTelemetry tracing.
