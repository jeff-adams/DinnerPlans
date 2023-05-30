using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.Http;
using DinnerPlansCommon;

namespace DinnerPlansAPI;

public class DinnerPlansMenuBot
{
    private const string mealTableName = "meals";
    private const string mealPartitionKey = "meal";
    private const string specialDatesTableName = "specialDates";
    private const string specialDatesPartitionKey = "specialDates";
    private const string rulesTableName = "rules";

    [FunctionName("TimedMenuUpdater")]
    public async Task MenuUpdaterBot(
        [TimerTrigger("%MenuUpdatorInterval%")] TimerInfo timer,
        [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
        [Table(specialDatesTableName, Connection = "DinnerPlansTableConnectionString")] TableClient specialDatesTable,
        [Table(rulesTableName, Connection = "DinnerPlansTableConnectionString")] TableClient rulesTable,
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
        [Table(mealTableName, Connection = "DinnerPlansTableConnectionString")] TableClient mealTable,
        [Table(specialDatesTableName, Connection = "DinnerPlansTableConnectionString")] TableClient specialDatesTable,
        [Table(rulesTableName, Connection = "DinnerPlansTableConnectionString")] TableClient rulesTable,
        ILogger log)
    {
        string dateQuery = req.Query["date"];
        bool isValidDate = DateTime.TryParse(dateQuery, out DateTime dateResult);
        if (!isValidDate)
        {
            log.LogError($"MealChooser recieved invalid DateTime format in query: [{dateQuery}]");
            return new BadRequestObjectResult($"Please provide a DateTime query, 'api/menu?date=<DateTime>'");
        }

        string date = dateResult.ToString("yyyy.MM.dd");
        log.LogInformation($"MealChooser | GET | Choose random meal for {date}");

       string selectedMealId = await RandomMealByDateAsync(dateResult, mealTable, specialDatesTable, rulesTable, log);

       return new OkObjectResult(selectedMealId);
    }

    private async Task<string> RandomMealByDateAsync(
        DateTime date,
        TableClient mealTable,
        TableClient specialDatesTable,
        TableClient rulesTable,
        ILogger log
    )
    {
         string mealId = await QueryForSpecialMealIdAsync(date.ToString("MMdd"), specialDatesTable, log);
        if (!string.IsNullOrEmpty(mealId))
        {
            return mealId;
        }

        IEnumerable<Meal> meals = await GetAllMealsNotOnMenuAsync(mealTable, log);
        meals = await FilterMealsOnDayOfWeekAsync(rulesTable, date, meals, log);
        meals = await FilterMealsOnSeasonAsync(rulesTable, date, meals, log);
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

    private async Task<IEnumerable<Meal>> GetAllMealsNotOnMenuAsync(TableClient mealTable, ILogger log)
    {
        log.LogInformation("Querying for all meals not currently on the menu");
        AsyncPageable<MealEntity> mealResults = mealTable.QueryAsync<MealEntity>(meal => meal.PartitionKey == mealPartitionKey);
        List<MealEntity> mealEntities = await mealResults.Where(meal => meal.NextOnMenu == null).ToListAsync();
        return mealEntities.Select(mealEntity => mealEntity.ConvertToMeal());
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnSeasonAsync(TableClient rulesTable, DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        log.LogInformation("Querying for the rule's definition of seasons");
        RuleEntity[] seasonRules = await rulesTable.QueryAsync<RuleEntity>(x => x.PartitionKey == "seasons").ToArrayAsync();
        string season = seasonRules.Where(rule => dateResult >= DateTime.Parse(rule.Start) && dateResult <= DateTime.Parse(rule.End))
                                   .Select(rule => rule.RowKey)
                                   .First();
        log.LogInformation($"Filter meals by the season: {season}");
        return meals.Where(meal => meal.Seasons.Contains(season));
    }

    private async Task<IEnumerable<Meal>> FilterMealsOnDayOfWeekAsync(TableClient rulesTable, DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        string day = dateResult.DayOfWeek.ToString();
        log.LogInformation($"Querying for rules associated with the day of the week: {day}");
        Response<RuleEntity> ruleResponse = await rulesTable.GetEntityAsync<RuleEntity>("days", day);
        log.LogInformation($"Filtering on meals from these catagories: {ruleResponse.Value.Catagories}");
        string[] catagories = ruleResponse.Value.Catagories.Split(',');
        return meals.Where(meal => meal.Catagories.Any(catagory => catagories.Contains(catagory)));
    }

    private async Task<string> QueryForSpecialMealIdAsync(string date, TableClient specialDateTable, ILogger log)
    {
        string mealId = string.Empty;

        try
        {
            SpecialDateEntity specialDateEntity = await specialDateTable.GetEntityAsync<SpecialDateEntity>(specialDatesPartitionKey, date);
            mealId = specialDateEntity.MealId;
        }
        catch (RequestFailedException)
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
