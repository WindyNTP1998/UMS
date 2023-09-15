using Microsoft.EntityFrameworkCore;
using UMS.Services.Repository;

namespace UMS.Services.ConfigurationService;

public static class ApplyMigration
{
    public static void ApplyMigrate(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsEfCoreDbContext>();

        if (db.Database.GetPendingMigrations().Any()) db.Database.Migrate();
    }
}