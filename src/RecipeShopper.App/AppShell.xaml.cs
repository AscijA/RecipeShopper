using Microsoft.Extensions.DependencyInjection;
using RecipeShopper.App.Presentation.Views;

namespace RecipeShopper.App;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        Items.Add(new TabBar
        {
            Items =
            {
                CreateTab<RecipesPage>(services, "Recepti", TabIcon("tab_recipes"), "recipes"),
                CreateTab<ShoppingPage>(services, "Kupovina", TabIcon("tab_cart"), "shopping"),
                CreateTab<IngredientsPage>(services, "Namirnice", TabIcon("tab_ingredients"), "ingredients"),
                CreateTab<SettingsPage>(services, "Postavke", TabIcon("tab_settings"), "settings")
            }
        });

        Routing.RegisterRoute(nameof(RecipeDetailPage), typeof(RecipeDetailPage));
        Routing.RegisterRoute(nameof(RecipeEditPage), typeof(RecipeEditPage));
        Routing.RegisterRoute(nameof(MealPlannerPage), typeof(MealPlannerPage));
        Routing.RegisterRoute(nameof(ShoppingListsPage), typeof(ShoppingListsPage));
        Routing.RegisterRoute(nameof(IngredientDetailPage), typeof(IngredientDetailPage));
        Routing.RegisterRoute(nameof(IngredientEditPage), typeof(IngredientEditPage));
    }

    private static ShellContent CreateTab<TPage>(IServiceProvider services, string title, string icon, string route)
        where TPage : Page => new()
        {
            Title = title,
            Icon = icon,
            Route = route,
            ContentTemplate = new DataTemplate(() => services.GetRequiredService<TPage>())
        };

    private static string TabIcon(string name)
    {
#if IOS
        return $"{name}_ios.png";
#else
        return $"{name}.svg";
#endif
    }
}
