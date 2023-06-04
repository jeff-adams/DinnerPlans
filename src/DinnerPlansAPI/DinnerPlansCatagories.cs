using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using DinnerPlansAPI.Repositories;

namespace DinnerPlansAPI;

public class DinnerPlansCatagories
{
    private readonly ITableRepository<CatagoryEntity> catagoryRepo;

    public DinnerPlansCatagories(ITableRepository<CatagoryEntity> catagoryRepository)
    {
        catagoryRepo = catagoryRepository;
    }

    [FunctionName("GetCatagories")]
    public async Task<IActionResult> GetCatagories(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "catagories")] HttpRequest req,
        ILogger log)
    {
        log.LogInformation($"Catagory | GET | All Catagories");
        IReadOnlyCollection<CatagoryEntity> catagories;
        try
        {
            catagories = await catagoryRepo.QueryEntityAsync(catagory => catagory.PartitionKey  == catagoryRepo.PartitionKey);
        }
        catch (TableRepositoryException)
        {
            return new OkObjectResult(new JsonResult(new EmptyResult()));
        }

        return new OkObjectResult(catagories);
    }
}