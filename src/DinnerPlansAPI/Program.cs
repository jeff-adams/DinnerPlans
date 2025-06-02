using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DinnerPlansAPI;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddTokenCredentials()
            .AddTableRepository<MenuEntity>("menu", "menu")
            .AddTableRepository<MealEntity>("meals", "meal")
            .AddTableRepository<CatagoryEntity>("catagories", "catagory")
            .AddTableRepository<CatagoryEntity>("catagories", "catagory")
            .AddTableRepository<RuleEntity>("rules", "day");
    })
    .Build();

host.Run();