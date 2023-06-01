using Azure.Core;
using DinnerPlansAPI.Repositories;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(DinnerPlansAPI.Startup))]

namespace DinnerPlansAPI;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.AddTokenCredentials();
        builder.Services.AddScoped<IMenuRepository, MenuTableRepository>();
    }
}