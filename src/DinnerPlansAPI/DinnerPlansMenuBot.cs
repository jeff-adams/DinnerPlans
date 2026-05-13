using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using DinnerPlansCommon;
using DinnerPlansAPI.Repositories;

namespace DinnerPlansAPI;

public class DinnerPlansMenuBot
{
    private readonly IConfiguration config;
    private readonly ITableRepository<MealEntity> mealRepo;
    private readonly ITableRepository<MenuEntity> menuRepo;
    private readonly ITableRepository<SpecialDateEntity> specialDateRepo;
    private readonly ITableRepository<RuleEntity> ruleRepo;
    private readonly ILogger<DinnerPlansMenuBot> log;

    public DinnerPlansMenuBot(
        IConfiguration config,
        ITableRepository<MealEntity> mealRespository,
        ITableRepository<MenuEntity> menuRepository,
        ITableRepository<SpecialDateEntity> specialDatesRepository,
        ITableRepository<RuleEntity> ruleRepository,
        ILogger<DinnerPlansMenuBot> logger
    )
    {
        this.config = config;
        mealRepo = mealRespository;
        menuRepo = menuRepository;
        specialDateRepo = specialDatesRepository;
        ruleRepo = ruleRepository;
        log = logger;
    }

    [Function("DailyMealUpdator")]
    public async Task MealUpdatorBot(
        [TimerTrigger("%MealDailyUpdatorInterval%")] TimerInfo timer
    )
    {
        log.LogInformation("{FunctionName} | {Type} | Updating today's meals 'LastOnMenu' date", "DailyMealUpdator", "Timer");
        MenuEntity todaysMenu;
        MealEntity todaysMeal;
        string today = DateTime.UtcNow.ToEasternStandardTime().ToString("yyyy.MM.dd");

        try
        {
            todaysMenu = await menuRepo.GetEntityAsync(today);
        }
        catch (TableRepositoryException)
        {
            log.LogInformation("{FunctionName} | {Type} | There is no menu for {Date}", "DailyMealUpdator", "Timer", today);
            return;
        }

        try
        {
            todaysMeal = await mealRepo.GetEntityAsync(todaysMenu.MealId);
        }
        catch (TableRepositoryException ex)
        {
            log.LogError(ex, "{FunctionName} | {Type} | Unable to find the meal {MealId}", "DailyMealUpdator", "Timer", todaysMenu.MealId);
            return;
        }

        // Update the meal with new dates
        if (!todaysMeal.Catagories.Contains("Special")) todaysMeal.NextOnMenu = null;
        todaysMeal.LastOnMenu = DateTime.UtcNow.ToEasternStandardTime().Date.ToUniversalTime();
        
        try
        {
            await mealRepo.UpdateEntityAsync(todaysMeal);
        }
        catch (TableRepositoryException ex)
        {
            log.LogError(ex, "{FunctionName} | {Type} | Unable to update the meal {MealId}", "DailyMealUpdator", "Timer", todaysMenu.MealId);
        }
    }

