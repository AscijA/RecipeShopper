using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class MealPlannerViewModel : BaseViewModel
{
    private readonly IAppDataStore store;
    private readonly IDialogService dialogs;

    [ObservableProperty] private bool isWeekA = true;
    [ObservableProperty] private DayMealPlan? selectedDay;
    [ObservableProperty] private MealSlotPlan? selectedSlot;
    [ObservableProperty] private RecipeItem? selectedRecipe;
    [ObservableProperty] private string multiplierText = "1";

    public ObservableCollection<DayMealPlan> Days { get; } = [];
    public ObservableCollection<RecipeItem> Recipes => store.Recipes;
    public string WeekLabel => IsWeekA ? "Sedmica A" : "Sedmica B";

    public MealPlannerViewModel(IAppDataStore store, IDialogService dialogs)
    {
        this.store = store;
        this.dialogs = dialogs;
        Title = "Plan obroka";
        RefreshDays();
    }

    partial void OnIsWeekAChanged(bool value)
    {
        OnPropertyChanged(nameof(WeekLabel));
        RefreshDays();
    }

    partial void OnSelectedDayChanged(DayMealPlan? value) => SelectedSlot = value?.Slots.FirstOrDefault();

    [RelayCommand]
    private void ShowWeekA() => IsWeekA = true;

    [RelayCommand]
    private void ShowWeekB() => IsWeekA = false;

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (SelectedDay is null || SelectedSlot is null || SelectedRecipe is null)
        {
            await dialogs.AlertAsync("Odaberite obrok", "Odaberite dan, termin i recept.");
            return;
        }
        if (!decimal.TryParse(MultiplierText, out var multiplier) || multiplier <= 0)
        {
            await dialogs.AlertAsync("Neispravan množilac", "Unesite broj veći od nule.");
            return;
        }
        store.AddMealPlanEntry(IsWeekA, SelectedDay.DayIndex, SelectedSlot.Id, SelectedRecipe.Id, multiplier);
        RefreshDays();
    }

    [RelayCommand]
    private void RemoveEntry(PlannerEntryReference? reference)
    {
        if (reference is null) return;
        store.RemoveMealPlanEntry(IsWeekA, reference.DayIndex, reference.SlotId, reference.Entry.Id);
        RefreshDays();
    }

    [RelayCommand]
    private async Task AddDayToShoppingAsync(DayMealPlan? day)
    {
        if (day is null) return;
        store.AddPlannerSelectionToShoppingList(IsWeekA, day.DayIndex);
        await dialogs.AlertAsync("Dodano", $"Namirnice za {day.DayName.ToLowerInvariant()} dodane su u kupovinu.");
    }

    [RelayCommand]
    private async Task AddWeekToShoppingAsync()
    {
        store.AddPlannerSelectionToShoppingList(IsWeekA);
        await dialogs.AlertAsync("Dodano", $"Namirnice iz {WeekLabel.ToLowerInvariant()} dodane su u kupovinu.");
    }

    private void RefreshDays()
    {
        var selectedIndex = SelectedDay?.DayIndex;
        Days.Clear();
        foreach (var day in IsWeekA ? store.WeekA : store.WeekB) Days.Add(day);
        SelectedDay = selectedIndex is int index ? Days.FirstOrDefault(x => x.DayIndex == index) : Days.FirstOrDefault();
    }
}

public sealed record PlannerEntryReference(int DayIndex, Guid SlotId, MealPlanEntry Entry);
