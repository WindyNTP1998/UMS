using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UMS.Services.Repository;

namespace UMS.Services.ConfigurationService;

public static class ServiceRegister
{
    public static void Register(IServiceCollection service, IConfiguration configuration)
    {
        service.AddControllers();
        service.CreateDbContext(configuration);
        service.ApplyMediatR();
        service.ConfigureSwagger();
    }


    private static void CreateDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UmsEfCoreDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
        });
    }

    private static void ApplyMediatR(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<Program>());
    }
}