    [Function("TimedMenuUpdator")]
    public async Task MenuUpdatorBot(
        [TimerTrigger("%MenuUpdatorInterval%")] TimerInfo timer
    )
    {
        int numOfNewMenus = 0;
        int numOfDaysToPlan = int.TryParse(config["NumberOfDaysToMenuPlan"], out int result) ? result : 30;

        // Check menu for meals in the next 30 days and select a random meal for each
        for (int i = 0; i < numOfDaysToPlan; i++)
        {
            DateTime date = DateTime.UtcNow.ToEasternStandardTime().AddDays(i);
            string dateString = date.ToString("yyyy.MM.dd");

            MenuEntity menuEntity = null;
            try
            {
                menuEntity = await menuRepo.GetEntityAsync(dateString);
            }
            catch (TableRepositoryException)
            {
                log.LogInformation("{FunctionName} | {Type} | No menu found for {Date}", "TimedMenuUpdator", "Timer", dateString);
            }

            if (menuEntity is not null && !string.IsNullOrEmpty(menuEntity.MealId))
            {
                log.LogInformation("{FunctionName} | {Type} | Menu found for {Date} already has a scheduled meal: [{MealId}]", "TimedMenuUpdator", "Timer", dateString, menuEntity.MealId);
                continue;
            }
            
            string selectedMealId = string.Empty;
            do
            {
                selectedMealId = await RandomMealByDateAsync(date.Date);
                if (menuEntity is not null) selectedMealId = selectedMealId != menuEntity.RemovedMealId ? selectedMealId : null;
            } while (selectedMealId is null);

            if (string.IsNullOrEmpty(selectedMealId))
            {
                log.LogError("{FunctionName} | {Type} | No meal could be selected for {Date}", "TimedMenuUpdator", "Timer", dateString);
                continue;
            } 

            menuEntity = new ()
            {
                PartitionKey = menuRepo.DefaultPartitionKey,
                Date = date.ToUniversalTime(),
                MealId = selectedMealId
            };

            try
            {
                await menuRepo.UpsertEntityAsync(menuEntity);
            }
            catch (TableRepositoryException)
            {
                log.LogError("{FunctionName} | {Type} | Unable to upsert the menu for {Date}", "TimedMenuUpdator", "Timer", dateString);
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
                log.LogError("{FunctionName} | {Type} | Unable to get the meal [{MealId}] to update the 'NextOnMenu' date to [{Date}]", "TimedMenuUpdator", "Timer", selectedMealId, dateString);
                continue;
            }

            try
            {
                await mealRepo.UpdateEntityAsync(mealEntity);
            }
            catch (TableRepositoryException)
            {
                log.LogError("{FunctionName} | {Type} | Unable to update the meal [{MealId}] 'NextOnMenu' date to [{Date}]", "TimedMenuUpdator", "Timer", mealEntity.Id, dateString);
                continue;
            }

            log.LogInformation("{FunctionName} | {Type} | Menu for {Date} has been chosen: [{MealName}] - [{MealId}]", "TimedMenuUpdator", "Timer", dateString, mealEntity.Name, mealEntity.Id);
            numOfNewMenus++;
        }

        log.LogInformation("{FunctionName} | {Type} | Menus for [{NumOfNewMenus}] days have been assigned", "TimedMenuUpdator", "Timer", numOfNewMenus);   
    }

