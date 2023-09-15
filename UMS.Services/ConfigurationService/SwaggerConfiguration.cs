using Microsoft.OpenApi.Models;

namespace UMS.Services.ConfigurationService;

public static class SwaggerConfiguration
{
    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "UMS API",
                Version = "v1",
                Description = "UMS API by Phong Nguyen",
                Contact = new OpenApiContact
                {
                    Name = "Phong Nguyen",
                    Email = "windfoto98.contact@gmail.com"
                }
            });
        });
    }

    public static void ApplySwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Your API V1"); });
    }
}