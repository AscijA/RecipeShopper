using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.App.Presentation.Views;
using RecipeShopper.Application.Abstractions.Platform;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class RecipesViewModel : BaseViewModel
{
    private readonly IAppDataStore store;
    private readonly INavigationService navigation;
    private readonly IDialogService dialogs;

    public ObservableCollection<RecipeItem> VisibleRecipes { get; } = [];
    public ObservableCollection<string> CategoryFilters { get; } = ["Sve"];

    [ObservableProperty] private string query = string.Empty;
    [ObservableProperty] private string selectedCategory = "Sve";

    public RecipesViewModel(IAppDataStore store, INavigationService navigation, IDialogService dialogs)
    {
        this.store = store;
        this.navigation = navigation;
        this.dialogs = dialogs;
        Title = "Recepti";
        ReloadCategories();
        Refresh();
    }

    public override Task OnAppearingAsync()
    {
        ReloadCategories();
        Refresh();
        return Task.CompletedTask;
    }

    partial void OnQueryChanged(string value) => Refresh();
    partial void OnSelectedCategoryChanged(string value) => Refresh();

    [RelayCommand]
    private Task OpenRecipeAsync(RecipeItem? recipe) => recipe is null
        ? Task.CompletedTask
        : navigation.GoToAsync(nameof(RecipeDetailPage), new Dictionary<string, object?> { ["RecipeId"] = recipe.Id });

    [RelayCommand]
    private Task NewRecipeAsync() => navigation.GoToAsync(nameof(RecipeEditPage));

    [RelayCommand]
    private Task OpenPlannerAsync() => navigation.GoToAsync(nameof(MealPlannerPage));

    [RelayCommand]
    private async Task AddSelectedAsync()
    {
        var selected = store.Recipes.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await dialogs.AlertAsync("Nema odabranih recepata", "Označite barem jedan recept.");
            return;
        }

        var action = await dialogs.ActionSheetAsync("Šta želite uraditi?", "Odustani", "Dodaj u kupovinu", "Dodaj u plan", "Oboje");
        if (action is "Dodaj u kupovinu" or "Oboje")
        {
            foreach (var recipe in selected)
            {
                store.AddRecipeToShoppingList(recipe.Id, 1m);
                recipe.IsSelected = false;
            }
            await dialogs.AlertAsync("Dodano", $"{selected.Count} recept(a) dodano je u „{store.ActiveShoppingList.Name}“.");
        }
        if (action is "Dodaj u plan" or "Oboje")
        {
            await navigation.GoToAsync(nameof(MealPlannerPage));
        }
    }

    private void ReloadCategories()
    {
        var current = SelectedCategory;
        CategoryFilters.Clear();
        CategoryFilters.Add("Sve");
        foreach (var category in store.Categories.OrderBy(x => x.Name))
        {
            CategoryFilters.Add(category.Name);
        }
        SelectedCategory = CategoryFilters.Contains(current) ? current : "Sve";
    }

    private void Refresh()
    {
        var filtered = store.Recipes
            .Where(x => SelectedCategory == "Sve" || x.Category == SelectedCategory)
            .Where(x => string.IsNullOrWhiteSpace(Query) || x.Name.Contains(Query.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase);
        VisibleRecipes.Clear();
        foreach (var recipe in filtered)
        {
            VisibleRecipes.Add(recipe);
        }
    }
}
