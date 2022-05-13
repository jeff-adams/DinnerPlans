using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DinnerPlansCommon;
using Azure.Data.Tables;
using Azure;

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
    }
}
