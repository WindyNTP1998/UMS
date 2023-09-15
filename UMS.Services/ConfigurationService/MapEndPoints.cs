namespace UMS.Services.ConfigurationService;

public static class MapEndPoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.ApplySwagger();
        app.UseHttpsRedirection();
        app.MapControllers();
        app.UseAuthorization();
        app.ApplyMigrate();
    }
}