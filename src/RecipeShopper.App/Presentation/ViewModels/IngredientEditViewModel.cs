using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.Application.Abstractions.Platform;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class IngredientEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IAppDataStore store;
    private readonly INavigationService navigation;
    private readonly IDialogService dialogs;
    private readonly IImageService images;
    private Guid? ingredientId;
    private string? relativeImagePath;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string icon = "🥣";
    [ObservableProperty] private string selectedFamily = "Masa";
    [ObservableProperty] private string baseUnit = "g";
    [ObservableProperty] private string imagePreviewPath = string.Empty;

    public IReadOnlyList<string> Families { get; } = ["Masa", "Zapremina", "Komadi", "Posebna jedinica"];
    public IReadOnlyList<string> Units { get; } = ["g", "kg", "ml", "l", "kom", "Šaka", "pakovanje", "kesica", "kutija", "boca", "tegla", "konzerva"];
    public ObservableCollection<PackageOfferItem> Offers { get; } = [];
    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePreviewPath);

    public IngredientEditViewModel(IAppDataStore store, INavigationService navigation, IDialogService dialogs, IImageService images)
    {
        this.store = store;
        this.navigation = navigation;
        this.dialogs = dialogs;
        this.images = images;
        Title = "Nova namirnica";
    }

    partial void OnImagePreviewPathChanged(string value) => OnPropertyChanged(nameof(HasImage));

    [RelayCommand]
    private async Task PickImageAsync()
    {
        var path = await images.PickAndStoreAsync();
        if (string.IsNullOrWhiteSpace(path)) return;
        relativeImagePath = path;
        ImagePreviewPath = Path.Combine(FileSystem.AppDataDirectory, path.Replace('/', Path.DirectorySeparatorChar));
    }

    [RelayCommand]
    private void RemoveImage()
    {
        relativeImagePath = null;
        ImagePreviewPath = string.Empty;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ingredientId = query.TryGetValue("IngredientId", out var value) && value is Guid id ? id : null;
        Load();
    }

    partial void OnSelectedFamilyChanged(string value)
    {
        BaseUnit = value switch { "Masa" => "g", "Zapremina" => "ml", "Komadi" => "kom", _ => "pakovanje" };
    }

    [RelayCommand]
    private async Task AddOfferAsync()
    {
        var storeName = await dialogs.ActionSheetAsync("Trgovina", "Odustani", store.Stores.Where(x => x.IsEnabled).Select(x => x.Name).ToArray());
        if (storeName is null) return;
        var amountText = await dialogs.PromptAsync("Veličina pakovanja", $"Unesite količinu u {BaseUnit}.", initialValue: "1", keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(amountText, out var amount) || amount <= 0) return;
        var priceText = await dialogs.PromptAsync("Cijena", $"Unesite cijenu u {store.Settings.Currency}.", keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(priceText, out var price) || price < 0) return;
        var type = await dialogs.PromptAsync("Vrsta pakovanja", "Npr. vreća, boca, kutija", initialValue: "pakovanje") ?? "pakovanje";
        Offers.Add(new PackageOfferItem
        {
            Store = storeName,
            Amount = amount,
            Unit = BaseUnit,
            Price = price,
            Currency = store.Settings.Currency,
            PackageType = type
        });
    }

    [RelayCommand]
    private void RemoveOffer(PackageOfferItem? offer)
    {
        if (offer is not null) Offers.Remove(offer);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await dialogs.AlertAsync("Naziv je obavezan", "Unesite naziv namirnice.");
            return;
        }
        var duplicate = store.Ingredients.FirstOrDefault(x => !x.IsArchived && x.Id != ingredientId && x.Name.Equals(Name.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (duplicate is not null)
        {
            await dialogs.AlertAsync("Namirnica već postoji", "Koristite postojeću namirnicu ili unesite drugi naziv.");
            return;
        }
        var ingredient = ingredientId is Guid id ? store.FindIngredient(id) : null;
        ingredient ??= new IngredientItem();
        ingredient.Name = Name.Trim();
        ingredient.Icon = string.IsNullOrWhiteSpace(Icon) ? "🥣" : Icon.Trim();
        ingredient.ImagePath = relativeImagePath;
        ingredient.MeasurementFamily = SelectedFamily switch
        {
            "Masa" => MeasurementFamily.Mass,
            "Zapremina" => MeasurementFamily.Volume,
            "Komadi" => MeasurementFamily.Count,
            _ => MeasurementFamily.Custom
        };
        ingredient.BaseUnit = BaseUnit;
        ingredient.Offers.Clear();
        foreach (var offer in Offers) ingredient.Offers.Add(offer);
        store.SaveIngredient(ingredient);
        await navigation.GoBackAsync();
    }

    private void Load()
    {
        var ingredient = ingredientId is Guid id ? store.FindIngredient(id) : null;
        if (ingredient is null)
        {
            Title = "Nova namirnica";
            return;
        }
        Title = "Uredi namirnicu";
        Name = ingredient.Name;
        Icon = ingredient.Icon;
        relativeImagePath = ingredient.ImagePath;
        ImagePreviewPath = string.IsNullOrWhiteSpace(relativeImagePath)
            ? string.Empty
            : Path.Combine(FileSystem.AppDataDirectory, relativeImagePath.Replace('/', Path.DirectorySeparatorChar));
        SelectedFamily = ingredient.MeasurementFamily switch
        {
            MeasurementFamily.Mass => "Masa",
            MeasurementFamily.Volume => "Zapremina",
            MeasurementFamily.Count => "Komadi",
            _ => "Posebna jedinica"
        };
        BaseUnit = ingredient.BaseUnit;
        Offers.Clear();
        foreach (var offer in ingredient.Offers)
        {
            Offers.Add(new PackageOfferItem
            {
                PackageType = offer.PackageType,
                Amount = offer.Amount,
                Unit = offer.Unit,
                Store = offer.Store,
                Price = offer.Price,
                PreviousPrice = offer.PreviousPrice,
                Currency = offer.Currency,
                UpdatedAt = offer.UpdatedAt
            });
        }
    }
}
