using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Store.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class OrderConcurrencyTests
{
    private readonly StoreApiFactory _factory;

    public OrderConcurrencyTests(StoreApiFactory factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage OrderRequest(int productId, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { items = new[] { new { productId, quantity = 1 } } })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    // Rule 2: two customers buy the last unit at the same moment -> exactly one 201 and one 409.
    // Repeated a few times so both race outcomes (RowVersion conflict and plain "no stock left") get exercised.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Two_parallel_orders_for_the_last_unit_one_succeeds_one_gets_409(int round)
    {
        var productId = await _factory.CreateProductAsync(stock: 1);
        using var aarav = await _factory.CreateClientForAsync("aarav@store.local");
        using var priya = await _factory.CreateClientForAsync("priya@store.local");

        using var start = new ManualResetEventSlim(false);
        var first = Task.Run(async () => { start.Wait(); return await aarav.SendAsync(OrderRequest(productId, $"race-a-{round}")); });
        var second = Task.Run(async () => { start.Wait(); return await priya.SendAsync(OrderRequest(productId, $"race-b-{round}")); });
        start.Set();

        var responses = await Task.WhenAll(first, second);
        var codes = responses.Select(r => r.StatusCode).OrderBy(c => (int)c).ToArray();

        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, codes);

        var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("out_of_stock", problem.RootElement.GetProperty("code").GetString());

        var (stock, orderLines) = await _factory.GetProductStateAsync(productId);
        Assert.Equal(0, stock);
        Assert.Equal(1, orderLines);
    }

    // Optimistic concurrency must not turn "someone else bought one too" into a failure:
    // the request that loses the RowVersion check re-reads the stock and succeeds when there is enough.
    [Fact]
    public async Task Parallel_orders_all_succeed_while_there_is_enough_stock()
    {
        var productId = await _factory.CreateProductAsync(stock: 10);
        var emails = new[] { "aarav@store.local", "priya@store.local", "rohan@store.local" };
        var clients = await Task.WhenAll(emails.Select(e => _factory.CreateClientForAsync(e)));

        using var start = new ManualResetEventSlim(false);
        var tasks = clients
            .Select((client, i) => Task.Run(async () => { start.Wait(); return await client.SendAsync(OrderRequest(productId, $"enough-{i}-{Guid.NewGuid():N}")); }))
            .ToArray();
        start.Set();
        var responses = await Task.WhenAll(tasks);
        foreach (var client in clients) client.Dispose();

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var (stock, orderLines) = await _factory.GetProductStateAsync(productId);
        Assert.Equal(7, stock);
        Assert.Equal(3, orderLines);
    }

    // Rule 1 against the real database: one short line fails the whole order and no stock moves.
    [Fact]
    public async Task One_short_line_fails_the_whole_order_and_no_stock_changes()
    {
        var plentyId = await _factory.CreateProductAsync(stock: 5);
        var scarceId = await _factory.CreateProductAsync(stock: 1);
        using var client = await _factory.CreateClientForAsync("aarav@store.local");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { items = new[] { new { productId = plentyId, quantity = 2 }, new { productId = scarceId, quantity = 2 } } })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal((5, 0), await _factory.GetProductStateAsync(plentyId));
        Assert.Equal((1, 0), await _factory.GetProductStateAsync(scarceId));
    }

    // Rule 3 under concurrency: a double-click sends the same key twice at once -> one order, stock reduced once.
    [Fact]
    public async Task Parallel_requests_with_the_same_idempotency_key_create_one_order()
    {
        var productId = await _factory.CreateProductAsync(stock: 10);
        using var client = await _factory.CreateClientForAsync("rohan@store.local");
        var key = $"double-click-{Guid.NewGuid():N}";

        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => Task.Run(async () => { start.Wait(); return await client.SendAsync(OrderRequest(productId, key)); }))
            .ToArray();
        start.Set();

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, $"Unexpected {(int)r.StatusCode}"));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);

        var ids = new HashSet<int>();
        foreach (var response in responses)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            ids.Add(body.RootElement.GetProperty("id").GetInt32());
        }
        Assert.Single(ids);

        var (stock, orderLines) = await _factory.GetProductStateAsync(productId);
        Assert.Equal(9, stock);
        Assert.Equal(1, orderLines);
    }
}
