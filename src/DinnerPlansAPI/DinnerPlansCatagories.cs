using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using DinnerPlansAPI.Repositories;
using System.Linq;
using Microsoft.Azure.Functions.Worker;

namespace DinnerPlansAPI;

public class DinnerPlansCatagories
{
    private readonly ITableRepository<CatagoryEntity> catagoryRepo;

    public DinnerPlansCatagories(ITableRepository<CatagoryEntity> catagoryRepository)
    {
        catagoryRepo = catagoryRepository;
    }

    [Function("GetCatagories")]
    public async Task<IActionResult> GetCatagories(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "catagories")] HttpRequest req,
        ILogger log)
    {
        log.LogInformation($"Catagory | GET | All Catagories");
        IReadOnlyCollection<CatagoryEntity> catagoryEntities;
        try
        {
            catagoryEntities = await catagoryRepo.QueryEntityAsync(catagory => catagory.PartitionKey  == catagoryRepo.PartitionKey);
            log.LogInformation($"There are {catagoryEntities.Count} catagories");
        }
        catch (TableRepositoryException)
        {
            return new OkObjectResult(new JsonResult(new EmptyResult()));
        }

        IEnumerable<string> catagories = catagoryEntities.Select(x => x.RowKey);

        return new OkObjectResult(catagories);
    }
}