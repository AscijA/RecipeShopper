using RecipeShopper.Domain.Entities;

namespace RecipeShopper.Domain.Services;

public static class DefaultCatalog
{
    private static readonly (string Name, string Icon)[] Categories =
    [
        ("Govedina", "category-beef"),
        ("Piletina", "category-chicken"),
        ("Riba", "category-fish"),
        ("Supa/Čorba", "category-soup"),
        ("Tijesto", "category-dough"),
        ("Slatko", "category-sweet"),
        ("Vegetarijansko", "category-vegetarian"),
        ("Salata", "category-salad"),
        ("Doručak", "category-breakfast"),
        ("Ručak", "category-lunch"),
        ("Večera", "category-dinner"),
        ("Ostalo", "category-other")
    ];

    private static readonly string[] MealSlots = ["Doručak", "Ručak", "Večera", "Ostalo"];

    public static IReadOnlyList<RecipeCategory> CreateCategories() => Categories
        .Select((value, index) => new RecipeCategory
        {
            Name = value.Name,
            IconKey = value.Icon,
            SortOrder = index
        })
        .ToList();

    public static IReadOnlyList<MealSlot> CreateMealSlots() => MealSlots
        .Select((name, index) => new MealSlot { Name = name, SortOrder = index })
        .ToList();
}
