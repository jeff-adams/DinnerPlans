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
    public static class DinnerPlansMeals
    {
        private const string mealTableName = "meals";
        private const string mealPartitionKey = "meal";
        private const string catagoriesTableName = "catagories";
        private const string catagoriesPartionKey = "catagory";

        [FunctionName("GetMealById")]
        public static async Task<IActionResult> GetMealById(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "meal/{id}")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log,
            string id)
        {
            log.LogInformation($"Meal | GET | Meal - {id}");
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
            log.LogInformation($"Meal | GET | All Meals");
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
            [Table(catagoriesTableName, Connection = "DinnerPlansTableConnectionString")] TableClient catagoryTable,
            ILogger log)
        {
            log.LogInformation($"Meal | PUT | Create New Meal");

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

            await UpdateOrAddCatagories(catagoryTable, meal.Catagories);
        
            return new OkObjectResult(mealEntity.Id).DefineResultAsPlainTextContent();
        }
        
        [FunctionName("UpdateMeal")]
        public static async Task<IActionResult> UpdateMeal(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "meal")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            [Table(catagoriesTableName, Connection = "DinnerPlansTableConnectionString")] TableClient catagoryTable,
            ILogger log)
        {
            string reqBody = await req.ReadAsStringAsync();
            Meal meal = JsonSerializer.Deserialize<Meal>(reqBody);
            
            log.LogInformation($"Meal | POST | Update Meal - {meal.Name} [{meal.Id}]");
            
            MealEntity mealEntity = meal.ConvertToMealEntity(mealPartitionKey);
            try
            {
                Response response = await mealTable.UpdateEntityAsync<MealEntity>(mealEntity, Azure.ETag.All);
            }
            catch (RequestFailedException ex)
            {
                return new BadRequestObjectResult(ex);
            }

            await UpdateOrAddCatagories(catagoryTable, meal.Catagories);

            return new OkResult();
        }

        [FunctionName("DeleteMeal")]
        public static async Task<IActionResult> DeleteMeal(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "meal/{id}")] HttpRequest req,
            [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
            ILogger log,
            string id)
        {
            log.LogInformation($"Meal | DELETE | Meal - {id}");
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

        private static async Task UpdateOrAddCatagories(TableClient catagoryTable, string[] catagories)
        {
            foreach (string catagory in catagories)
            {
                await catagoryTable.UpsertEntityAsync<TableEntity>(new TableEntity(catagoriesPartionKey, catagory));
            }
        }
    }
}
