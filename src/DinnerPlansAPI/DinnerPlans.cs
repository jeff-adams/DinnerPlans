using System.Linq;
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
    public static class DinnerPlans
    {
        private const string mealTableName = "meals";
        private const string mealPartitionKey = "meal";
        private const string menuTableName = "menu";
        private const string menuPartionKey = "menu";

        [FunctionName("GetMealById")]
        public static async Task<IActionResult> GetMealById(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "meal/{id}")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log,
            string id)
        {
            log.LogInformation($"Meal | GET | {id}");
            MealEntity meal = null;
            try
            {
                meal = await mealTable.GetEntityAsync<MealEntity>(mealPartitionKey, id);
            }
            catch (RequestFailedException)
            {
                return new BadRequestObjectResult($"There was no meal found with ID: {id}");
            }

            return new OkObjectResult(meal);
        }

        [FunctionName("GetMeals")]
        public static async Task<IActionResult> GetMeals(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "meals")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log)
        {
            log.LogInformation($"Meal | GET | meals");
            AsyncPageable<MealEntity> mealsResults = null;
            try
            {
                mealsResults = mealTable.QueryAsync<MealEntity>(meal => meal.PartitionKey == mealPartitionKey);
            }
            catch (RequestFailedException)
            {
                return new BadRequestObjectResult($"The table did not contain any meals");
            }

            List<MealEntity> meals = await mealsResults.ToListAsync();

            return new OkObjectResult(meals);
        }
    }
}
