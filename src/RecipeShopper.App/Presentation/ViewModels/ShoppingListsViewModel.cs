using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.Application.Abstractions.Platform;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class ShoppingListsViewModel : BaseViewModel
{
    private readonly IAppDataStore store;
    private readonly INavigationService navigation;
    private readonly IDialogService dialogs;

    public ObservableCollection<NamedShoppingList> ActiveLists { get; } = [];
    public ObservableCollection<NamedShoppingList> ArchivedLists { get; } = [];
    public bool HasArchivedLists => ArchivedLists.Count > 0;

    public ShoppingListsViewModel(IAppDataStore store, INavigationService navigation, IDialogService dialogs)
    {
        this.store = store;
        this.navigation = navigation;
        this.dialogs = dialogs;
        Title = "Moje liste";
        Refresh();
    }

    public override Task OnAppearingAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var name = await dialogs.PromptAsync("Nova lista", "Unesite naziv liste.", initialValue: "Nova kupovina");
        if (string.IsNullOrWhiteSpace(name)) return;
        store.CreateShoppingList(name);
        Refresh();
    }

    [RelayCommand]
    private async Task OpenAsync(NamedShoppingList? list)
    {
        if (list is null) return;
        if (list.IsArchived)
        {
            await dialogs.AlertAsync(list.Name, "Završena lista je zaključana. Možete je duplicirati u novu aktivnu listu.");
            return;
        }
        store.ActiveShoppingListId = list.Id;
        await navigation.GoBackAsync();
    }

    [RelayCommand]
    private async Task RenameAsync(NamedShoppingList? list)
    {
        if (list is null || list.IsArchived) return;
        var name = await dialogs.PromptAsync("Preimenuj listu", "Unesite novi naziv.", initialValue: list.Name);
        if (!string.IsNullOrWhiteSpace(name)) list.Name = name.Trim();
    }

    [RelayCommand]
    private void Duplicate(NamedShoppingList? list)
    {
        if (list is null) return;
        store.DuplicateShoppingList(list.Id);
        Refresh();
    }

    [RelayCommand]
    private async Task CompleteAsync(NamedShoppingList? list)
    {
        if (list is null || list.IsArchived) return;
        if (!await dialogs.ConfirmAsync("Završiti kupovinu?", "Količine, cijene i odabrana pakovanja biće zaključani.", "Završi")) return;
        store.CompleteShoppingList(list.Id);
        Refresh();
    }

    [RelayCommand]
    private async Task DeleteAsync(NamedShoppingList? list)
    {
        if (list is null) return;
        if (!await dialogs.ConfirmAsync("Obrisati listu?", $"Lista „{list.Name}“ biće trajno uklonjena.", "Obriši")) return;
        store.DeleteShoppingList(list.Id);
        Refresh();
    }

    private void Refresh()
    {
        ActiveLists.Clear();
        ArchivedLists.Clear();
        foreach (var list in store.ShoppingLists.OrderByDescending(x => x.CreatedAt))
        {
            if (list.IsArchived) ArchivedLists.Add(list); else ActiveLists.Add(list);
        }
        OnPropertyChanged(nameof(HasArchivedLists));
    }
}
