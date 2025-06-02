using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DinnerPlansCommon;
using DinnerPlansAPI.Repositories;

namespace DinnerPlansAPI;

public class DinnerPlansMenu
{
    private readonly ITableRepository<MenuEntity> menuRepo;
    private readonly ITableRepository<MealEntity> mealRepo;

    public DinnerPlansMenu(
        ITableRepository<MenuEntity> menuRepository,
        ITableRepository<MealEntity> mealReposistory)
    {
        menuRepo = menuRepository;
        mealRepo = mealReposistory;
    }

    [Function("GetMenuByDates")]
    public async Task<IActionResult> GetMenuByDates(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "menu")] HttpRequest req,
        ILogger log)
    {
        DateRange dateRange = await JsonSerializer.DeserializeAsync<DateRange>(req.Body);
        string startDate = dateRange.StartDate.ToString("yyyy.MM.dd");
        string endDate = dateRange.EndDate.ToString("yyyy.MM.dd");

        log.LogInformation($"Menu | GET | Menu from {startDate} to {endDate}");
        
        IReadOnlyCollection<MenuEntity> menuEntities = await menuRepo.QueryEntityAsync(menu => menu.PartitionKey == "menu" 
                                                                    && menu.Date >= dateRange.StartDate 
                                                                    && menu.Date <= dateRange.EndDate);

        var menuRange = new MenuRange(new List<Menu>());
        
        foreach (var menuEntity in menuEntities)
        {
            MealEntity mealEntity = null;
            MealEntity removedMealEntity = null;
            try
            {
                if (!string.IsNullOrEmpty(menuEntity.MealId))
                {
                    mealEntity = await mealRepo.GetEntityAsync(menuEntity.MealId);
                }
                if (!string.IsNullOrEmpty(menuEntity.RemovedMealId))
                {
                    removedMealEntity = await mealRepo.GetEntityAsync(menuEntity.RemovedMealId);
                }
            }
            catch (TableRepositoryException ex)
            {
                log.LogWarning(ex.Message);
            }

            menuRange.Menus.Add(new Menu(menuEntity.Date, 
                                            mealEntity?.ConvertToMeal(), 
                                            removedMealEntity?.ConvertToMeal()));
        }

        string menuResult = JsonSerializer.Serialize<MenuRange>(menuRange);
        
        return new OkObjectResult(menuResult);
    }

    [Function("CreateMenu")]
    public async Task<IActionResult> CreateMenu(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "menu")] HttpRequest req,
        ILogger log)
    {
        Menu menu = await req.ReadFromJsonAsync<Menu>();

        log.LogInformation($"Menu | PUT | Create new menu for {menu.Date.ToString("yyyy.MM.dd")}");

        MenuEntity menuEntity = menu.ConvertToMenuEntity(menuRepo.PartitionKey);

        try
        {
            await menuRepo.AddEntityAsync(menuEntity);   
        }
        catch (TableRepositoryException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }

        return new OkResult();
    }

    [Function("UpdateMenu")]
    public async Task<IActionResult> UpdateMenu(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "menu")] HttpRequest req,
        ILogger log)
    {
        Menu menu = await req.ReadFromJsonAsync<Menu>();

        log.LogInformation($"Menu | POST | Update Menu for {menu.Date.ToString("yyyy.MM.dd")}");

        MenuEntity menuEntity = menu.ConvertToMenuEntity(menuRepo.PartitionKey);
        log.LogInformation($"Updating Menu Entity...\nRowKey: {menuEntity.RowKey}\nDate: {menuEntity.Date}\nMealId: {menuEntity.MealId}");
        
        try
        {
            await menuRepo.UpsertEntityAsync(menuEntity);   
        }
        catch (TableRepositoryException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }

        return new OkResult();
    }

    [Function("GetTodaysMenu")]
    public async Task<IActionResult> GetTodaysMenu(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/today")] HttpRequest req,
        ILogger log)
    {
        string today = DateTime.UtcNow.ToEasternStandardTime().ToString("yyyy.MM.dd");

        log.LogInformation($"Menu | GET | Menu from today - {today}");
        
        MenuEntity menuEntity = null;
        try
        {
            menuEntity = await menuRepo.GetEntityAsync(today);
        }
        catch (TableRepositoryException)
        {
            return new OkObjectResult("There's nothing on the menu for today :(").DefineResultAsPlainTextContent(StatusCodes.Status200OK);
        }

        MealEntity mealEntity = null;
        try
        {
            if (!string.IsNullOrEmpty(menuEntity.MealId))
            {
                mealEntity = await mealRepo.GetEntityAsync(menuEntity.MealId);
            }
        }
        catch (RequestFailedException ex)
        {
            log.LogWarning(ex.Message);
        }

        return req.ContentType switch
        {
            "application/json" => new OkObjectResult(mealEntity.ConvertToMeal()),
            "text/html" => new OkObjectResult(mealEntity.ConvertToMeal().ConvertToHtml()),
            _ => new OkObjectResult(mealEntity.Name).DefineResultAsPlainTextContent(StatusCodes.Status200OK)
        };
    }

    [Function("GetTomorrowsMenu")]
    public async Task<IActionResult> GetTomorrowsMenu(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/tomorrow")] HttpRequest req,
        ILogger log)
    {
        string tomorrow = DateTime.UtcNow.ToEasternStandardTime().AddDays(1).ToString("yyyy.MM.dd");

        log.LogInformation($"Menu | GET | Menu from tomorrow - {tomorrow}");
        
        MenuEntity menuEntity = null;
        try
        {
            menuEntity = await menuRepo.GetEntityAsync(tomorrow);
        }
        catch (TableRepositoryException)
        {
            return new OkObjectResult("There's nothing on the menu for tomorrow :(").DefineResultAsPlainTextContent(StatusCodes.Status200OK);
        }

        MealEntity mealEntity = null;
        try
        {
            if (!string.IsNullOrEmpty(menuEntity.MealId))
            {
                mealEntity = await mealRepo.GetEntityAsync(menuEntity.MealId);
            }
        }
        catch (RequestFailedException ex)
        {
            log.LogWarning(ex.Message);
        }

        return req.ContentType switch
        {
            "application/json" => new OkObjectResult(mealEntity.ConvertToMeal()),
            "text/html" => new OkObjectResult(mealEntity.ConvertToMeal().ConvertToHtml()),
            _ => new OkObjectResult(mealEntity.Name).DefineResultAsPlainTextContent(StatusCodes.Status200OK)
        };
    }
}