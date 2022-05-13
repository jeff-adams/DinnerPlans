namespace DinnerPlansCommon;
public record Meal(Guid Id,
                   string Name,
                   string Catagories,
                   string Seasons,
                   string Recipe,
                   int Rating,
                   int Priority);

public record Menu(DateTime Date,
                   Guid MealId,
                   Guid RemovedMealId);

public record Option(string Seasons,
                     string Catagories);
