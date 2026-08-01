using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.App.Presentation.Views;
using RecipeShopper.Application.Abstractions.Platform;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class ShoppingViewModel : BaseViewModel
{
    private readonly IAppDataStore store;
    private readonly INavigationService navigation;
    private readonly IShareService share;
    private readonly IDialogService dialogs;

    [ObservableProperty] private NamedShoppingList? selectedList;
    [ObservableProperty] private string selectedGrouping = "Bez grupiranja";
    [ObservableProperty] private string totalText = string.Empty;
    [ObservableProperty] private Color totalColor = Colors.Orange;
    [ObservableProperty] private int uncheckedCount;

    public ObservableCollection<NamedShoppingList> ActiveLists { get; } = [];
    public ObservableCollection<ShoppingGroup> Groups { get; } = [];
    public IReadOnlyList<string> GroupingOptions { get; } = ["Bez grupiranja", "Po receptu", "Po trgovini"];
    public bool HasItems => Groups.Sum(x => x.Count) > 0;
    public bool IsReadOnly => SelectedList?.IsArchived == true;

    public ShoppingViewModel(IAppDataStore store, INavigationService navigation, IShareService share, IDialogService dialogs)
    {
        this.store = store;
        this.navigation = navigation;
        this.share = share;
        this.dialogs = dialogs;
        Title = "Kupovina";
        SelectedGrouping = GroupingLabel(store.Settings.DefaultGrouping);
        Refresh();
    }

    public override Task OnAppearingAsync()
    {
        RefreshLists();
        SelectedList = store.ActiveShoppingList;
        Refresh();
        return Task.CompletedTask;
    }

    partial void OnSelectedListChanged(NamedShoppingList? value)
    {
        if (value is not null)
        {
            store.ActiveShoppingListId = value.Id;
            Refresh();
        }
    }

    partial void OnSelectedGroupingChanged(string value) => Refresh();

    public void SetChecked(ShoppingListItem item, bool isChecked)
    {
        store.SetShoppingItemChecked(item.IngredientId, isChecked);
        Refresh();
    }

    [RelayCommand]
    private Task ManageListsAsync() => navigation.GoToAsync(nameof(ShoppingListsPage));

    [RelayCommand]
    private async Task AddManualAsync()
    {
        var ingredients = store.Ingredients.Where(x => !x.IsArchived).OrderBy(x => x.Name).ToList();
        var choice = await dialogs.ActionSheetAsync("Odaberite namirnicu", "Odustani", ingredients.Select(x => $"{x.Icon} {x.Name}").ToArray());
        var ingredient = ingredients.FirstOrDefault(x => $"{x.Icon} {x.Name}" == choice);
        if (ingredient is null) return;
        var amountText = await dialogs.PromptAsync("Količina", $"Koliko treba za {ingredient.Name.ToLowerInvariant()}?", initialValue: "1", keyboard: Keyboard.Numeric);
        if (amountText is null) return;
        if (!decimal.TryParse(amountText, out var amount) || amount <= 0)
        {
            await dialogs.AlertAsync("Neispravna količina", "Unesite broj veći od nule.");
            return;
        }
        store.AddManualShoppingItem(ingredient.Id, amount, ingredient.BaseUnit, string.Empty);
        Refresh();
    }

    [RelayCommand]
    private async Task SelectPackageAsync(ShoppingListItem? item)
    {
        if (item is null) return;
        var ingredient = store.FindIngredient(item.IngredientId);
        if (ingredient is null || ingredient.Offers.Count == 0)
        {
            await dialogs.AlertAsync("Nema ponuda", "Dodajte pakovanja i cijene na stranici namirnice.");
            return;
        }
        var labels = ingredient.Offers.Select(x => $"{x.Store} · {x.SizeText} · {x.PriceText}").ToArray();
        var choice = await dialogs.ActionSheetAsync("Odaberite pakovanje", "Odustani", labels);
        var index = Array.IndexOf(labels, choice);
        if (index < 0) return;
        store.SelectOffer(item.IngredientId, ingredient.Offers[index].Id);
        Refresh();
    }

    [RelayCommand]
    private async Task RemoveAsync(ShoppingListItem? item)
    {
        if (item is null) return;
        if (store.Settings.ConfirmDestructiveActions &&
            !await dialogs.ConfirmAsync("Ukloniti namirnicu?", $"Sve stavke za „{item.Name}“ biće uklonjene iz ove liste.", "Ukloni"))
        {
            return;
        }
        store.RemoveShoppingItem(item.IngredientId);
        Refresh();
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (!HasItems) return;
        if (!await dialogs.ConfirmAsync("Očistiti listu?", "Sve stavke aktivne kupovine biće uklonjene.", "Očisti")) return;
        store.ClearActiveShoppingList();
        Refresh();
    }

    [RelayCommand]
    private Task ShareAsync() => share.ShareTextAsync(store.ActiveShoppingList.Name, store.BuildShareText(CurrentGrouping()));

    private void RefreshLists()
    {
        ActiveLists.Clear();
        foreach (var list in store.ShoppingLists.Where(x => !x.IsArchived).OrderByDescending(x => x.CreatedAt)) ActiveLists.Add(list);
    }

    private void Refresh()
    {
        if (SelectedList is not null && store.ShoppingLists.Any(x => x.Id == SelectedList.Id && !x.IsArchived))
        {
            store.ActiveShoppingListId = SelectedList.Id;
        }
        Groups.Clear();
        foreach (var group in store.BuildShoppingGroups(CurrentGrouping(), store.Settings.HideChecked)) Groups.Add(group);
        var totals = store.CalculateShoppingTotals();
        TotalText = totals.DisplayText;
        TotalColor = totals.IsComplete ? Color.FromArgb("#2F7D4A") : Color.FromArgb("#D9772D");
        UncheckedCount = Groups.SelectMany(x => x).Count(x => !x.IsChecked);
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsReadOnly));
    }

    private ShoppingGrouping CurrentGrouping() => SelectedGrouping switch
    {
        "Po receptu" => ShoppingGrouping.Recipe,
        "Po trgovini" => ShoppingGrouping.Store,
        _ => ShoppingGrouping.None
    };

    private static string GroupingLabel(ShoppingGrouping grouping) => grouping switch
    {
        ShoppingGrouping.Recipe => "Po receptu",
        ShoppingGrouping.Store => "Po trgovini",
        _ => "Bez grupiranja"
    };
}
