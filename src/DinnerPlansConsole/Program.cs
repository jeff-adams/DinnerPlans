using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

ServiceCollection services = new();

services.AddSingleton<IConfiguration>(configuration);
services.AddSingleton<Application>();

ServiceProvider provider = services.BuildServiceProvider();
Application app = provider.GetRequiredService<Application>();

app.Run();
