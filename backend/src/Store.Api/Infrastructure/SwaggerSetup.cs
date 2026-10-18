using Microsoft.OpenApi.Models;

namespace Store.Api.Infrastructure;

public static class SwaggerSetup
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Store API",
                Version = "v1",
                Description = "Orders & inventory API. Use /api/auth/login, then click Authorize and paste the access token."
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the accessToken returned by /api/auth/login (without the 'Bearer ' prefix).",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };

            // Controller /// summaries show up as endpoint descriptions.
            var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(SwaggerSetup).Assembly.GetName().Name}.xml");
            if (File.Exists(xmlFile))
                options.IncludeXmlComments(xmlFile);

            options.SchemaFilter<RequestExamplesSchemaFilter>();
            options.OperationFilter<IdempotencyKeyOperationFilter>();
            options.OperationFilter<AuthResponsesOperationFilter>();

            options.AddSecurityDefinition("Bearer", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
        });

        return services;
    }
}
