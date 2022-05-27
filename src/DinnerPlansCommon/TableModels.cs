namespace DinnerPlansCommon;

public record Meal(string Id,
                   string Name,
                   string[] Catagories,
                   string[] Seasons,
                   string Recipe,
                   int Rating,
                   DateTime LastOnMenu,
                   DateTime NextOnMenu);

public record Menu(DateTime Date,
                   Meal Meal,
                   Meal RemovedMeal);

public record MenuRange(List<Menu> Menus);
