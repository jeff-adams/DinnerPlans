using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(DinnerPlansAPI.Startup))]

namespace DinnerPlansAPI;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder
            .AddTokenCredentials()
            .AddTableRepository<MenuEntity>("menu", "menu")
            .AddTableRepository<MealEntity>("meals", "meal")
            .AddTableRepository<CatagoryEntity>("catagories", "catagories")
            .AddTableRepository<SpecialDateEntity>("specialDates", "specialDates")
            .AddTableRepository<RuleEntity>("rules", "day");
    }
}