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
using Microsoft.Extensions.Configuration;

namespace DinnerPlansAPI;

public class DinnerPlansMenuBot
{
    private readonly IConfiguration config;
    private readonly ITableRepository<MealEntity> mealRepo;
    private readonly ITableRepository<MenuEntity> menuRepo;
    private readonly ITableRepository<SpecialDateEntity> specialDateRepo;
    private readonly ITableRepository<RuleEntity> ruleRepo;

    public DinnerPlansMenuBot(
        IConfiguration config,
        ITableRepository<MealEntity> mealRespository,
        ITableRepository<MenuEntity> menuRepository,
        ITableRepository<SpecialDateEntity> specialDatesRepository,
        ITableRepository<RuleEntity> ruleRepository
    )
    {
        this.config = config;
        mealRepo = mealRespository;
        menuRepo = menuRepository;
        specialDateRepo = specialDatesRepository;
        ruleRepo = ruleRepository;
    }

    [FunctionName("DailyMealUpdator")]
    public async Task MealUpdatorBot(
        [TimerTrigger("%MealDailyUpdatorInterval%")] TimerInfo timer,
        ILogger log
    )
    {
        log.LogInformation($"DailyMealUpdator | Timer | Updating today's meals 'LastOnMenu' date");
        MenuEntity todaysMenu;
        MealEntity todaysMeal;
        string today = DateTime.Today.ToString("yyyy.MM.dd");

        try
        {
            todaysMenu = await menuRepo.GetEntityAsync(today);
        }
        catch (TableRepositoryException)
        {
            log.LogInformation($"DailyMealUpdator | Timer | There is no menu for {today}");
            return;
        }

        try
        {
            todaysMeal = await mealRepo.GetEntityAsync(todaysMenu.MealId);
        }
        catch (TableRepositoryException ex)
        {
            log.LogError(ex, $"DailyMealUpdator | Timer | Unable to find the meal {todaysMenu.MealId}");
            return;
        }

        // Update the meal with new dates
        if (!todaysMeal.Catagories.Contains("Special")) todaysMeal.NextOnMenu = null;
        todaysMeal.LastOnMenu = DateTime.Today.ToUniversalTime();
        
        try
        {
            await mealRepo.UpdateEntityAsync(todaysMeal);
        }
        catch (TableRepositoryException ex)
        {
            log.LogError(ex, $"DailyMealUpdator | Timer | Unable to update the meal {todaysMenu.MealId}");
        }
    }

    [FunctionName("TimedMenuUpdator")]
    public async Task MenuUpdatorBot(
        [TimerTrigger("%MenuUpdatorInterval%")] TimerInfo timer,
        ILogger log
    )
    {
        int numOfNewMenus = 0;
        int numOfDaysToPlan = int.TryParse(config["NumberOfDaysToMenuPlan"], out int result) ? result : 30;

        // Check menu for meals in the next 30 days and select a random meal for each
        for (int i = 0; i < numOfDaysToPlan; i++)
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
                if (menuEntity is not null) selectedMealId = selectedMealId != menuEntity.RemovedMealId ? selectedMealId : string.Empty;
            } while (string.IsNullOrEmpty(selectedMealId)); 

            menuEntity = new ()
            {
                PartitionKey = menuRepo.PartitionKey,
                Date = date.ToUniversalTime(),
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
                mealEntity.NextOnMenu = date.ToUniversalTime();
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

            log.LogInformation($"MenuUpdatorBot | Timer | Menu for {dateString} has been chosen: [{mealEntity.Name}] - [{mealEntity.Id}]");
            numOfNewMenus++;
        }

        log.LogInformation($"MenuUpdatorBot | Timer | Menus for [{numOfNewMenus}] days have been assigned");   
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

       Meal meal = null;
       try
       {
            MealEntity mealEntity = await mealRepo.GetEntityAsync(selectedMealId);
            meal = mealEntity.ConvertToMeal();
       }
       catch (TableRepositoryException)
       {
            log.LogError($"MealChooserBot | GET | Unable to retrieve the selected meal [{selectedMealId}]");
            return new BadRequestObjectResult($"Unable to retrieve the selected meal [{selectedMealId}]");
       }

       return new OkObjectResult(meal);
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
        log.LogInformation($"MealChooserBot | GET | Filtered list of meals down to {meals.Count()} meals");

        // create weights
        int totalWeight = 0;
        int[] weights = meals
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
                Meal selectedMeal = meals.ElementAt(i); // get element at?
                selectedMealId = selectedMeal.Id;
                log.LogInformation($"MealChooserBot | GET | Randomly selected Meal: {selectedMeal.Name} - {selectedMealId}");
                break;
            }
        }

        return selectedMealId;
    }

    private async Task<IEnumerable<Meal>> GetAllMealsNotOnMenuAsync(ILogger log)
    {
        log.LogInformation("MealChooserBot | GET | Querying for all meals not currently on the menu");
        IReadOnlyCollection<MealEntity> mealEntities = await mealRepo.QueryEntityAsync(meal => meal.PartitionKey == mealRepo.PartitionKey);

        IEnumerable<Meal> meals = mealEntities
            // Need to filter locally, as the repo query can't handle the null comparison
            .Where(mealEntity => mealEntity.NextOnMenu is null || mealEntity.NextOnMenu < DateTime.Today)
            .Select(mealEntity => mealEntity.ConvertToMeal());
        log.LogInformation($"MealChooserBot | GET | Found {meals.Count()} meals not currently on the menu");
        return meals;
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnSeasonAsync(DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        log.LogInformation("MealChooserBot | GET | Querying for the rule's definition of seasons");
        IReadOnlyCollection<RuleEntity> seasonRules = await ruleRepo.QueryEntityAsync(x => x.PartitionKey == "seasons");
        string season = seasonRules.Where(rule => dateResult >= DateTime.Parse(rule.Start) && dateResult <= DateTime.Parse(rule.End))
                                   .Select(rule => rule.RowKey)
                                   .First();

        IEnumerable<Meal> filteredMeals = meals.Where(meal => meal.Seasons.Contains(season));
        log.LogInformation($"MealChooserBot | GET | Filtered {filteredMeals.Count()} meals by the season: [{season}]");
        return filteredMeals;
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnDayOfWeekAsync(DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        string day = dateResult.DayOfWeek.ToString();

        log.LogInformation($"MealChooserBot | GET | Querying for rules associated with the day of the week: {day}");
        RuleEntity rule = (await ruleRepo.QueryEntityAsync(x => x.PartitionKey == "days" && x.RowKey == day)).Single();

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
