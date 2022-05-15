using System;
using System.Linq;
using DinnerPlansCommon;
using Microsoft.AspNetCore.Http;
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
            Priority = meal.Priority
        };

    public static Meal ConvertToMeal(this MealEntity mealEntity) =>
        new Meal(
            mealEntity.Id, 
            mealEntity.Name, 
            mealEntity.Catagories.Split(','), 
            mealEntity.Seasons.Split(','), 
            mealEntity.Recipe, 
            mealEntity.Rating, 
            mealEntity.Priority);

    public static OkObjectResult DefineResultAsPlainTextContent(this OkObjectResult result)
    {
        var collection = new MediaTypeCollection();
        collection.Add("text/plain");

        result.ContentTypes = collection;
        result.StatusCode = StatusCodes.Status201Created;
        return result;
    }
}