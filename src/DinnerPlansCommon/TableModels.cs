namespace DinnerPlansCommon;

public record Meal(string Id,
                   string Name,
                   string[] Catagories,
                   string[] Seasons,
                   string Recipe,
                   int Rating,
                   int Priority);

public record Menu(DateTime Date,
                   string MealId,
                   string RemovedMealId);

public record Option(string Seasons,
                     string Catagories);
