using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace DinnerPlansAPI.Repositories;

public class TableRepository<T> : IDinnerPlanRepository<T> where T : class, ITableEntity, new()
{
    private TableClient client;
    private readonly string partitionKey = "menu";
    private readonly string entityTypeName;

    public TableRepository(IConfiguration config, TokenCredential creds)
    {
        entityTypeName = nameof(T);
        string tableName = config[entityTypeName];
        Uri tableEndpoint = new (config["TableEndpoint"]);
        client = new TableClient(tableEndpoint, tableName, creds);
    }

    public async Task<T> GetEntityAsync(string key)
    {
        try
        {
            return await client.GetEntityAsync<T>(partitionKey, key);
        }
        catch (RequestFailedException ex)
        {
            throw new DinnerPlansRepositoryException($"Unable to get the {entityTypeName} for the key: {key}", ex);
        }
    }

    public async Task AddEntityAsync(T entity)
    {
        try
        {
            Response response = await client.AddEntityAsync<T>(entity);   
        }
        catch (RequestFailedException ex)
        {
            throw new DinnerPlansRepositoryException($"Unable to create the {entityTypeName} for the key: {entity.RowKey}", ex);
        }
    }

    public async Task UpdateEntityAsync(T entity)
    {
        try
        {
            Response response = await client.UpdateEntityAsync<T>(entity, Azure.ETag.All);
        }
        catch (RequestFailedException ex)
        {
            throw new DinnerPlansRepositoryException($"Unable to update the {entityTypeName}", ex);
        }
    }

    public async Task<IReadOnlyCollection<T>> QueryEntityAsync(Expression<Func<T, bool>> filter)
    {
        try
        {
            return await client.QueryAsync<T>(filter).ToListAsync();
        }
        catch (RequestFailedException)
        {
            return Enumerable.Empty<T>() as IReadOnlyCollection<T>;
        }
    }
}