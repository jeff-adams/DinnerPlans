using DinnerPlansAPI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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