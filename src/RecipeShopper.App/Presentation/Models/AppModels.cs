using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RecipeShopper.App.Presentation.Models;

public enum MeasurementFamily
{
    Mass,
    Volume,
    Count,
    Custom
}

public enum ShoppingGrouping
{
    None,
    Recipe,
    Store
}

public enum AppAppearance
{
    System,
    Light,
    Dark
}

public sealed partial class RecipeItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string category = "Ostalo";
    [ObservableProperty] private string icon = "🍽️";
    [ObservableProperty] private string? photoPath;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private bool isSelected;

    public ObservableCollection<RecipeIngredientItem> Ingredients { get; } = [];
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);

    partial void OnPhotoPathChanged(string? value) => OnPropertyChanged(nameof(HasPhoto));
}

public sealed partial class RecipeIngredientItem : ObservableObject
{
    public Guid IngredientId { get; init; }

    [ObservableProperty] private string ingredientName = string.Empty;
    [ObservableProperty] private string icon = "🥣";
    [ObservableProperty] private decimal? amount;
    [ObservableProperty] private string unit = string.Empty;
    [ObservableProperty] private bool isSelected = true;

    public string AmountText => Amount is null ? "po ukusu" : $"{Amount:0.##} {Unit}".Trim();

    partial void OnAmountChanged(decimal? value) => OnPropertyChanged(nameof(AmountText));
    partial void OnUnitChanged(string value) => OnPropertyChanged(nameof(AmountText));
}

public sealed partial class RecipeIngredientDraft : ObservableObject
{
    public Guid IngredientId { get; set; }

    [ObservableProperty] private string ingredientName = string.Empty;
    [ObservableProperty] private string icon = "🥣";
    [ObservableProperty] private string amount = string.Empty;
    [ObservableProperty] private string unit = "g";
}

public sealed partial class IngredientItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string icon = "🥣";
    [ObservableProperty] private MeasurementFamily measurementFamily;
    [ObservableProperty] private string baseUnit = "g";
    [ObservableProperty] private bool isArchived;

    public ObservableCollection<PackageOfferItem> Offers { get; } = [];
    public string MeasurementDescription => $"{FamilyLabel(MeasurementFamily)} · {BaseUnit}";
    public string OfferSummary => Offers.Count == 0 ? "Cijene nisu unesene" : $"{Offers.Count} ponuda";

    partial void OnMeasurementFamilyChanged(MeasurementFamily value) => OnPropertyChanged(nameof(MeasurementDescription));
    partial void OnBaseUnitChanged(string value) => OnPropertyChanged(nameof(MeasurementDescription));

    private static string FamilyLabel(MeasurementFamily family) => family switch
    {
        MeasurementFamily.Mass => "Masa",
        MeasurementFamily.Volume => "Zapremina",
        MeasurementFamily.Count => "Komadi",
        _ => "Posebna jedinica"
    };
}

public sealed partial class PackageOfferItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty] private string packageType = "pakovanje";
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private string unit = "g";
    [ObservableProperty] private string store = string.Empty;
    [ObservableProperty] private decimal? price;
    [ObservableProperty] private decimal? previousPrice;
    [ObservableProperty] private string currency = "BAM";
    [ObservableProperty] private DateTime updatedAt = DateTime.Today;

    public string SizeText => $"{Amount:0.##} {Unit}";
    public string PriceText => Price is null ? "Bez cijene" : $"{Price:0.00} {Currency}";
    public string Description => $"{PackageType}, {SizeText} · {Store}";

    partial void OnAmountChanged(decimal value) => NotifyComputed();
    partial void OnUnitChanged(string value) => NotifyComputed();
    partial void OnPriceChanged(decimal? value) => NotifyComputed();
    partial void OnCurrencyChanged(string value) => NotifyComputed();
    partial void OnPackageTypeChanged(string value) => OnPropertyChanged(nameof(Description));
    partial void OnStoreChanged(string value) => OnPropertyChanged(nameof(Description));

    private void NotifyComputed()
    {
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(Description));
    }
}

