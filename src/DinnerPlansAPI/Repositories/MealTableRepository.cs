using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace DinnerPlansAPI.Repositories;

public class MealTableRepository : IMealRepository
{
    private TableClient client;
    private const string mealTableName = "menu";
    private const string mealPartitionKey = "menu";
    
    public MealTableRepository(IConfiguration config, TokenCredential creds)
    {
        Uri tableEndpoint = new (config["TableEndpoint"]);
        client = new TableClient(tableEndpoint, mealTableName, creds);
    }

    public async Task<MealEntity> GetMealEntityAsync(string mealId)
    {
        try
        {
            return await client.GetEntityAsync<MealEntity>(mealPartitionKey, mealId);
        }
        catch (RequestFailedException ex)
        {
            throw new MealRepositoryException($"There was no meal found with ID: {mealId}", ex);
        }
    }
    
    public async Task AddMealEntityAsync(MealEntity meal)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateMealEntityAsync(MealEntity meal)
    {
        throw new NotImplementedException();
    }

    public async Task<IReadOnlyCollection<MealEntity>> QueryMealEntityAsync(Expression<Func<MealEntity, bool>> filter)
    {
        try
        {
            return await client.QueryAsync<MealEntity>(meal => meal.PartitionKey  == mealPartitionKey).ToListAsync();
        }
        catch (RequestFailedException ex)
        {
            return Enumerable.Empty<MealEntity>() as IReadOnlyCollection<MealEntity>;
        }
    }
}