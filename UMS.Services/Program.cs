using UMS.Services.ConfigurationService;

var builder = WebApplication.CreateBuilder(args);
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false)
    .Build();

ServiceRegister.Register(builder.Services, configuration);

var app = builder.Build();
app.MapUserEndpoints();
app.Run();