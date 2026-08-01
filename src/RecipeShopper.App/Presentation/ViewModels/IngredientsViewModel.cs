using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.App.Presentation.Views;
using RecipeShopper.Application.Abstractions.Platform;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class IngredientsViewModel : BaseViewModel
{
    private readonly IAppDataStore store;
    private readonly INavigationService navigation;

    [ObservableProperty] private string query = string.Empty;
    [ObservableProperty] private string sortOrder = "Naziv A–Ž";

    public ObservableCollection<IngredientItem> VisibleIngredients { get; } = [];
    public IReadOnlyList<string> SortOptions { get; } = ["Naziv A–Ž", "Naziv Ž–A", "Najviše ponuda"];

    public IngredientsViewModel(IAppDataStore store, INavigationService navigation)
    {
        this.store = store;
        this.navigation = navigation;
        Title = "Namirnice";
        Refresh();
    }

    public override Task OnAppearingAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    partial void OnQueryChanged(string value) => Refresh();
    partial void OnSortOrderChanged(string value) => Refresh();

    [RelayCommand]
    private Task OpenAsync(IngredientItem? ingredient) => ingredient is null
        ? Task.CompletedTask
        : navigation.GoToAsync(nameof(IngredientDetailPage), new Dictionary<string, object?> { ["IngredientId"] = ingredient.Id });

    [RelayCommand]
    private Task NewAsync() => navigation.GoToAsync(nameof(IngredientEditPage));

    private void Refresh()
    {
        IEnumerable<IngredientItem> filtered = store.Ingredients
            .Where(x => !x.IsArchived)
            .Where(x => string.IsNullOrWhiteSpace(Query) || x.Name.Contains(Query.Trim(), StringComparison.CurrentCultureIgnoreCase));
        filtered = SortOrder switch
        {
            "Naziv Ž–A" => filtered.OrderByDescending(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            "Najviše ponuda" => filtered.OrderByDescending(x => x.Offers.Count).ThenBy(x => x.Name),
            _ => filtered.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        VisibleIngredients.Clear();
        foreach (var ingredient in filtered) VisibleIngredients.Add(ingredient);
    }
}
