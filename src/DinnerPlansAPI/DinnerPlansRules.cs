using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinnerPlansAPI.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DinnerPlansAPI;

public class DinnerPlansRules
{
    private readonly ITableRepository<RuleEntity> ruleRepo;
    private readonly ILogger<DinnerPlansRules> log;

    public DinnerPlansRules(
        ITableRepository<RuleEntity> ruleRepository,
        ILogger<DinnerPlansRules> logger)
    {
        ruleRepo = ruleRepository;
        log = logger;
    }

    [Function(nameof(GetSeasons))]
    public async Task<IActionResult> GetSeasons(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "seasons")] HttpRequest req)
    {
        log.LogInformation("{FunctionName} | {Type} | All Seasons", nameof(GetSeasons), "GET");
        IReadOnlyCollection<RuleEntity> seasonEntities;
        try
        {
            seasonEntities = await ruleRepo.QueryEntityAsync(rule => rule.PartitionKey == "seasons");
            log.LogInformation("{FunctionName} | {Type} | Retrieved {RuleCount} seasons", nameof(GetSeasons), "GET", seasonEntities.Count);
        }
        catch (TableRepositoryException ex)
        {
            log.LogError("{FunctionName} | {Type} | Failed to get seasons: {ErrorMessage}", nameof(GetSeasons), "GET", ex.Message);
            return new BadRequestObjectResult(new JsonResult(new EmptyResult()));
        }

        return new OkObjectResult(seasonEntities.Select(x => x.ConvertToRule()));
    }

    [Function(nameof(UpdateSeason))]
    public async Task<IActionResult> UpdateSeason(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "seasons/{seasonId}")] HttpRequest req,
        string seasonId)
    {
        log.LogInformation("{FunctionName} | {Type} | Updating season {SeasonId}", nameof(UpdateSeason), "PUT", seasonId);
        RuleEntity seasonEntity;
        try
        {
            seasonEntity = await ruleRepo.GetEntityAsync(seasonId);
            if (seasonEntity is null)
            {
                log.LogWarning("{FunctionName} | {Type} | The season {SeasonId} was not found", nameof(UpdateSeason), "PUT", seasonId);
                return new NotFoundObjectResult(new JsonResult(new EmptyResult()));
            }
        }
        catch (TableRepositoryException ex)
        {
            log.LogError("{FunctionName} | {Type} | Failed to retrieve the season {SeasonId}: {ErrorMessage}", nameof(UpdateSeason), "PUT", seasonId, ex.Message);
            return new BadRequestObjectResult(new JsonResult(new EmptyResult()));
        }

        // TODO: Get the start and end date from the request body and update the existingRule with the new values

        try
        {
            await ruleRepo.UpsertEntityAsync(seasonEntity);
            log.LogInformation("{FunctionName} | {Type} | Successfully updated season with id: {SeasonId}", nameof(UpdateSeason), "PUT", seasonId);
        }
        catch (TableRepositoryException ex)
        {
            log.LogError("{FunctionName} | {Type} | Failed to update season with id: {SeasonId}: {ErrorMessage}", nameof(UpdateSeason), "PUT", seasonId, ex.Message);
            return new BadRequestObjectResult(new JsonResult(new EmptyResult()));
        }
        
        return new OkObjectResult(seasonEntity.ConvertToRule());
    }
}