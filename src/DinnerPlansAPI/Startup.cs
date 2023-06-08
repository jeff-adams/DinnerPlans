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
            .AddTableRepository<CatagoryEntity>("catagories", "catagory")
            .AddTableRepository<SpecialDateEntity>("specialDates", "dates")
            .AddTableRepository<RuleEntity>("rules", "day");
    }
}