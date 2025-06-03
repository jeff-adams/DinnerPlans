using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DinnerPlansAPI.Repositories;

namespace DinnerPlansAPI;

public class DinnerPlansCatagories
{
    private readonly ITableRepository<CatagoryEntity> catagoryRepo;
    private readonly ILogger<DinnerPlansCatagories> log;

    public DinnerPlansCatagories(
        ITableRepository<CatagoryEntity> catagoryRepository,
        ILogger<DinnerPlansCatagories> logger)
    {
        catagoryRepo = catagoryRepository;
        log = logger;
    }

    [Function("GetCatagories")]
    public async Task<IActionResult> GetCatagories(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "catagories")] HttpRequest req)
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