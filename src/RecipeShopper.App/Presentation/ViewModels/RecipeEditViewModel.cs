using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.Application.Abstractions.Platform;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class RecipeEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IAppDataStore store;
    private readonly INavigationService navigation;
    private readonly IImageService images;
    private readonly IDialogService dialogs;
    private Guid? recipeId;
    private string? relativePhotoPath;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string selectedCategory = "Ostalo";
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string photoPreviewPath = string.Empty;
    [ObservableProperty] private IngredientItem? selectedCatalogIngredient;

    public ObservableCollection<string> Categories { get; } = [];
    public ObservableCollection<IngredientItem> AvailableIngredients { get; } = [];
    public ObservableCollection<RecipeIngredientDraft> Ingredients { get; } = [];
    public ObservableCollection<string> Units { get; } = ["g", "kg", "ml", "l", "kom", "kašičica", "kašika", "šolja", "Šaka", "pakovanje"];
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPreviewPath);

    public RecipeEditViewModel(IAppDataStore store, INavigationService navigation, IImageService images, IDialogService dialogs)
    {
        this.store = store;
        this.navigation = navigation;
        this.images = images;
        this.dialogs = dialogs;
        foreach (var category in store.Categories.OrderBy(x => x.Name)) Categories.Add(category.Name);
        foreach (var ingredient in store.Ingredients.Where(x => !x.IsArchived).OrderBy(x => x.Name)) AvailableIngredients.Add(ingredient);
        Title = "Novi recept";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        recipeId = query.TryGetValue("RecipeId", out var value) && value is Guid id ? id : null;
        Load();
    }

    partial void OnPhotoPreviewPathChanged(string value) => OnPropertyChanged(nameof(HasPhoto));

    [RelayCommand]
    private void AddIngredient()
    {
        if (SelectedCatalogIngredient is null || Ingredients.Any(x => x.IngredientId == SelectedCatalogIngredient.Id))
        {
            return;
        }
        Ingredients.Add(new RecipeIngredientDraft
        {
            IngredientId = SelectedCatalogIngredient.Id,
            IngredientName = SelectedCatalogIngredient.Name,
            Icon = SelectedCatalogIngredient.Icon,
            Unit = SelectedCatalogIngredient.BaseUnit
        });
    }

    [RelayCommand]
    private void RemoveIngredient(RecipeIngredientDraft? ingredient)
    {
        if (ingredient is not null) Ingredients.Remove(ingredient);
    }

    [RelayCommand]
    private Task PickPhotoAsync() => RunBusyAsync(async () =>
    {
        try
        {
            var path = await images.PickAndStoreAsync();
            SetPhoto(path);
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync("Fotografija nije dostupna", ex.Message);
        }
    });

    [RelayCommand]
    private Task CapturePhotoAsync() => RunBusyAsync(async () =>
    {
        try
        {
            var path = await images.CaptureAndStoreAsync();
            SetPhoto(path);
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync("Kamera nije dostupna", ex.Message);
        }
    });

    [RelayCommand]
    private void RemovePhoto()
    {
        relativePhotoPath = null;
        PhotoPreviewPath = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await dialogs.AlertAsync("Naziv je obavezan", "Unesite naziv recepta.");
            return;
        }
        if (Ingredients.Count == 0)
        {
            await dialogs.AlertAsync("Dodajte namirnice", "Recept mora sadržavati barem jednu namirnicu.");
            return;
        }

        var invalid = Ingredients.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Amount) && (!decimal.TryParse(x.Amount, out var amount) || amount <= 0));
        if (invalid is not null)
        {
            await dialogs.AlertAsync("Neispravna količina", $"Provjerite količinu za „{invalid.IngredientName}“.");
            return;
        }

        var recipe = recipeId is Guid id ? store.FindRecipe(id) : null;
        recipe ??= new RecipeItem();
        recipe.Name = Name.Trim();
        recipe.Category = SelectedCategory;
        recipe.Icon = store.Categories.FirstOrDefault(x => x.Name == SelectedCategory)?.Icon ?? "🍽️";
        recipe.Notes = Notes.Trim();
        recipe.PhotoPath = relativePhotoPath;
        recipe.Ingredients.Clear();
        foreach (var draft in Ingredients)
        {
            var amount = decimal.TryParse(draft.Amount, out var parsed) ? parsed : (decimal?)null;
            recipe.Ingredients.Add(new RecipeIngredientItem
            {
                IngredientId = draft.IngredientId,
                IngredientName = draft.IngredientName,
                Icon = draft.Icon,
                Amount = amount,
                Unit = draft.Unit
            });
        }
        store.SaveRecipe(recipe);
        await navigation.GoBackAsync();
    }

    private void Load()
    {
        var recipe = recipeId is Guid id ? store.FindRecipe(id) : null;
        if (recipe is null)
        {
            Title = "Novi recept";
            return;
        }
        Title = "Uredi recept";
        Name = recipe.Name;
        SelectedCategory = recipe.Category;
        Notes = recipe.Notes;
        relativePhotoPath = recipe.PhotoPath;
        PhotoPreviewPath = string.IsNullOrWhiteSpace(relativePhotoPath) ? string.Empty : Path.Combine(FileSystem.AppDataDirectory, relativePhotoPath.Replace('/', Path.DirectorySeparatorChar));
        Ingredients.Clear();
        foreach (var line in recipe.Ingredients)
        {
            Ingredients.Add(new RecipeIngredientDraft
            {
                IngredientId = line.IngredientId,
                IngredientName = line.IngredientName,
                Icon = line.Icon,
                Amount = line.Amount?.ToString("0.##") ?? string.Empty,
                Unit = line.Unit
            });
        }
    }

    private void SetPhoto(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        relativePhotoPath = path;
        PhotoPreviewPath = Path.Combine(FileSystem.AppDataDirectory, path.Replace('/', Path.DirectorySeparatorChar));
    }
}
