using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Store.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApiBehaviourTests
{
    private readonly StoreApiFactory _factory;

    public ApiBehaviourTests(StoreApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_is_rate_limited_to_five_attempts_per_minute()
    {
        using var client = _factory.CreateClient();
        var codes = new List<HttpStatusCode>();

        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "aarav@store.local", password = "wrong-password" });
            codes.Add(response.StatusCode);
        }

        Assert.All(codes.Take(5), c => Assert.Equal(HttpStatusCode.Unauthorized, c));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[5]);
    }

    [Fact]
    public async Task Customer_cannot_read_another_customers_order()
    {
        var productId = await _factory.CreateProductAsync(stock: 5);
        using var owner = await _factory.CreateClientForAsync("aarav@store.local");
        using var stranger = await _factory.CreateClientForAsync("priya@store.local");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { items = new[] { new { productId, quantity = 1 } } })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var created = await owner.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var orderId = body.RootElement.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/orders/{orderId}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_status_transition_returns_400_problem_details()
    {
        var productId = await _factory.CreateProductAsync(stock: 5);
        using var customer = await _factory.CreateClientForAsync("rohan@store.local");
        using var admin = await _factory.CreateClientForAsync("admin@store.local");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { items = new[] { new { productId, quantity = 1 } } })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var body = JsonDocument.Parse(await (await customer.SendAsync(request)).Content.ReadAsStringAsync());
        var orderId = body.RootElement.GetProperty("id").GetInt32();

        var response = await admin.PatchAsJsonAsync($"/api/orders/{orderId}/status", new { status = "Shipped" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("Pending", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Admin_endpoints_reject_customers_and_anonymous_users()
    {
        using var anonymous = _factory.CreateClient();
        using var customer = await _factory.CreateClientForAsync("aarav@store.local");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/reports/top-customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/reports/top-customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await customer.PostAsJsonAsync("/api/products", new { sku = "NEW-1", name = "New", price = 10, stockQuantity = 1 })).StatusCode);
    }
}
