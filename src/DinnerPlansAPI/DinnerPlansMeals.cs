using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DinnerPlansCommon;
using DinnerPlansAPI.Repositories;

namespace DinnerPlansAPI;

public class DinnerPlansMeals
{
    private JsonSerializerOptions jsonOptions = new JsonSerializerOptions() 
    { 
        PropertyNameCaseInsensitive = true, 
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
    };

    private readonly ITableRepository<MealEntity> mealRepo;
    private readonly ITableRepository<CatagoryEntity> catagoryRepo;
    private readonly ILogger<DinnerPlansMeals> log;

    public DinnerPlansMeals(
        ITableRepository<MealEntity> mealRespository,
        ITableRepository<CatagoryEntity> catagoryRepository,
        ILogger<DinnerPlansMeals> logger
    )
    {
        mealRepo = mealRespository;
        catagoryRepo = catagoryRepository;
        log = logger;
    }

    [Function("GetMealById")]
    public async Task<IActionResult> GetMealById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "meal/{id}")] HttpRequest req,
        string id)
    {
        log.LogInformation($"Meal | GET | Meal - {id}");
        MealEntity mealEntity;
        try
        {
            mealEntity = await mealRepo.GetEntityAsync(id);
        }
        catch (TableRepositoryException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
        
        return new OkObjectResult(mealEntity.ConvertToMeal());
    }

    [Function("GetMeals")]
    public async Task<IActionResult> GetMeals(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "meals")] HttpRequest req)
    {
        log.LogInformation($"Meal | GET | All Meals");
        IReadOnlyCollection<MealEntity> mealEntities;
        try
        {
            mealEntities = await mealRepo.QueryEntityAsync(meal => meal.PartitionKey  == mealRepo.PartitionKey);
        }
        catch (TableRepositoryException)
        {
            return new OkObjectResult(new JsonResult(new EmptyResult()));
        }

        IEnumerable<Meal> meals = mealEntities.Select(mealEntity => mealEntity.ConvertToMeal());

        return new OkObjectResult(meals);
    }

    [Function("CreateMeal")]
    public async Task<IActionResult> CreateMeal(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "meal")] HttpRequest req)
    {
        log.LogInformation($"Meal | PUT | Create New Meal");

        Meal meal = await req.ReadFromJsonAsync<Meal>(options: jsonOptions);
        MealEntity mealEntity = meal.ConvertToMealEntity(mealRepo.PartitionKey);
        try
        {
            await mealRepo.AddEntityAsync(mealEntity);
        }
        catch (TableRepositoryException ex)
        {
            return new BadRequestObjectResult(ex);
        }

        await UpdateOrAddCatagories(meal.Catagories);
    
        return new OkObjectResult(mealEntity.Id).DefineResultAsPlainTextContent(StatusCodes.Status201Created);
    }
    
    [Function("UpdateMeal")]
    public async Task<IActionResult> UpdateMeal(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "meal")] HttpRequest req)
    {
        Meal meal = await req.ReadFromJsonAsync<Meal>(options: new JsonSerializerOptions{ PropertyNameCaseInsensitive = true });
        
        log.LogInformation($"Meal | POST | Update Meal - {meal.Name} [{meal.Id}]");
        
        MealEntity mealEntity = meal.ConvertToMealEntity(mealRepo.PartitionKey);
        try
        {
            await mealRepo.UpsertEntityAsync(mealEntity);
        }
        catch (RequestFailedException ex)
        {
            return new BadRequestObjectResult(ex);
        }

        await UpdateOrAddCatagories(meal.Catagories);

        return new OkResult();
    }

    [Function("DeleteMeal")]
    public async Task<IActionResult> DeleteMeal(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "meal/{id}")] HttpRequest req,
        string id)
    {
        log.LogInformation($"Meal | DELETE | Meal - {id}");
        try
        {
            await mealRepo.DeleteEntityAsync(id);
        }
        catch (RequestFailedException ex)
        {
            return new BadRequestObjectResult(ex);
        }
        return new OkResult();
    }

    private async Task UpdateOrAddCatagories(string[] catagories)
    {
        foreach (string catagory in catagories)
        {
            CatagoryEntity catagoryEntity = new ();
            catagoryEntity.PartitionKey = catagoryRepo.PartitionKey;
            catagoryEntity.RowKey = catagory;

            try
            {
                await catagoryRepo.UpsertEntityAsync(catagoryEntity);
            }
            catch (RequestFailedException ex)
            {
                log.LogError($"Unable to upsert the catagory [{catagory}]", ex);
            }
        }
    }
}