    [Function("MealChooserBot")]
    public async Task<IActionResult> ChooseMeal(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "bot/choose_meal")] HttpRequest req)
    {
        string dateQuery = req.Query["date"];
        bool isValidDate = DateTime.TryParse(dateQuery, out DateTime dateResult);
        if (!isValidDate)
        {
            log.LogError("{FunctionName} | {Type} | Recieved invalid DateTime format in query: [{DateQuery}]", "ChooseMeal", "GET", dateQuery);
            return new BadRequestObjectResult($"Please provide a DateTime query, 'api/choose_meal?date=<DateTime>'");
        }

        string date = dateResult.ToString("yyyy.MM.dd");
        log.LogInformation("{FunctionName} | {Type} | Choose random meal for {Date}", "ChooseMeal", "GET", date);

        string selectedMealId = await RandomMealByDateAsync(dateResult);
        if (string.IsNullOrEmpty(selectedMealId))
        {
            log.LogWarning("{FunctionName} | {Type} | No meal could be selected for {Date}", "ChooseMeal", "GET", date);
            return new BadRequestObjectResult($"No meal could be selected for {date}");
        }

       Meal meal = null;
       try
       {
            MealEntity mealEntity = await mealRepo.GetEntityAsync(selectedMealId);
            meal = mealEntity.ConvertToMeal();
       }
       catch (TableRepositoryException)
       {
            log.LogError("{FunctionName} | {Type} | Unable to retrieve the selected meal [{MealId}]", "ChooseMeal", "GET", selectedMealId);
            return new BadRequestObjectResult($"Unable to retrieve the selected meal [{selectedMealId}]");
       }

       return new OkObjectResult(meal);
    }

    private async Task<string> RandomMealByDateAsync(DateTime date)
    {
        string mealId = await QueryForSpecialMealIdAsync(date.ToString("MMdd"));
        if (!string.IsNullOrEmpty(mealId))
        {
            log.LogInformation("{FunctionName} | {Type} | Found special meal for {Date}: [{MealId}]", "RandomMealByDate", "Internal", date.ToString("yyyy.MM.dd"), mealId);
            return mealId;
        }

        IEnumerable<Meal> meals = await GetAllMealsNotOnMenuAsync();
        if (!meals.Any())
        {
            log.LogWarning("{FunctionName} | {Type} | No meals available to choose from for {Date}", "RandomMealByDate", "Internal", date.ToString("yyyy.MM.dd"));
            return string.Empty;
        }
        meals = await FilterMealsOnDayOfWeekAsync(date, meals);
        if (!meals.Any())
        {
            log.LogWarning("{FunctionName} | {Type} | No meals available to choose from after filtering by day of the week for {Date}", "RandomMealByDate", "Internal", date.ToString("yyyy.MM.dd"));
            return string.Empty;
        }
        meals = await FilterMealsOnSeasonAsync(date, meals);
        if (!meals.Any())
        {
            log.LogWarning("{FunctionName} | {Type} | No meals available to choose from after filtering by season for {Date}", "RandomMealByDate", "Internal", date.ToString("yyyy.MM.dd"));
            return string.Empty;
        }
        log.LogInformation("{FunctionName} | {Type} | Filtered list of meals down to {MealCount} meals", "RandomMealByDate", "Internal", meals.Count());

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
        log.LogInformation("{FunctionName} | {Type} | Number of meals: {MealCount} | Total weight: {TotalWeight} | Randomly selected index: {RandomIndex}", "RandomMealByDate", "Internal", weights.Length, totalWeight, randomIndex);
        string selectedMealId = string.Empty;
        for (int i = 0; i < weights.Length; i++)
        {
            randomIndex -= weights[i];
            if (randomIndex <= 0)
            {
                Meal selectedMeal = meals.ElementAt(i); // get element at?
                selectedMealId = selectedMeal.Id;
                log.LogInformation("{FunctionName} | {Type} | Randomly selected Meal: {MealName} - {MealId}", "RandomMealByDate", "Internal", selectedMeal.Name, selectedMealId);
                break;
            }
        }

        return selectedMealId;
    }

    private async Task<IEnumerable<Meal>> GetAllMealsNotOnMenuAsync()
    {
        log.LogInformation("{FunctionName} | {Type} | Querying for all meals not currently on the menu", "GetAllMealsNotOnMenu", "Internal");
        IReadOnlyCollection<MealEntity> mealEntities = await mealRepo.QueryEntityAsync(meal => meal.PartitionKey == mealRepo.DefaultPartitionKey);

        IEnumerable<Meal> meals = mealEntities
            // Need to filter locally, as the repo query can't handle the null comparison
            .Where(mealEntity => mealEntity.NextOnMenu is null || mealEntity.NextOnMenu < DateTime.UtcNow.ToEasternStandardTime().Date)
            .Select(mealEntity => mealEntity.ConvertToMeal());
        log.LogInformation("{FunctionName} | {Type} | Found {MealCount} meals not currently on the menu", "GetAllMealsNotOnMenu", "Internal", meals.Count());
        return meals;
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnSeasonAsync(DateTime dateResult, IEnumerable<Meal> meals)
    {
        log.LogInformation("{FunctionName} | {Type} | Querying for the rule's definition of seasons", "FilterMealsOnSeason", "Internal");
        IReadOnlyCollection<RuleEntity> seasonRules = await ruleRepo.QueryEntityAsync(x => x.PartitionKey == "seasons");
        string[] seasons = seasonRules
            .Select(ruleEntity =>
                {
                    Rule rule = ruleEntity.ConvertToRule();
                    if (rule.StartDate == DateTime.MinValue || rule.EndDate == DateTime.MinValue)
                    {
                        log.LogError("{FunctionName} | {Type} | Invalid season rule with key [{RuleKey}] has invalid start or end date. Start: [{Start}] End: [{End}]", "FilterMealsOnSeason", "Internal", rule.Key, rule.StartDate, rule.EndDate);
                    }
                    log.LogInformation("{FunctionName} | {Type} | Season rule found for season [{Season}] with start date [{Start}] and end date [{End}]", "FilterMealsOnSeason", "Internal", rule.Key, rule.StartDate, rule.EndDate);
                    return rule;
                })
            .Where(rule => dateResult >= rule.StartDate && dateResult <= rule.EndDate)
            .Select(rule => rule.Key)
            .ToArray();

        string season = seasons.FirstOrDefault();
        if(seasons.Length > 1)
        {
            log.LogWarning("{FunctionName} | {Type} | Multiple season rules found for the date {Date}. Seasons: {Seasons}", "FilterMealsOnSeason", "Internal", dateResult, string.Join(", ", seasons));
        }
        log.LogInformation("{FunctionName} | {Type} | The season for the date {Date} is [{Season}]", "FilterMealsOnSeason", "Internal", dateResult, season);

        IEnumerable<Meal> filteredMeals = meals.Where(meal => meal.Seasons.Contains(season));
        log.LogInformation("{FunctionName} | {Type} | Filtered {MealCount} meals by the season: [{Season}]", "FilterMealsOnSeason", "Internal", filteredMeals.Count(), season);
        return filteredMeals;
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnDayOfWeekAsync(DateTime dateResult, IEnumerable<Meal> meals)
    {
        string day = dateResult.DayOfWeek.ToString();

        log.LogInformation("{FunctionName} | {Type} | Querying for rules associated with the day of the week: {DayOfWeek}", "FilterMealsOnDayOfWeek", "Internal", day);
        RuleEntity rule = (await ruleRepo.QueryEntityAsync(x => x.PartitionKey == "days" && x.RowKey == day)).Single();

        log.LogInformation("{FunctionName} | {Type} | Filtering on meals from these catagories: {Catagories}", "FilterMealsOnDayOfWeek", "Internal", rule.Catagories);
        string[] catagories = rule.Catagories.Split(',');

        IEnumerable<Meal> filteredMeals = meals.Where(meal => meal.Catagories.Any(catagory => catagories.Contains(catagory)));
        log.LogInformation("{FunctionName} | {Type} | Filtered {MealCount} meals by the day of the week: {DayOfWeek}", "FilterMealsOnDayOfWeek", "Internal", filteredMeals.Count(), day);

        return filteredMeals;
    }

    private async Task<string> QueryForSpecialMealIdAsync(string date)
    {
        log.LogInformation("{FunctionName} | {Type} | Quering for special meals planned for {Date}", "QueryForSpecialMealId", "Internal", date);

        string mealId = string.Empty;
        try
        {
            SpecialDateEntity specialDateEntity = await specialDateRepo.GetEntityAsync(date);
            mealId = specialDateEntity.MealId;
        }
        catch (TableRepositoryException)
        {
            log.LogInformation("{FunctionName} | {Type} | No special meals planned for {Date}", "QueryForSpecialMealId", "Internal", date);
        }

        return mealId;
    }

    private int CalculateMealWeight(Meal meal)
    {
        int weight = 0;
        
        DateTime start = meal.LastOnMenu ?? DateTime.UtcNow.AddDays(-1);
        int dateWeight = (DateTime.UtcNow - start).Days;

        weight += meal.Rating * dateWeight;

        return weight <= 1 ? 1 : weight;
    }
}
