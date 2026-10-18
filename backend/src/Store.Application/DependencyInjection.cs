using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Store.Application.Auth;
using Store.Application.Orders;
using Store.Application.Products;
using Store.Application.Reports;

namespace Store.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OrderOptions>(configuration.GetSection(OrderOptions.SectionName));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
