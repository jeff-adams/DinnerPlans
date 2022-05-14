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
using System.Text.Json;
using System.IO;
using Microsoft.AspNetCore.Mvc.Formatters;

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
            MealEntity mealEntity = null;
            try
            {
                mealEntity = await mealTable.GetEntityAsync<MealEntity>(mealPartitionKey, id);
            }
            catch (RequestFailedException)
            {
                return new BadRequestObjectResult($"There was no meal found with ID: {id}");
            }
            
            return new OkObjectResult(mealEntity.ConvertToMeal());
        }

        [FunctionName("GetMeals")]
        public static async Task<IActionResult> GetMeals(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "meals")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log)
        {
            log.LogInformation($"Meal | GET | meals");
            AsyncPageable<MealEntity> mealsResults;
            try
            {
                mealsResults = mealTable.QueryAsync<MealEntity>(meal => meal.PartitionKey  == mealPartitionKey);
            }
            catch (RequestFailedException)
            {
                return new OkObjectResult(new JsonResult(new EmptyResult()));
            }

            List<MealEntity> mealEntities = await mealsResults.ToListAsync();
            var meals = mealEntities.Select(mealEntity => mealEntity.ConvertToMeal());

            return new OkObjectResult(meals);
        }

        [FunctionName("CreateMeal")]
        public static async Task<IActionResult> CreateMeal(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "meal")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log)
        {
            log.LogInformation($"Meal | PUT | meal");

            string reqBody = await req.ReadAsStringAsync();
            Meal meal = JsonSerializer.Deserialize<Meal>(reqBody);
            MealEntity mealEntity = meal.ConvertToMealEntity(mealPartitionKey);
            try
            {
                Response response = await mealTable.AddEntityAsync<MealEntity>(mealEntity);
            }
            catch (RequestFailedException ex)
            {
                return new BadRequestObjectResult(ex);
            }
            
            var result = new OkObjectResult(mealEntity.Id);

            var collection = new MediaTypeCollection();
            collection.Add("text/plain");

            result.ContentTypes = collection;
            result.StatusCode = StatusCodes.Status201Created;

            return result;
        }
        
        [FunctionName("UpdateMeal")]
        public static async Task<IActionResult> UpdateMeal(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "meal")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log)
        {
            log.LogInformation($"Meal | POST | meal");

            string reqBody = await req.ReadAsStringAsync();
            Meal meal = JsonSerializer.Deserialize<Meal>(reqBody);
            MealEntity mealEntity = meal.ConvertToMealEntity(mealPartitionKey);
            try
            {
                Response response = await mealTable.UpdateEntityAsync<MealEntity>(mealEntity, Azure.ETag.All);
            }
            catch (RequestFailedException ex)
            {
                return new BadRequestObjectResult(ex);
            }
            return new OkResult();
        }

        [FunctionName("DeleteMeal")]
        public static async Task<IActionResult> DeleteMeal(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "meal/{id}")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log,
            string id)
        {
            log.LogInformation($"Meal | DELETE | {id}");
            try
            {
                Response response = await mealTable.DeleteEntityAsync(mealPartitionKey, id);
            }
            catch (RequestFailedException ex)
            {
                return new BadRequestObjectResult(ex);
            }
            return new OkResult();
        }
    }
}
