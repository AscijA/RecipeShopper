using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.App.Presentation.Views;
using RecipeShopper.Application.Abstractions.Platform;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class RecipeDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IAppDataStore store;
    private readonly INavigationService navigation;
    private readonly IDialogService dialogs;
    private Guid recipeId;

    [ObservableProperty] private RecipeItem? recipe;
    [ObservableProperty] private string multiplierText = "1";
    [ObservableProperty] private string imagePath = string.Empty;

    public ObservableCollection<RecipeIngredientItem> Ingredients { get; } = [];
    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath);

    public RecipeDetailViewModel(IAppDataStore store, INavigationService navigation, IDialogService dialogs)
    {
        this.store = store;
        this.navigation = navigation;
        this.dialogs = dialogs;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("RecipeId", out var value) && value is Guid id)
        {
            recipeId = id;
            Load();
        }
    }

    public override Task OnAppearingAsync()
    {
        Load();
        return Task.CompletedTask;
    }

    partial void OnImagePathChanged(string value) => OnPropertyChanged(nameof(HasImage));

    [RelayCommand]
    private void ToggleAll()
    {
        var shouldSelect = Ingredients.Any(x => !x.IsSelected);
        foreach (var ingredient in Ingredients)
        {
            ingredient.IsSelected = shouldSelect;
        }
    }

    [RelayCommand]
    private async Task AddToShoppingAsync()
    {
        if (Recipe is null)
        {
            return;
        }
        if (!decimal.TryParse(MultiplierText, out var multiplier) || multiplier <= 0)
        {
            await dialogs.AlertAsync("Neispravan množilac", "Unesite broj veći od nule.");
            return;
        }
        var selected = Ingredients.Where(x => x.IsSelected).Select(x => x.IngredientId).ToList();
        if (selected.Count == 0)
        {
            await dialogs.AlertAsync("Nema namirnica", "Odaberite barem jednu namirnicu.");
            return;
        }
        store.AddRecipeToShoppingList(Recipe.Id, multiplier, selected);
        await dialogs.AlertAsync("Dodano u kupovinu", $"Odabrane namirnice dodane su u „{store.ActiveShoppingList.Name}“.");
    }

    [RelayCommand]
    private Task EditAsync() => Recipe is null
        ? Task.CompletedTask
        : navigation.GoToAsync(nameof(RecipeEditPage), new Dictionary<string, object?> { ["RecipeId"] = Recipe.Id });

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Recipe is null || !await dialogs.ConfirmAsync("Obrisati recept?", $"Recept „{Recipe.Name}“ biće uklonjen.", "Obriši"))
        {
            return;
        }
        store.DeleteRecipe(Recipe.Id);
        await navigation.GoBackAsync();
    }

    [RelayCommand]
    private void SetMultiplier(string? value) => MultiplierText = value ?? "1";

    private void Load()
    {
        Recipe = store.FindRecipe(recipeId);
        if (Recipe is null)
        {
            return;
        }
        Title = Recipe.Name;
        ImagePath = string.IsNullOrWhiteSpace(Recipe.PhotoPath)
            ? string.Empty
            : Path.Combine(FileSystem.AppDataDirectory, Recipe.PhotoPath.Replace('/', Path.DirectorySeparatorChar));
        Ingredients.Clear();
        foreach (var line in Recipe.Ingredients)
        {
            line.IsSelected = true;
            Ingredients.Add(line);
        }
    }
}
