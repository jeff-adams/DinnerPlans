using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using System.Linq.Expressions;

namespace DinnerPlansAPI.Repositories;

public class MenuTableRepository : IMenuRepository
{
    private TableClient client;
    private const string menuTableName = "menu";
    private const string menuPartitionKey = "menu";
    
    public MenuTableRepository(IConfiguration config, TokenCredential creds)
    {
        Uri tableEndpoint = new (config["TableEndpoint"]);
        client = new TableClient(tableEndpoint, menuTableName, creds);
    }

    public async Task<MenuEntity> GetMenuEntityAsync(string menuKey)
    {
        MenuEntity menuEntity;
        try
        {
            menuEntity = await client.GetEntityAsync<MenuEntity>(menuPartitionKey, menuKey);
        }
        catch (RequestFailedException ex)
        {
            throw new MenuRepositoryException($"Unable to get the menu for {menuKey}", ex);
        }

        return menuEntity;
    }

    public async Task AddMenuEntityAsync(MenuEntity menu)
    {
        try
        {
            Response response = await client.AddEntityAsync<MenuEntity>(menu);   
        }
        catch (RequestFailedException ex)
        {
            throw new MenuRepositoryException($"Unable to create the menu for {menu.RowKey}", ex);
        }
    }

    public async Task UpdateMenuEntityAsync(MenuEntity menu)
    {
        try
        {
            Response response = await client.UpdateEntityAsync<MenuEntity>(menu, Azure.ETag.All);
        }
        catch (RequestFailedException ex)
        {
            throw new MenuRepositoryException("Unable to update the menu", ex);
        }
    }

    public async Task<IReadOnlyCollection<MenuEntity>> QueryMenuEntityAsync(Expression<Func<MenuEntity, bool>> filter)
    {
        AsyncPageable<MenuEntity> results;
        try
        {
            results = client.QueryAsync<MenuEntity>(filter);
        }
        catch (RequestFailedException)
        {
            return Enumerable.Empty<MenuEntity>() as IReadOnlyCollection<MenuEntity>;
        }

        return await results.ToListAsync();
    }
}