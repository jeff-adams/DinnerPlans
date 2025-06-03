using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using DinnerPlansAPI.Repositories;
using Azure.Data.Tables;

namespace DinnerPlansAPI;

public static class FunctionHostBuilderExtensions
{
    public static IServiceCollection AddTokenCredentials(this IServiceCollection services)
    {
        var creds = new DefaultAzureCredential();
        services.AddScoped<TokenCredential>(x => creds);
        return services;
    }

    public static IServiceCollection AddTableRepository<T>(this IServiceCollection services, string tableName, string partitionKey) where T : class, ITableEntity, new()
    {
        services.AddScoped<ITableRepository<T>, TableRepository<T>>(
            provider => new TableRepository<T>(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<TokenCredential>(),
                tableName,
                partitionKey
            )
        );
        return services;
    }
}