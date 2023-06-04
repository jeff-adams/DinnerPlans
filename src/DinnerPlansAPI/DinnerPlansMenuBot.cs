using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.Http;
using DinnerPlansCommon;
using DinnerPlansAPI.Repositories;

namespace DinnerPlansAPI;

public class DinnerPlansMenuBot
{
    private readonly ITableRepository<MealEntity> mealRepo;
    private readonly ITableRepository<MenuEntity> menuRepo;
    private readonly ITableRepository<SpecialDateEntity> specialDateRepo;
    private readonly ITableRepository<RuleEntity> ruleRepo;

    public DinnerPlansMenuBot(
        ITableRepository<MealEntity> mealRespository,
        ITableRepository<MenuEntity> menuRepository,
        ITableRepository<SpecialDateEntity> specialDatesRepository,
        ITableRepository<RuleEntity> ruleRepository
    )
    {
        mealRepo = mealRespository;
        menuRepo = menuRepository;
        specialDateRepo = specialDatesRepository;
        ruleRepo = ruleRepository;
    }


    [FunctionName("TimedMenuUpdater")]
    public async Task MenuUpdaterBot(
        [TimerTrigger("%MenuUpdatorInterval%")] TimerInfo timer,
        ILogger log
    )
    {
        // Runs every 5 minutes???
        //Check menu for meals in the next 30 days
        //For each day that does not have a meal    
            //Choose meal
            //Update the menu with the selected meal
    }

    [FunctionName("MealChooser")]
    public async Task<IActionResult> ChooseMeal(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "choose_meal")] HttpRequest req,
        ILogger log)
    {
        string dateQuery = req.Query["date"];
        bool isValidDate = DateTime.TryParse(dateQuery, out DateTime dateResult);
        if (!isValidDate)
        {
            log.LogError($"MealChooser recieved invalid DateTime format in query: [{dateQuery}]");
            return new BadRequestObjectResult($"Please provide a DateTime query, 'api/choose_meal?date=<DateTime>'");
        }

        string date = dateResult.ToString("yyyy.MM.dd");
        log.LogInformation($"MealChooser | GET | Choose random meal for {date}");

       string selectedMealId = await RandomMealByDateAsync(dateResult, log);

       return new OkObjectResult(selectedMealId);
    }

    private async Task<string> RandomMealByDateAsync(DateTime date, ILogger log)
    {
        string mealId = await QueryForSpecialMealIdAsync(date.ToString("MMdd"), log);
        if (!string.IsNullOrEmpty(mealId))
        {
            return mealId;
        }

        IEnumerable<Meal> meals = await GetAllMealsNotOnMenuAsync(log);
        meals = await FilterMealsOnDayOfWeekAsync(date, meals, log);
        meals = await FilterMealsOnSeasonAsync(date, meals, log);
        Meal[] filteredMeals = meals.ToArray();

        // create weights
        int totalWeight = 0;
        int[] weights = filteredMeals
            .Select(meal => 
                { 
                    int mealWeight = CalculateMealWeight(meal); 
                    totalWeight += mealWeight; 
                    return mealWeight;
                })
            .ToArray();

        // randomly choose weighted meal
        int randomIndex = new Random().Next(totalWeight) + 1;
        log.LogInformation($"Number of meals: {weights.Length} | Total weight: {totalWeight} | Randomly selected index: {randomIndex}");
        string selectedMealId = string.Empty;
        for (int i = 0; i < weights.Length; i++)
        {
            randomIndex -= weights[i];
            if (randomIndex <= 0)
            {
                selectedMealId = filteredMeals[i].Id;
                break;
            }
        }

        return selectedMealId;
    }

    private async Task<IEnumerable<Meal>> GetAllMealsNotOnMenuAsync(ILogger log)
    {
        log.LogInformation("Querying for all meals not currently on the menu");
        IReadOnlyCollection<MealEntity> mealEntities = await mealRepo.QueryEntityAsync(meal => meal.PartitionKey == mealRepo.PartitionKey);
        return mealEntities.Select(mealEntity => mealEntity.ConvertToMeal());
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnSeasonAsync(DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        log.LogInformation("Querying for the rule's definition of seasons");
        IReadOnlyCollection<RuleEntity> seasonRules = await ruleRepo.QueryEntityAsync(x => x.PartitionKey == "seasons");
        string season = seasonRules.Where(rule => dateResult >= DateTime.Parse(rule.Start) && dateResult <= DateTime.Parse(rule.End))
                                   .Select(rule => rule.RowKey)
                                   .First();
        log.LogInformation($"Filter meals by the season: {season}");
        return meals.Where(meal => meal.Seasons.Contains(season));
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnDayOfWeekAsync(DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        string day = dateResult.DayOfWeek.ToString();
        log.LogInformation($"Querying for rules associated with the day of the week: {day}");
        RuleEntity rule = await ruleRepo.GetEntityAsync(day);
        log.LogInformation($"Filtering on meals from these catagories: {rule.Catagories}");
        string[] catagories = rule.Catagories.Split(',');
        return meals.Where(meal => meal.Catagories.Any(catagory => catagories.Contains(catagory)));
    }

    private async Task<string> QueryForSpecialMealIdAsync(string date, ILogger log)
    {
        string mealId = string.Empty;

        try
        {
            SpecialDateEntity specialDateEntity = await specialDateRepo.GetEntityAsync(date);
            mealId = specialDateEntity.MealId;
        }
        catch (TableRepositoryException)
        {
            log.LogInformation($"No special meals planned for {date}");
        }

        return mealId;
    }

    private int CalculateMealWeight(Meal meal)
    {
        int weight = 0;
        
        DateTime start = meal.LastOnMenu ?? DateTime.Now.AddDays(-1);
        int dateWeight = (DateTime.Now - start).Days;

        weight += meal.Rating * dateWeight;

        return weight <= 1 ? 1 : weight;
    }
}
