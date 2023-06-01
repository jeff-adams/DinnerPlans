using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

namespace DinnerPlansAPI;

public static class FunctionHostBuilderExtensions
{
    public static IFunctionsHostBuilder AddTokenCredentials(this IFunctionsHostBuilder builder)
    {
        var creds = new DefaultAzureCredential();
        builder.Services.AddScoped<TokenCredential>(x => creds);
        return builder;
    }
}