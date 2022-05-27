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

namespace DinnerPlansAPI;

public static class DinnerPlansCatagories
{
    private const string catagoriesTableName = "catagories";
    private const string catagoriesPartionKey = "catagory";

    [FunctionName("GetCatagories")]
    public static async Task<IActionResult> GetCatagories(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "catagories")] HttpRequest req,
        [Table(catagoriesTableName, Connection = "DinnerPlansTableConnectionString")] TableClient catagoryTable,
        ILogger log)
    {
        log.LogInformation($"Catagory | GET | All Catagories");
        AsyncPageable<TableEntity> catagoryResults;
        try
        {
            catagoryResults = catagoryTable.QueryAsync<TableEntity>(catagory => catagory.PartitionKey  == catagoriesPartionKey);
        }
        catch (RequestFailedException)
        {
            return new OkObjectResult(new JsonResult(new EmptyResult()));
        }

        List<string> catagories = await catagoryResults.Select(row => row.RowKey).ToListAsync();
        return new OkObjectResult(catagories);
    }
}