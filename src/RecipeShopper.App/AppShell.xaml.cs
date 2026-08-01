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
                CreateTab<RecipesPage>(services, "Recepti", "tab_recipes.svg", "recipes"),
                CreateTab<ShoppingPage>(services, "Kupovina", "tab_cart.svg", "shopping"),
                CreateTab<IngredientsPage>(services, "Namirnice", "tab_ingredients.svg", "ingredients"),
                CreateTab<SettingsPage>(services, "Postavke", "tab_settings.svg", "settings")
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
}
