using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Data.Tables;
using DinnerPlansCommon;

namespace DinnerPlansAPI
{
    public static class DinnerPlansMenu
    {
        private const string mealTableName = "meals";
        private const string mealPartitionKey = "meal";
        private const string menuTableName = "menu";
        private const string menuPartitionKey = "menu";

        [FunctionName("GetMenuByDates")]
        public static async Task<IActionResult> GetMenuByDates(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "menu")] HttpRequest req,
            [Table(menuTableName, Connection = "DinnerPlansTableConnectionString")] TableClient menuTable,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log)
        {
            DateRange dateRange = await JsonSerializer.DeserializeAsync<DateRange>(req.Body);
            string startDate = dateRange.StartDate.ToString("yyyy.MM.dd");
            string endDate = dateRange.EndDate.ToString("yyyy.MM.dd");

            log.LogInformation($"Menu | GET | Menu from {startDate} to {endDate}");
            
            AsyncPageable<MenuEntity> menuResults;
            try
            {
                menuResults = menuTable.QueryAsync<MenuEntity>(menu => menu.PartitionKey == menuPartitionKey 
                                                                    && menu.Date >= dateRange.StartDate 
                                                                    && menu.Date <= dateRange.EndDate);
            }
            catch (RequestFailedException)
            {
                return new OkObjectResult(new JsonResult(new EmptyResult()));
            }

            List<MenuEntity> menuEntities = await menuResults.ToListAsync();

            var menuRange = new MenuRange(new List<Menu>());
            
            foreach (var menuEntity in menuEntities)
            {
                MealEntity mealEntity = null;
                MealEntity removedMealEntity = null;
                try
                {
                    if (!string.IsNullOrEmpty(menuEntity.MealId))
                    {
                        mealEntity = await mealTable.GetEntityAsync<MealEntity>(mealPartitionKey, menuEntity.MealId);
                    }
                    if (!string.IsNullOrEmpty(menuEntity.RemovedMealId))
                    {
                        removedMealEntity = await mealTable.GetEntityAsync<MealEntity>(mealPartitionKey, menuEntity.RemovedMealId);
                    }
                }
                catch (RequestFailedException ex)
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

        [FunctionName("CreateMenu")]
        public static async Task<IActionResult> CreateMenu(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "menu")] HttpRequest req,
            [Table(menuTableName, Connection = "DinnerPlansTableConnectionString")] TableClient menuTable,
            ILogger log)
        {
            string reqBody = await req.ReadAsStringAsync();
            Menu menu = JsonSerializer.Deserialize<Menu>(reqBody);

            log.LogInformation($"Menu | PUT | Create new menu for {menu.Date.ToString("yyyy.MM.dd")}");

            MenuEntity menuEntity = menu.ConvertToMenuEntity(menuPartitionKey);

            try
            {
                Response response = await menuTable.AddEntityAsync<MenuEntity>(menuEntity);   
            }
            catch (RequestFailedException ex)
            {
                return new BadRequestObjectResult(ex);
            }

            return new OkResult();
        }

        [FunctionName("UpdateMenu")]
        public static async Task<IActionResult> UpdateMenu(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "menu")] HttpRequest req,
            [Table(menuTableName, Connection = "DinnerPlansTableConnectionString")] TableClient menuTable,
            ILogger log)
        {
            string reqBody = await req.ReadAsStringAsync();
            Menu menu = JsonSerializer.Deserialize<Menu>(reqBody);

            log.LogInformation($"Menu | POST | Update Menu for {menu.Date.ToString("yyyy.MM.dd")}");

            MenuEntity menuEntity = menu.ConvertToMenuEntity(menuPartitionKey);
            log.LogInformation($"Updating Menu Entity...\nRowKey: {menuEntity.RowKey}\nDate: {menuEntity.Date}\nMealId: {menuEntity.MealId}");
            try
            {
                Response response = await menuTable.UpdateEntityAsync<MenuEntity>(menuEntity, Azure.ETag.All);   
            }
            catch (RequestFailedException ex)
            {
                return new BadRequestObjectResult(ex);
            }

            return new OkResult();
        }

        [FunctionName("GetTodaysMenu")]
        public static async Task<IActionResult> GetTodaysMenu(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/today")] HttpRequest req,
            [Table(menuTableName, Connection = "DinnerPlansTableConnectionString")] TableClient menuTable,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log)
        {
            string today = DateTime.Now.ToString("yyyy.MM.dd");

            log.LogInformation($"Menu | GET | Menu from today - {today}");
            
            MenuEntity menuEntity = null;
            try
            {
                menuEntity = await menuTable.GetEntityAsync<MenuEntity>(menuPartitionKey, today);
            }
            catch (RequestFailedException)
            {
                return new OkObjectResult("There's nothing on the menu for today :(").DefineResultAsPlainTextContent(StatusCodes.Status204NoContent);
            }

            MealEntity mealEntity = null;
            try
            {
                if (!string.IsNullOrEmpty(menuEntity.MealId))
                {
                    mealEntity = await mealTable.GetEntityAsync<MealEntity>(mealPartitionKey, menuEntity.MealId);
                }
            }
            catch (RequestFailedException ex)
            {
                log.LogWarning(ex.Message);
            }
            
            return new OkObjectResult(mealEntity.Name).DefineResultAsPlainTextContent(StatusCodes.Status200OK);
        }
    }
}