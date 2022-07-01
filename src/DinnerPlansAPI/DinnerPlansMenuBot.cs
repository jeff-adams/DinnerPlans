using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using DinnerPlansCommon;
using Azure;
using System.Collections.Generic;
using System.Linq;

namespace DinnerPlansAPI;

public static class DinnerPlansMenuBot
{
    private const string mealTableName = "meals";
    private const string mealPartitionKey = "meal";
    private const string specialDatesTableName = "specialDates";
    private const string specialDatesPartitionKey = "specialDates";
    private const string rulesTableName = "rules";

    [FunctionName("MealChooser")]
    public static async Task<IActionResult> ChooseMeal(
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

        string mealId = await QueryForSpecialMealIdAsync(dateResult.ToString("MMdd"), specialDatesTable, log);
        if (!string.IsNullOrEmpty(mealId))
        {
            return new OkObjectResult(mealId);
        }

        IEnumerable<Meal> meals = await GetAllMealsNotOnMenuAsync(mealTable, log);
        meals = await FilterMealsOnDayOfWeekAsync(rulesTable, dateResult, meals, log);
        meals = await FilterMealsOnSeasonAsync(rulesTable, dateResult, meals, log);
        Meal[] filteredMeals = meals.ToArray();

        // create weights
        int totalWeight = 0;
        int[] weights = filteredMeals.Select(meal => { int mealWeight = CalculateMealWeight(meal); totalWeight += mealWeight; return mealWeight;}).ToArray();

        // randomly choose weighted meal
        int randomIndex = new Random().Next(totalWeight) + 1;
        Meal selectedMeal = null;
        for (int i = 0; i < weights.Length; i++)
        {
            int weight = weights[i];
            randomIndex -= weight;
            if (randomIndex <= 0)
            {
                selectedMeal = filteredMeals[i];
            }
        }

        return new OkObjectResult(selectedMeal.Id);
    }

    private static async Task<IEnumerable<Meal>> GetAllMealsNotOnMenuAsync(TableClient mealTable, ILogger log)
    {
        log.LogInformation("Querying for all meals not currently on the menu");
        AsyncPageable<MealEntity> mealResults = mealTable.QueryAsync<MealEntity>(meal => meal.NextOnMenu == null);
        List<MealEntity> mealEntities = await mealResults.ToListAsync();
        return mealEntities.Select(mealEntity => mealEntity.ConvertToMeal());
    }

    private static async Task<IEnumerable<Meal>> FilterMealsOnSeasonAsync(TableClient rulesTable, DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        log.LogInformation("Querying for the rule's definition of seasons");
        RuleEntity[] seasonRules = await rulesTable.QueryAsync<RuleEntity>(x => x.PartitionKey == "seasons").ToArrayAsync();
        log.LogInformation($"Filter meals by the season of {dateResult.ToString("yyyyMMdd")}");
        string season = seasonRules.Where(rule => dateResult >= DateTime.Parse(rule.Start) && dateResult <= DateTime.Parse(rule.End))
                                   .Select(rule => rule.RowKey)
                                   .First();
        return meals.Where(meal => meal.Seasons.Contains(season));
    }

    private static async Task<IEnumerable<Meal>> FilterMealsOnDayOfWeekAsync(TableClient rulesTable, DateTime dateResult, IEnumerable<Meal> meals, ILogger log)
    {
        string day = dateResult.DayOfWeek.ToString();
        log.LogInformation($"Querying for rules associated with the day of the week: {day}");
        var ruleResponse = await rulesTable.GetEntityAsync<RuleEntity>("days", day);
        string[] catagories = ruleResponse.Value.Catagories.Split(',');
        return meals.Where(meal => meal.Catagories.Any(catagory => catagories.Contains(catagory)));
    }

    private static async Task<string> QueryForSpecialMealIdAsync(string date, TableClient specialDateTable, ILogger log)
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

    private static int CalculateMealWeight(Meal meal)
    {
        int weight = 0;
        
        DateTime start = meal.LastOnMenu ?? DateTime.Now;
        int dateWeight = (DateTime.Now - start).Days;

        weight += meal.Rating * dateWeight;

        return weight <= 1 ? 1 : weight;
    }
}
