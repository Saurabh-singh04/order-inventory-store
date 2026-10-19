using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Store.IntegrationTests;

/// <summary>Edge cases found during review - each one used to end in a 500 or a security gap.</summary>
[Collection(ApiCollection.Name)]
public sealed class HardeningTests
{
    private readonly StoreApiFactory _factory;

    public HardeningTests(StoreApiFactory factory)
    {
        _factory = factory;
    }

    private static object NewUser(string email) => new { fullName = "Test User", email, password = "Password@123" };

    private static async Task<string> RefreshTokenOf(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("refreshToken").GetString()!;
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reusing_an_old_one_revokes_the_session()
    {
        using var client = _factory.CreateClient();
        var registered = await client.PostAsJsonAsync("/api/auth/register", NewUser($"rotate-{Guid.NewGuid():N}@test.local"));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var first = await RefreshTokenOf(registered);

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = await RefreshTokenOf(refreshed);
        Assert.NotEqual(first, second);

        // Old token replayed (e.g. stolen) -> rejected, and the newer token dies with it.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second })).StatusCode);
    }

    [Fact]
    public async Task Parallel_refreshes_with_the_same_token_only_issue_one_new_token()
    {
        using var client = _factory.CreateClient();
        var registered = await client.PostAsJsonAsync("/api/auth/register", NewUser($"parallel-{Guid.NewGuid():N}@test.local"));
        var token = await RefreshTokenOf(registered);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    [Fact]
    public async Task Parallel_registrations_with_the_same_email_return_201_and_409_not_500()
    {
        using var client = _factory.CreateClient();
        var email = $"twice-{Guid.NewGuid():N}@test.local";

        var responses = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => client.PostAsJsonAsync("/api/auth/register", NewUser(email))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Theory]
    [InlineData("/api/products?page=2147483647")]
    [InlineData("/api/products?page=-5&pageSize=100000")]
    public async Task Extreme_paging_values_are_clamped_instead_of_failing(string url)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Undefined_status_value_is_a_validation_error()
    {
        using var admin = await _factory.CreateClientForAsync("admin@store.local");

        var response = await admin.PatchAsJsonAsync("/api/orders/1/status", new { status = 99 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Order_without_idempotency_key_is_placed_normally()
    {
        var productId = await _factory.CreateProductAsync(stock: 3);
        using var customer = await _factory.CreateClientForAsync("aarav@store.local");

        var response = await customer.PostAsJsonAsync("/api/orders", new { items = new[] { new { productId, quantity = 1 } } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, (await _factory.GetProductStateAsync(productId)).Stock);
    }

    [Fact]
    public async Task Dates_read_from_the_database_are_returned_as_utc()
    {
        var productId = await _factory.CreateProductAsync(stock: 2);
        using var customer = await _factory.CreateClientForAsync("priya@store.local");
        using var admin = await _factory.CreateClientForAsync("admin@store.local");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { items = new[] { new { productId, quantity = 1 } } })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var created = JsonDocument.Parse(await (await customer.SendAsync(request)).Content.ReadAsStringAsync());
        var orderId = created.RootElement.GetProperty("id").GetInt32();
        await admin.PatchAsJsonAsync($"/api/orders/{orderId}/status", new { status = "Confirmed" });

        // Read back from SQL Server (not the in-memory entity) - datetime2 comes back without a kind.
        using var order = JsonDocument.Parse(await admin.GetStringAsync($"/api/orders/{orderId}"));

        Assert.EndsWith("Z", order.RootElement.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", order.RootElement.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task Health_endpoint_checks_the_database()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
