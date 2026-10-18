using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Store.Api.Controllers;
using Store.Api.Hubs;
using Store.Api.Infrastructure;
using Store.Application;
using Store.Application.Abstractions;
using Store.Infrastructure;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Persistence.Seed;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

    SecuritySetup.EnsureJwtKey(builder);

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

    builder.Services
        .AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddLoginRateLimiting(builder.Configuration);

    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IOrderNotifier, SignalROrderNotifier>();

    builder.Services.AddSwaggerWithJwt();
    builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

    var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders(OrdersController.ReplayedHeader, "Retry-After", "X-Token-Expired")));

    if (builder.Configuration.GetValue("ForwardedHeaders:Enabled", false))
    {
        // Only switched on when the API sits behind our own reverse proxy (docker-compose),
        // so the login rate limiter sees the real client IP instead of the proxy's.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    var app = builder.Build();

    if (app.Configuration.GetValue("ForwardedHeaders:Enabled", false))
        app.UseForwardedHeaders();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
    {
        app.UseSwagger();
        app.UseSwaggerUI(o => o.DisplayRequestDuration());
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHub<OrderHub>(OrderHub.Path);
    app.MapHealthChecks("/health");

    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
        await DatabaseInitializer.InitializeAsync(app.Services);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
