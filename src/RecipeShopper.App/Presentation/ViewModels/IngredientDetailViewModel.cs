using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.App.Presentation.Views;
using RecipeShopper.Application.Abstractions.Platform;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class IngredientDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IAppDataStore store;
    private readonly INavigationService navigation;
    private readonly IDialogService dialogs;
    private Guid ingredientId;

    [ObservableProperty] private IngredientItem? ingredient;
    public ObservableCollection<PackageOfferItem> Offers { get; } = [];

    public IngredientDetailViewModel(IAppDataStore store, INavigationService navigation, IDialogService dialogs)
    {
        this.store = store;
        this.navigation = navigation;
        this.dialogs = dialogs;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("IngredientId", out var value) && value is Guid id)
        {
            ingredientId = id;
            Load();
        }
    }

    public override Task OnAppearingAsync()
    {
        Load();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task EditAsync() => Ingredient is null
        ? Task.CompletedTask
        : navigation.GoToAsync(nameof(IngredientEditPage), new Dictionary<string, object?> { ["IngredientId"] = Ingredient.Id });

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Ingredient is null || !await dialogs.ConfirmAsync("Arhivirati namirnicu?", "Postojeći recepti i liste će je zadržati.", "Arhiviraj")) return;
        store.DeleteIngredient(Ingredient.Id);
        await navigation.GoBackAsync();
    }

    private void Load()
    {
        Ingredient = store.FindIngredient(ingredientId);
        if (Ingredient is null) return;
        Title = Ingredient.Name;
        Offers.Clear();
        foreach (var offer in Ingredient.Offers.OrderBy(x => x.Store).ThenBy(x => x.Amount)) Offers.Add(offer);
    }
}
