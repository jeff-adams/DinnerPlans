using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using DinnerPlansAPI.Repositories;
using Azure.Data.Tables;

namespace DinnerPlansAPI;

public static class FunctionHostBuilderExtensions
{
    public static IFunctionsHostBuilder AddTokenCredentials(this IFunctionsHostBuilder builder)
    {
        var creds = new DefaultAzureCredential();
        builder.Services.AddScoped<TokenCredential>(x => creds);
        return builder;
    }

    public static IFunctionsHostBuilder AddTableRepository<T>(this IFunctionsHostBuilder builder, string tableName, string partitionKey) where T : class, ITableEntity, new()
    {
        builder.Services.AddScoped<ITableRepository<T>, TableRepository<T>>(
            provider => new TableRepository<T>(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<TokenCredential>(),
                tableName,
                partitionKey
            )
        );
        return builder;
    }
}