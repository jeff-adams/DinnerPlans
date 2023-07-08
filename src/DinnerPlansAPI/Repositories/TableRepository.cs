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

public class TableRepository<T> : ITableRepository<T> where T : class, ITableEntity, new()
{
    public string PartitionKey { get; }

    private TableClient client;
    private readonly string entityTypeName;

    public TableRepository(IConfiguration config, TokenCredential creds, string tableName, string partitionKey)
    {
        PartitionKey = partitionKey;
        entityTypeName = typeof(T).ToString().Split('.').Last();
        Uri tableEndpoint = new (config["TableEndpoint"]);
        client = new TableClient(tableEndpoint, tableName, creds);
    }

    public async Task<T> GetEntityAsync(string key)
    {
        try
        {
            return await client.GetEntityAsync<T>(PartitionKey, key);
        }
        catch (RequestFailedException ex)
        {
            throw new TableRepositoryException($"Unable to get the {entityTypeName} for the key: {key}", ex);
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
            throw new TableRepositoryException($"Unable to create the {entityTypeName} with key {entity.RowKey}", ex);
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
            throw new TableRepositoryException($"Unable to update the {entityTypeName} with key {entity.RowKey}", ex);
        }
    }

    public async Task UpsertEntityAsync(T entity)
    {
        try
        {
            Response response = await client.UpsertEntityAsync<T>(entity, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex)
        {
            throw new TableRepositoryException($"Unable to upsert the {entityTypeName} with key {entity.RowKey}", ex);
        }
    }

    public async Task DeleteEntityAsync(string key)
    {
        try
        {
            Response response = await client.DeleteEntityAsync(PartitionKey, key);
        }
        catch (RequestFailedException ex)
        {
            throw new TableRepositoryException($"Unable to delete the {entityTypeName} with key {key}", ex);
        }
    }

    public async Task<IReadOnlyCollection<T>> QueryEntityAsync(Expression<Func<T, bool>> filter)
    {
        try
        {
            return await client.QueryAsync<T>(filter).ToListAsync();
        }
        catch (RequestFailedException ex)
        {
            throw new TableRepositoryException("The table query failed", ex);
        }
    }
}