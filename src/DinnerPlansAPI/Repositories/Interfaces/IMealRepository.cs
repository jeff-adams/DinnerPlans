using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DinnerPlansAPI.Repositories;

public interface IMealRepository
{
    Task<MealEntity> GetMealEntityAsync(string mealKey);
    Task AddMealEntityAsync(MealEntity meal);
    Task UpdateMealEntityAsync(MealEntity meal);
    Task<IReadOnlyCollection<MealEntity>> QueryMealEntityAsync(Expression<Func<MealEntity, bool>> filter);
}