public sealed partial class ShoppingContribution : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid IngredientId { get; init; }
    public Guid? RecipeId { get; init; }

    [ObservableProperty] private string sourceName = "Ručno dodano";
    [ObservableProperty] private decimal? amount;
    [ObservableProperty] private string unit = string.Empty;
    [ObservableProperty] private string note = string.Empty;
}

public sealed partial class ShoppingListItem : ObservableObject
{
    public Guid IngredientId { get; init; }

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string icon = "🥣";
    [ObservableProperty] private string quantityText = string.Empty;
    [ObservableProperty] private string sourceSummary = string.Empty;
    [ObservableProperty] private string store = "Bez trgovine";
    [ObservableProperty] private string priceText = "Cijena nije potpuna";
    [ObservableProperty] private string recommendation = string.Empty;
    [ObservableProperty] private bool isChecked;
    [ObservableProperty] private bool hasCompletePrice;

    public ObservableCollection<Guid> ContributionIds { get; } = [];
}

public sealed class ShoppingGroup : ObservableCollection<ShoppingListItem>
{
    public ShoppingGroup(string name, IEnumerable<ShoppingListItem> items) : base(items) => Name = name;
    public string Name { get; }
}

public sealed partial class NamedShoppingList : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty] private string name = "Moja kupovina";
    [ObservableProperty] private bool isArchived;
    [ObservableProperty] private DateTime createdAt = DateTime.Now;
    [ObservableProperty] private DateTime? completedAt;

    public ObservableCollection<ShoppingContribution> Contributions { get; } = [];
    public Dictionary<Guid, bool> CheckStates { get; } = [];
    public Dictionary<Guid, Guid> SelectedOfferIds { get; } = [];
    public string StatusText => IsArchived ? $"Završena {CompletedAt:d}" : $"{Contributions.Count} stavki";

    partial void OnIsArchivedChanged(bool value) => OnPropertyChanged(nameof(StatusText));
    partial void OnCompletedAtChanged(DateTime? value) => OnPropertyChanged(nameof(StatusText));
}

public sealed partial class MealPlanEntry : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RecipeId { get; init; }

    [ObservableProperty] private string recipeName = string.Empty;
    [ObservableProperty] private string recipeIcon = "🍽️";
    [ObservableProperty] private decimal multiplier = 1m;

    public string DisplayText => Multiplier == 1m ? RecipeName : $"{RecipeName} ×{Multiplier:0.##}";
    partial void OnMultiplierChanged(decimal value) => OnPropertyChanged(nameof(DisplayText));
    partial void OnRecipeNameChanged(string value) => OnPropertyChanged(nameof(DisplayText));
}

public sealed partial class MealSlotPlan : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty] private string name = "Ručak";
    public ObservableCollection<MealPlanEntry> Entries { get; } = [];
    public bool HasEntries => Entries.Count > 0;
}

public sealed partial class DayMealPlan : ObservableObject
{
    public int DayIndex { get; init; }

    [ObservableProperty] private string dayName = string.Empty;
    [ObservableProperty] private string dateHint = string.Empty;
    public ObservableCollection<MealSlotPlan> Slots { get; } = [];
}

public sealed partial class StoreItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private bool isEnabled = true;
}

public sealed partial class CategoryItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string icon = "🍽️";
}

public sealed partial class AppSettings : ObservableObject
{
    [ObservableProperty] private AppAppearance appearance = AppAppearance.System;
    [ObservableProperty] private string accent = "Zelena";
    [ObservableProperty] private ShoppingGrouping defaultGrouping = ShoppingGrouping.None;
    [ObservableProperty] private bool moveCheckedToBottom = true;
    [ObservableProperty] private bool hideChecked;
    [ObservableProperty] private bool confirmDestructiveActions = true;
    [ObservableProperty] private DateTime referenceMonday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
    [ObservableProperty] private bool reminderEnabled;
    [ObservableProperty] private TimeSpan reminderTime = new(18, 0, 0);
    [ObservableProperty] private string currency = "BAM";
}
