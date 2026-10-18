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
