using System.Globalization;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Store.Api.Hubs;
using Store.Infrastructure.Security;

namespace Store.Api.Infrastructure;

public static class SecuritySetup
{
    public const string LoginRateLimitPolicy = "login";
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";

    /// <summary>
    /// Makes sure a signing key exists. Outside Development a missing key is a startup error;
    /// in Development a random key is generated so the project runs without extra setup.
    /// </summary>
    public static void EnsureJwtKey(WebApplicationBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
            return;

        if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it with 'dotnet user-secrets' or the Jwt__Key environment variable.");

        builder.Configuration["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        Serilog.Log.Warning("Jwt:Key is not configured - using a random development key. Tokens become invalid after a restart.");
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt.Key),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = StoreClaims.Name,
                    RoleClaimType = StoreClaims.Role
                };

                options.Events = new JwtBearerEvents
                {
                    // Browsers can't set headers on WebSockets, so SignalR sends the token in the query string.
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments(OrderHub.Path))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
                        if (expired)
                            context.Response.Headers.Append("X-Token-Expired", "true");

                        await WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized",
                            expired ? "Access token has expired." : "A valid access token is required.");
                    },
                    OnForbidden = context =>
                        WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden",
                            "You do not have permission to access this resource.")
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
        var window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:LoginWindowSeconds", 60));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // One fixed window per client IP.
            options.AddPolicy(LoginRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                    ? (int)Math.Ceiling(value.TotalSeconds)
                    : (int)window.TotalSeconds;

                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting");
                logger.LogWarning("Login rate limit hit for {RemoteIp}", context.HttpContext.Connection.RemoteIpAddress);

                await WriteProblemAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, "Too many requests",
                    $"Too many login attempts. Please try again in {retryAfter} seconds.",
                    new Dictionary<string, object?> { ["retryAfterSeconds"] = retryAfter });
            };
        });

        return services;
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, int status, string title, string detail,
        IDictionary<string, object?>? extensions = null)
    {
        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
                problem.Extensions[key] = value;
        }

        var writer = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await writer.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
