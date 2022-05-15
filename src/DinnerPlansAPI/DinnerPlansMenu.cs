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
using System;

namespace DinnerPlansAPI
{
    public static class DinnerPlansMenu
    {
        private const string mealTableName = "meals";
        private const string mealPartitionKey = "meal";
        private const string menuTableName = "menu";
        private const string menuPartitionKey = "menu";
        private const string catagoriesTableName = "catagories";
        private const string catagoriesPartionKey = "catagory";

        [FunctionName("GetMenuByDates")]
        public static async Task<IActionResult> GetMealByDates(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "menu")] HttpRequest req,
            [Table(menuTableName, Connection = "DinnerPlansTableConnectionString")] TableClient menuTable,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log)
        {
            DateRange dateRange = await JsonSerializer.DeserializeAsync<DateRange>(req.Body);
            string startDate = dateRange.StartDate.ToString("yyyy.MM.dd");
            string endDate = dateRange.EndDate.ToString("yyyy.MM.dd");

            log.LogInformation($"Meal | GET | Menu from {startDate} to {endDate}");
            
            AsyncPageable<MenuEntity> menuResults;
            try
            {
                menuResults = menuTable.QueryAsync<MenuEntity>(menu => menu.PartitionKey == menuPartitionKey 
                                                                    && menu.Date >= dateRange.StartDate 
                                                                    && menu.Date <= dateRange.EndDate);
                // menuResults = menuTable.QueryAsync<MenuEntity>(menu => menu.PartitionKey == menuPartitionKey);
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
                    mealEntity = await mealTable.GetEntityAsync<MealEntity>(mealPartitionKey, menuEntity.MealId);
                    removedMealEntity = await mealTable.GetEntityAsync<MealEntity>(mealPartitionKey, menuEntity.RemovedMealId);
                }
                catch (RequestFailedException ex)
                {
                    log.LogWarning(ex.Message);
                }

                menuRange.Menus.Add(new Menu(menuEntity.Date, 
                                             mealEntity.ConvertToMeal(), 
                                             removedMealEntity.ConvertToMeal()));
            }

            string menuResult = JsonSerializer.Serialize<MenuRange>(menuRange);
            
            return new OkObjectResult(menuResult);
        }
    }
}
