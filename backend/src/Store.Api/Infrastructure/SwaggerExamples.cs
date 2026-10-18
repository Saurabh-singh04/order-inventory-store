using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Store.Api.Controllers;
using Store.Application.Auth;
using Store.Application.Orders;
using Store.Application.Products;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Store.Api.Infrastructure;

/// <summary>Pre-fills "Try it out" in Swagger with request bodies that work against the seeded data.</summary>
public sealed class RequestExamplesSchemaFilter : ISchemaFilter
{
    private static readonly Dictionary<Type, object> Examples = new()
    {
        [typeof(RegisterRequest)] = new { fullName = "Neha Verma", email = "neha@store.local", password = "Neha@1234" },
        [typeof(LoginRequest)] = new { email = "aarav@store.local", password = "Customer@123" },
        [typeof(RefreshRequest)] = new { refreshToken = "paste the refreshToken returned by /api/auth/login" },
        [typeof(CreateProductRequest)] = new { sku = "ACC-4005", name = "Wireless Earbuds", price = 2999.00m, stockQuantity = 25, isActive = true },
        [typeof(UpdateProductRequest)] = new
        {
            sku = "ACC-4005",
            name = "Wireless Earbuds (2026)",
            price = 2799.00m,
            stockQuantity = 30,
            isActive = true,
            rowVersion = "paste rowVersion from GET /api/products/{id}"
        },
        [typeof(CreateOrderRequest)] = new { items = new[] { new { productId = 5, quantity = 2 }, new { productId = 8, quantity = 1 } } },
        [typeof(OrderItemRequest)] = new { productId = 5, quantity = 2 },
        [typeof(UpdateOrderStatusRequest)] = new { status = "Confirmed" }
    };

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (Examples.TryGetValue(context.Type, out var example))
            schema.Example = ToOpenApi(JsonSerializer.SerializeToElement(example));
    }

    private static IOpenApiAny ToOpenApi(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new OpenApiObject();
                foreach (var property in element.EnumerateObject())
                    obj[property.Name] = ToOpenApi(property.Value);
                return obj;
            case JsonValueKind.Array:
                var array = new OpenApiArray();
                array.AddRange(element.EnumerateArray().Select(ToOpenApi));
                return array;
            case JsonValueKind.Number:
                return element.TryGetInt32(out var i) ? new OpenApiInteger(i) : new OpenApiDouble(element.GetDouble());
            case JsonValueKind.True:
            case JsonValueKind.False:
                return new OpenApiBoolean(element.GetBoolean());
            case JsonValueKind.Null:
                return new OpenApiNull();
            default:
                return new OpenApiString(element.GetString());
        }
    }
}

/// <summary>Documents the Idempotency-Key header and gives it a fresh example value.</summary>
public sealed class IdempotencyKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var header = operation.Parameters?.FirstOrDefault(p => p.Name == OrdersController.IdempotencyHeader);
        if (header is null)
            return;

        header.Required = true;
        header.Description = "Any unique string (a GUID is easiest). Sending the same key again returns the original order instead of creating a new one.";
        header.Example = new OpenApiString("3f9c2b1e-8a4d-4c1b-9e2f-6d7a5b0c1e23");
    }
}

/// <summary>Adds 401 / 403 to every endpoint that needs a token, so Swagger lists all possible codes.</summary>
public sealed class AuthResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
            return;

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing, invalid or expired access token" });

        if (metadata.OfType<IAuthorizeData>().Any(a => !string.IsNullOrEmpty(a.Roles)) || operation.Responses.ContainsKey("403"))
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Signed in, but the role is not allowed" });
    }
}
