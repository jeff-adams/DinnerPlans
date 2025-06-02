using System;
using System.Linq;
using DinnerPlansCommon;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace DinnerPlansAPI;

public static class DinnerPlanAPIExtensions
{
    public static MealEntity ConvertToMealEntity(this Meal meal, string partionKey) =>
        new MealEntity()
        {
            PartitionKey = partionKey,
            Id = meal.Id ?? Guid.NewGuid().ToString(),
            Name = meal.Name,
            Catagories = meal.Catagories.Aggregate((accum, next) => $"{accum},{next}"),
            Seasons = meal.Seasons.Aggregate((accum, next) => $"{accum},{next}"),
            Recipe = meal.Recipe,
            Rating = meal.Rating,
            LastOnMenu = meal.LastOnMenu,
            NextOnMenu = meal.NextOnMenu
        };

    public static string ConvertToHtml(this Meal meal)
    {
        string recipe = meal.Recipe is null ? string.Empty : $@"<a href=""{meal.Recipe}"">{meal.Recipe}</a>";
        return $@"<!DOCTYPE html><html><head><title>DinnerPlans</title></head><body><h1>{meal.Name}</h1><p>{recipe}</p></body></html>";
    }

    public static Meal ConvertToMeal(this MealEntity mealEntity) =>
        new Meal(
            mealEntity.Id, 
            mealEntity.Name, 
            mealEntity.Catagories.Split(','), 
            mealEntity.Seasons.Split(','), 
            mealEntity.Recipe, 
            mealEntity.Rating, 
            mealEntity.LastOnMenu,
            mealEntity.NextOnMenu);

    public static MenuEntity ConvertToMenuEntity(this Menu menu, string partionKey) =>
        new MenuEntity()
        {
            PartitionKey = partionKey,
            Date = menu.Date,
            MealId = menu.Meal?.Id,
            RemovedMealId = menu.RemovedMeal?.Id
        };

    public static Rule ConvertToRule(this RuleEntity ruleEntity)
    {
        DateTime start = DateTime.Parse(ruleEntity.Start);
        DateTime end = DateTime.Parse(ruleEntity.End);
        if (end < start)
        {
            _ = DateTime.UtcNow <= end
                ? start.AddYears(-1)
                : end.AddYears(1);
        }

        return new (ruleEntity.RowKey, start, end); 
    }

    public static OkObjectResult DefineResultAsPlainTextContent(this OkObjectResult result, int statusCode)
    {
        var collection = new MediaTypeCollection
        {
            "text/plain"
        };

        result.ContentTypes = collection;
        result.StatusCode = statusCode;
        return result;
    }

    public static DateTime ToEasternStandardTime(this DateTime date) =>
        date.Kind == DateTimeKind.Utc 
            ? TimeZoneInfo.ConvertTimeFromUtc(date, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")) 
            : date;
}