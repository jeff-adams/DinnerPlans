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

    [FunctionName("DailyMealUpdater")]
    public async Task MealUpdaterBot(
        [TimerTrigger("%MealDailyUpdatorInterval%")] TimerInfo timer,
        ILogger log
    )
    {
        // Triggers every day at 23:30
        log.LogInformation($"DailyMealUpdater | Timer | Updating today's meals 'LastOnMenu' date");
        // Get todays meal
        MenuEntity todaysMenu;
        MealEntity todaysMeal;
        string today = DateTime.Today.ToString("yyyy.MM.dd");

        try
        {
            todaysMenu = await menuRepo.GetEntityAsync(today);
        }
        catch (TableRepositoryException)
        {
            log.LogInformation($"DailyMealUpdater | Timer | There is no menu for {today}");
            return;
        }

        try
        {
            todaysMeal = await mealRepo.GetEntityAsync(todaysMenu.MealId);
        }
        catch (TableRepositoryException ex)
        {
            log.LogError(ex, $"DailyMealUpdater | Timer | Unable to find the meal {todaysMenu.MealId}");
            return;
        }

        // Update the meal with new dates
        todaysMeal.NextOnMenu = null;
        todaysMeal.LastOnMenu = DateTime.Today;
        
        try
        {
            await mealRepo.UpdateEntityAsync(todaysMeal);
        }
        catch (TableRepositoryException ex)
        {
            log.LogError(ex, $"DailyMealUpdater | Timer | Unable to update the meal {todaysMenu.MealId}");
        }
    }

    [FunctionName("TimedMenuUpdater")]
    public async Task MenuUpdaterBot(
        [TimerTrigger("%MenuUpdatorInterval%")] TimerInfo timer,
        ILogger log
    )
    {
        // Check menu for meals in the next 30 days and select a random meal for each
        for (int i = 0; i < 30; i++)
        {
            DateTime date = DateTime.Today.AddDays(i);
            string dateString = date.ToString("yyyy.MM.dd");

            MenuEntity menuEntity = null;
            try
            {
                menuEntity = await menuRepo.GetEntityAsync(dateString);
            }
            catch (TableRepositoryException)
            {
                log.LogInformation($"MenuUpdatorBot | Timer | No menu found for {dateString}");
            }

            if (menuEntity is not null && !string.IsNullOrEmpty(menuEntity.MealId))
            {
                log.LogInformation($"MenuUpdatorBot | Timer | Menu found for {dateString} already has a scheduled meal: [{menuEntity.MealId}]");
                continue;
            }
            
            string selectedMealId = string.Empty;
            do
            {
                selectedMealId = await RandomMealByDateAsync(date, log);
                selectedMealId = selectedMealId != menuEntity.RemovedMealId ? selectedMealId : string.Empty;
            } while (string.IsNullOrEmpty(selectedMealId)); 

            menuEntity = new ()
            {
                PartitionKey = menuRepo.PartitionKey,
                Date = date,
                MealId = selectedMealId
            };

            try
            {
                await menuRepo.UpsertEntityAsync(menuEntity);
            }
            catch (TableRepositoryException)
            {
                log.LogError($"MenuUpdatorBot | Timer | Unable to upsert the menu for {dateString}");
                continue;
            }

            MealEntity mealEntity = null;

            try
            {
                mealEntity = await mealRepo.GetEntityAsync(selectedMealId);
                mealEntity.NextOnMenu = date;
            }
            catch (TableRepositoryException)
            {
                log.LogError($"MenuUpdatorBot | Timer | Unable to get the meal [{selectedMealId}] to update the 'NextOnMenu' date to [{dateString}]");
                continue;
            }

            try
            {
                await mealRepo.UpdateEntityAsync(mealEntity);
            }
            catch (TableRepositoryException)
            {
                log.LogError($"MenuUpdatorBot | Timer | Unable to update the meal [{mealEntity.Id}] 'NextOnMenu' date to [{dateString}]");
                continue;
            }
        }   
    }

    [FunctionName("MealChooserBot")]
    public async Task<IActionResult> ChooseMeal(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "bot/choose_meal")] HttpRequest req,
        ILogger log)
    {
        string dateQuery = req.Query["date"];
        bool isValidDate = DateTime.TryParse(dateQuery, out DateTime dateResult);
        if (!isValidDate)
        {
            log.LogError($"MealChooserBot | GET | Recieved invalid DateTime format in query: [{dateQuery}]");
            return new BadRequestObjectResult($"Please provide a DateTime query, 'api/choose_meal?date=<DateTime>'");
        }

        string date = dateResult.ToString("yyyy.MM.dd");
        log.LogInformation($"MealChooserBot | GET | Choose random meal for {date}");

       string selectedMealId = await RandomMealByDateAsync(dateResult, log);

       return new OkObjectResult(selectedMealId);
    }

    private async Task<string> RandomMealByDateAsync(DateTime date, ILogger log)
    {
        string mealId = await QueryForSpecialMealIdAsync(date.ToString("MMdd"), log);
        if (!string.IsNullOrEmpty(mealId))
        {
            log.LogInformation($"MealChooserBot | GET | Found special meal for {date.ToString("yyyy.MM.dd")}: [{mealId}]");
            return mealId;
        }

        IEnumerable<Meal> meals = await GetAllMealsNotOnMenuAsync(log);
        meals = await FilterMealsOnDayOfWeekAsync(date, meals, log);
        meals = await FilterMealsOnSeasonAsync(date, meals, log);
        Meal[] filteredMeals = meals.ToArray();
        log.LogInformation($"MealChooserBot | GET | Filtered list of meals down to {filteredMeals.Length} meals");

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
        log.LogInformation($"MealChooserBot | GET | Number of meals: {weights.Length} | Total weight: {totalWeight} | Randomly selected index: {randomIndex}");
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
        log.LogInformation("MealChooserBot | GET | Querying for all meals not currently on the menu");
        IReadOnlyCollection<MealEntity> mealEntities = await mealRepo.QueryEntityAsync(meal => 
            meal.PartitionKey == mealRepo.PartitionKey
            && meal.NextOnMenu <= DateTime.Now);
        return mealEntities.Select(mealEntity => mealEntity.ConvertToMeal());
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnSeasonAsync(DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        log.LogInformation("MealChooserBot | GET | Querying for the rule's definition of seasons");
        IReadOnlyCollection<RuleEntity> seasonRules = await ruleRepo.QueryEntityAsync(x => x.PartitionKey == "seasons");
        string season = seasonRules.Where(rule => dateResult >= DateTime.Parse(rule.Start) && dateResult <= DateTime.Parse(rule.End))
                                   .Select(rule => rule.RowKey)
                                   .First();
        log.LogInformation($"MealChooserBot | GET | Filter meals by the season: {season}");
        return meals.Where(meal => meal.Seasons.Contains(season));
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnDayOfWeekAsync(DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        string day = dateResult.DayOfWeek.ToString();
        log.LogInformation($"MealChooserBot | GET | Querying for rules associated with the day of the week: {day}");
        RuleEntity rule = await ruleRepo.GetEntityAsync(day);
        log.LogInformation($"MealChooserBot | GET | Filtering on meals from these catagories: {rule.Catagories}");
        string[] catagories = rule.Catagories.Split(',');
        return meals.Where(meal => meal.Catagories.Any(catagory => catagories.Contains(catagory)));
    }

    private async Task<string> QueryForSpecialMealIdAsync(string date, ILogger log)
    {
        log.LogInformation($"MealChooserBot | GET | Quering for special meals planned for {date}");

        string mealId = string.Empty;
        try
        {
            SpecialDateEntity specialDateEntity = await specialDateRepo.GetEntityAsync(date);
            mealId = specialDateEntity.MealId;
        }
        catch (TableRepositoryException)
        {
            log.LogInformation($"MealChooserBot | GET | No special meals planned for {date}");
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
