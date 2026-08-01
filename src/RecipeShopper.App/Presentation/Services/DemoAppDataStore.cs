using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.Application.Abstractions.Persistence;
using RecipeShopper.Domain.ValueObjects;
using DomainEntities = RecipeShopper.Domain.Entities;
using DomainEnums = RecipeShopper.Domain.Enums;
using DomainMealPlanEntry = RecipeShopper.Domain.Entities.MealPlanEntry;
using DomainMealSlot = RecipeShopper.Domain.Entities.MealSlot;
using UiMeasurementFamily = RecipeShopper.App.Presentation.Models.MeasurementFamily;

namespace RecipeShopper.App.Presentation.Services;

/// <summary>
/// Repository-backed presentation state. It maps the UI models to the normalized domain repositories
/// so the existing synchronous view-model commands remain small while every mutation is persisted.
/// </summary>
public sealed class DemoAppDataStore : IAppDataStore
{
    private readonly IRecipeRepository recipeRepository;
    private readonly IIngredientRepository ingredientRepository;
    private readonly ICatalogRepository catalogRepository;
    private readonly IShoppingListRepository shoppingListRepository;
    private readonly IMealPlanRepository mealPlanRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly Dictionary<string, Guid> packageTypeIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, Guid> offerPackageIds = [];
    private readonly Dictionary<Guid, Guid> offerStoreIds = [];
    private Dictionary<string, Guid> seedIngredientIds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Guid> seedRecipeIds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Guid> seedMealSlotIds = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<RecipeItem> Recipes { get; } = [];
    public ObservableCollection<IngredientItem> Ingredients { get; } = [];
    public ObservableCollection<CategoryItem> Categories { get; } = [];
    public ObservableCollection<StoreItem> Stores { get; } = [];
    public ObservableCollection<NamedShoppingList> ShoppingLists { get; } = [];
    public ObservableCollection<DayMealPlan> WeekA { get; } = [];
    public ObservableCollection<DayMealPlan> WeekB { get; } = [];
    public AppSettings Settings { get; } = new();

    private Guid activeShoppingListId;
    public Guid ActiveShoppingListId
    {
        get => activeShoppingListId;
        set
        {
            if (ShoppingLists.Any(x => x.Id == value && !x.IsArchived))
            {
                activeShoppingListId = value;
            }
        }
    }

    public NamedShoppingList ActiveShoppingList =>
        ShoppingLists.FirstOrDefault(x => x.Id == ActiveShoppingListId && !x.IsArchived)
        ?? ShoppingLists.First(x => !x.IsArchived);

    public DemoAppDataStore(
        IRecipeRepository recipeRepository,
        IIngredientRepository ingredientRepository,
        ICatalogRepository catalogRepository,
        IShoppingListRepository shoppingListRepository,
        IMealPlanRepository mealPlanRepository,
        ISettingsRepository settingsRepository)
    {
        this.recipeRepository = recipeRepository;
        this.ingredientRepository = ingredientRepository;
        this.catalogRepository = catalogRepository;
        this.shoppingListRepository = shoppingListRepository;
        this.mealPlanRepository = mealPlanRepository;
        this.settingsRepository = settingsRepository;

        if (!LoadPersistentState())
        {
            Seed();
            PersistAll();
        }

        Settings.PropertyChanged += (_, _) => PersistSettings();
    }

    public RecipeItem? FindRecipe(Guid id) => Recipes.FirstOrDefault(x => x.Id == id);
    public IngredientItem? FindIngredient(Guid id) => Ingredients.FirstOrDefault(x => x.Id == id);

    public void SaveRecipe(RecipeItem recipe)
    {
        if (Recipes.All(x => x.Id != recipe.Id))
        {
            Recipes.Add(recipe);
        }

        PersistRecipe(recipe);
    }

    public void DeleteRecipe(Guid id)
    {
        var recipe = FindRecipe(id);
        if (recipe is not null)
        {
            Recipes.Remove(recipe);
            recipeRepository.ArchiveAsync(id).GetAwaiter().GetResult();
        }
    }

    public void SaveIngredient(IngredientItem ingredient)
    {
        if (Ingredients.All(x => x.Id != ingredient.Id))
        {
            Ingredients.Add(ingredient);
        }

        PersistIngredient(ingredient);
    }

    public void DeleteIngredient(Guid id)
    {
        var ingredient = FindIngredient(id);
        if (ingredient is not null)
        {
            ingredient.IsArchived = true;
            ingredientRepository.ArchiveAsync(id).GetAwaiter().GetResult();
        }
    }

    public void SaveStore(StoreItem store)
    {
        catalogRepository.UpsertStoreAsync(new DomainEntities.Store
        {
            Id = store.Id,
            Name = store.Name,
            IsEnabled = store.IsEnabled,
            SortOrder = Stores.IndexOf(store)
        }).GetAwaiter().GetResult();
    }

    public void AddRecipeToShoppingList(Guid recipeId, decimal multiplier, IEnumerable<Guid>? ingredientIds = null)
    {
        var recipe = FindRecipe(recipeId);
        if (recipe is null || multiplier <= 0 || ActiveShoppingList.IsArchived)
        {
            return;
        }

        var selected = ingredientIds?.ToHashSet();
        foreach (var line in recipe.Ingredients.Where(x => selected is null || selected.Contains(x.IngredientId)))
        {
            ActiveShoppingList.Contributions.Add(new ShoppingContribution
            {
                IngredientId = line.IngredientId,
                RecipeId = recipe.Id,
                SourceName = recipe.Name,
                Amount = line.Amount * multiplier,
                Unit = line.Unit
            });
        }

        PersistShoppingList(ActiveShoppingList);
    }

    public void AddManualShoppingItem(Guid ingredientId, decimal? amount, string unit, string note)
    {
        if (FindIngredient(ingredientId) is null || ActiveShoppingList.IsArchived)
        {
            return;
        }

        ActiveShoppingList.Contributions.Add(new ShoppingContribution
        {
            IngredientId = ingredientId,
            SourceName = "Ručno dodano",
            Amount = amount,
            Unit = unit,
            Note = note
        });
        PersistShoppingList(ActiveShoppingList);
    }

    public IReadOnlyList<ShoppingGroup> BuildShoppingGroups(ShoppingGrouping grouping, bool hideChecked)
    {
        var aggregate = ActiveShoppingList.Contributions
            .GroupBy(x => x.IngredientId)
            .Select(BuildShoppingItem)
            .Where(x => !hideChecked || !x.IsChecked)
            .OrderBy(x => Settings.MoveCheckedToBottom && x.IsChecked)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return grouping switch
        {
            ShoppingGrouping.Recipe => aggregate
                .GroupBy(GetRecipeGroup)
                .OrderBy(x => x.Key)
                .Select(x => new ShoppingGroup(x.Key, x))
                .ToList(),
            ShoppingGrouping.Store => aggregate
                .GroupBy(x => x.Store)
                .OrderBy(x => x.Key == "Bez trgovine" ? 1 : 0)
                .ThenBy(x => x.Key)
                .Select(x => new ShoppingGroup(x.Key, x))
                .ToList(),
            _ => [new ShoppingGroup("Sve namirnice", aggregate)]
        };
    }

    public ShoppingTotals CalculateShoppingTotals()
    {
        var items = ActiveShoppingList.Contributions.GroupBy(x => x.IngredientId).Select(BuildShoppingItem).Where(x => !x.IsChecked).ToList();
        var known = items.Where(x => x.HasCompletePrice).Sum(x => ParseMoney(x.PriceText));
        return new ShoppingTotals(known, items.Count(x => !x.HasCompletePrice), Settings.Currency);
    }

    public void SetShoppingItemChecked(Guid ingredientId, bool value)
    {
        ActiveShoppingList.CheckStates[ingredientId] = value;
        PersistShoppingList(ActiveShoppingList);
    }

    public void SelectOffer(Guid ingredientId, Guid? offerId)
    {
        if (offerId is null)
        {
            ActiveShoppingList.SelectedOfferIds.Remove(ingredientId);
        }
        else
        {
            ActiveShoppingList.SelectedOfferIds[ingredientId] = offerId.Value;
        }

        PersistShoppingList(ActiveShoppingList);
    }

    public void RemoveShoppingItem(Guid ingredientId)
    {
        foreach (var item in ActiveShoppingList.Contributions.Where(x => x.IngredientId == ingredientId).ToList())
        {
            ActiveShoppingList.Contributions.Remove(item);
        }
        ActiveShoppingList.CheckStates.Remove(ingredientId);
        ActiveShoppingList.SelectedOfferIds.Remove(ingredientId);
        PersistShoppingList(ActiveShoppingList);
    }

    public void ClearActiveShoppingList()
    {
        if (ActiveShoppingList.IsArchived)
        {
            return;
        }
        ActiveShoppingList.Contributions.Clear();
        ActiveShoppingList.CheckStates.Clear();
        ActiveShoppingList.SelectedOfferIds.Clear();
        PersistShoppingList(ActiveShoppingList);
    }

    public NamedShoppingList CreateShoppingList(string name)
    {
        var list = new NamedShoppingList { Name = string.IsNullOrWhiteSpace(name) ? "Nova kupovina" : name.Trim() };
        ShoppingLists.Add(list);
        ActiveShoppingListId = list.Id;
        PersistShoppingList(list);
        return list;
    }

    public NamedShoppingList DuplicateShoppingList(Guid id)
    {
        var source = ShoppingLists.First(x => x.Id == id);
        var copy = CreateShoppingList($"{source.Name} – kopija");
        foreach (var contribution in source.Contributions)
        {
            copy.Contributions.Add(new ShoppingContribution
            {
                IngredientId = contribution.IngredientId,
                RecipeId = contribution.RecipeId,
                SourceName = contribution.SourceName,
                Amount = contribution.Amount,
                Unit = contribution.Unit,
                Note = contribution.Note
            });
        }
        PersistShoppingList(copy);
        return copy;
    }

    public void CompleteShoppingList(Guid id)
    {
        var list = ShoppingLists.FirstOrDefault(x => x.Id == id);
        if (list is null)
        {
            return;
        }
        list.IsArchived = true;
        list.CompletedAt = DateTime.Now;
        if (ShoppingLists.All(x => x.IsArchived))
        {
            CreateShoppingList("Nova kupovina");
        }
        else if (ActiveShoppingListId == id)
        {
            ActiveShoppingListId = ShoppingLists.First(x => !x.IsArchived).Id;
        }

        PersistShoppingList(list);
    }

    public void DeleteShoppingList(Guid id)
    {
        var list = ShoppingLists.FirstOrDefault(x => x.Id == id);
        if (list is null)
        {
            return;
        }
        if (!list.IsArchived && ShoppingLists.Count(x => !x.IsArchived) == 1)
        {
            ClearActiveShoppingList();
            list.Name = "Moja kupovina";
            PersistShoppingList(list);
            return;
        }
        shoppingListRepository.DeleteAsync(id).GetAwaiter().GetResult();
        ShoppingLists.Remove(list);
        if (ActiveShoppingListId == id)
        {
            ActiveShoppingListId = ShoppingLists.First(x => !x.IsArchived).Id;
        }
    }

    public void AddPlannerSelectionToShoppingList(bool weekA, int? dayIndex = null)
    {
        var days = weekA ? WeekA : WeekB;
        foreach (var day in days.Where(x => dayIndex is null || x.DayIndex == dayIndex))
            foreach (var entry in day.Slots.SelectMany(x => x.Entries))
            {
                AddRecipeToShoppingList(entry.RecipeId, entry.Multiplier);
            }
    }

    public void AddMealPlanEntry(bool weekA, int dayIndex, Guid slotId, Guid recipeId, decimal multiplier)
    {
        var day = (weekA ? WeekA : WeekB).FirstOrDefault(x => x.DayIndex == dayIndex);
        var slot = day?.Slots.FirstOrDefault(x => x.Id == slotId);
        var recipe = FindRecipe(recipeId);
        if (slot is null || recipe is null || multiplier <= 0)
        {
            return;
        }
        slot.Entries.Add(new MealPlanEntry { RecipeId = recipe.Id, RecipeName = recipe.Name, RecipeIcon = recipe.Icon, Multiplier = multiplier });
        PersistPlanner();
    }

    public void RemoveMealPlanEntry(bool weekA, int dayIndex, Guid slotId, Guid entryId)
    {
        var slot = (weekA ? WeekA : WeekB).FirstOrDefault(x => x.DayIndex == dayIndex)?.Slots.FirstOrDefault(x => x.Id == slotId);
        var entry = slot?.Entries.FirstOrDefault(x => x.Id == entryId);
        if (entry is not null)
        {
            slot!.Entries.Remove(entry);
            PersistPlanner();
        }
    }

    public string BuildShareText(ShoppingGrouping grouping)
    {
        var lines = new List<string> { ActiveShoppingList.Name, string.Empty };
        foreach (var group in BuildShoppingGroups(grouping, false))
        {
            lines.Add(group.Name.ToUpperInvariant());
            lines.AddRange(group.Select(x => $"{(x.IsChecked ? "✓" : "☐")} {x.Name} — {x.QuantityText}"));
            lines.Add(string.Empty);
        }
        lines.Add(CalculateShoppingTotals().DisplayText);
        return string.Join(Environment.NewLine, lines);
    }

    public string ExportJson()
    {
        var export = new
        {
            schemaVersion = 1,
            exportedAtUtc = DateTime.UtcNow,
            categories = Categories.Select(x => new { x.Id, x.Name, x.Icon }),
            ingredients = Ingredients.Where(x => !x.IsArchived).Select(x => new
            {
                x.Id,
                x.Name,
                x.Icon,
                measurementFamily = x.MeasurementFamily.ToString(),
                x.BaseUnit,
                offers = x.Offers.Select(o => new { o.Id, o.PackageType, o.Amount, o.Unit, o.Store, o.Price, o.PreviousPrice, o.Currency, o.UpdatedAt })
            }),
            recipes = Recipes.Select(x => new
            {
                x.Id,
                x.Name,
                x.Category,
                x.Icon,
                x.Notes,
                ingredients = x.Ingredients.Select(i => new { i.IngredientId, i.Amount, i.Unit })
            })
        };
        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    public void ReloadPersistentState()
    {
        Recipes.Clear();
        Ingredients.Clear();
        Categories.Clear();
        Stores.Clear();
        ShoppingLists.Clear();
        WeekA.Clear();
        WeekB.Clear();
        packageTypeIds.Clear();
        offerPackageIds.Clear();
        offerStoreIds.Clear();

        if (!LoadPersistentState())
        {
            Seed();
            PersistAll();
        }
    }

    public void ResetDemoData()
    {
        foreach (var recipe in Recipes)
        {
            recipeRepository.ArchiveAsync(recipe.Id).GetAwaiter().GetResult();
        }
        foreach (var ingredient in Ingredients)
        {
            ingredientRepository.ArchiveAsync(ingredient.Id).GetAwaiter().GetResult();
        }
        foreach (var list in ShoppingLists)
        {
            shoppingListRepository.DeleteAsync(list.Id).GetAwaiter().GetResult();
        }
        foreach (var entry in mealPlanRepository.GetEntriesAsync().GetAwaiter().GetResult())
        {
            mealPlanRepository.DeleteEntryAsync(entry.Id).GetAwaiter().GetResult();
        }

        Recipes.Clear();
        Ingredients.Clear();
        Categories.Clear();
        Stores.Clear();
        ShoppingLists.Clear();
        WeekA.Clear();
        WeekB.Clear();
        Seed();
        PersistAll();
    }

    private ShoppingListItem BuildShoppingItem(IGrouping<Guid, ShoppingContribution> contributions)
    {
        var ingredient = FindIngredient(contributions.Key) ?? new IngredientItem { Id = contributions.Key, Name = "Nepoznata namirnica" };
        var normalized = contributions.Select(x => Normalize(x.Amount, x.Unit, ingredient.MeasurementFamily)).ToList();
        var canSum = normalized.All(x => x.HasValue);
        decimal? amount = canSum ? normalized.Sum(x => x!.Value) : null;
        var quantity = amount is null ? string.Join(" + ", contributions.Select(x => x.Amount is null ? "po ukusu" : $"{x.Amount:0.##} {x.Unit}")) : FormatBaseAmount(amount.Value, ingredient);
        var selectedOffer = ActiveShoppingList.SelectedOfferIds.TryGetValue(ingredient.Id, out var offerId)
            ? ingredient.Offers.FirstOrDefault(x => x.Id == offerId)
            : null;
        var packageCount = selectedOffer is not null && amount is not null
            ? (int)Math.Ceiling(amount.Value / Math.Max(0.0001m, Normalize(selectedOffer.Amount, selectedOffer.Unit, ingredient.MeasurementFamily) ?? selectedOffer.Amount))
            : 0;
        var complete = selectedOffer?.Price is not null && packageCount > 0;
        var price = complete ? selectedOffer!.Price!.Value * packageCount : 0m;

        var item = new ShoppingListItem
        {
            IngredientId = ingredient.Id,
            Name = ingredient.Name,
            Icon = ingredient.Icon,
            QuantityText = quantity,
            SourceSummary = string.Join(", ", contributions.Select(x => x.SourceName).Distinct()),
            Store = selectedOffer?.Store ?? "Bez trgovine",
            PriceText = complete ? $"{price:0.00} {selectedOffer!.Currency} · {packageCount} × {selectedOffer.SizeText}" : "Odaberi pakovanje i cijenu",
            Recommendation = FindRecommendation(ingredient, amount, selectedOffer),
            IsChecked = ActiveShoppingList.CheckStates.GetValueOrDefault(ingredient.Id),
            HasCompletePrice = complete
        };
        foreach (var id in contributions.Select(x => x.Id))
        {
            item.ContributionIds.Add(id);
        }
        return item;
    }

    private string GetRecipeGroup(ShoppingListItem item)
    {
        var contributions = ActiveShoppingList.Contributions.Where(x => x.IngredientId == item.IngredientId).ToList();
        var sources = contributions.Select(x => x.SourceName).Distinct().ToList();
        return sources.Count == 1 ? sources[0] : "Više recepata";
    }

    private static decimal ParseMoney(string value)
    {
        var first = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return decimal.TryParse(first, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : 0m;
    }

    private static decimal? Normalize(decimal? amount, string unit, MeasurementFamily family)
    {
        if (amount is null)
        {
            return null;
        }
        return unit.Trim().ToLowerInvariant() switch
        {
            "kg" => amount * 1000m,
            "g" => amount,
            "l" => amount * 1000m,
            "ml" => amount,
            "kašičica" => amount * 5m,
            "kašika" => amount * 15m,
            "šolja" => amount * 240m,
            "kom" => amount,
            _ when family == MeasurementFamily.Custom => amount,
            _ => null
        };
    }

    private static string FormatBaseAmount(decimal amount, IngredientItem ingredient)
    {
        if (ingredient.MeasurementFamily == MeasurementFamily.Mass && amount >= 1000m)
        {
            return $"{amount / 1000m:0.##} kg";
        }
        if (ingredient.MeasurementFamily == MeasurementFamily.Volume && amount >= 1000m)
        {
            return $"{amount / 1000m:0.##} l";
        }
        return $"{amount:0.##} {ingredient.BaseUnit}";
    }

    private static string FindRecommendation(IngredientItem ingredient, decimal? requiredAmount, PackageOfferItem? selected)
    {
        if (requiredAmount is null || ingredient.Offers.Count < 2)
        {
            return string.Empty;
        }
        var candidates = ingredient.Offers
            .Where(x => x.Price is not null && x.Amount > 0)
            .Select(x => new
            {
                Offer = x,
                Normalized = Normalize(x.Amount, x.Unit, ingredient.MeasurementFamily),
            })
            .Where(x => x.Normalized is not null)
            .Select(x => new
            {
                x.Offer,
                Count = (int)Math.Ceiling(requiredAmount.Value / x.Normalized!.Value),
                Cost = (int)Math.Ceiling(requiredAmount.Value / x.Normalized!.Value) * x.Offer.Price!.Value
            })
            .OrderBy(x => x.Cost)
            .ToList();
        if (candidates.Count == 0)
        {
            return string.Empty;
        }
        var best = candidates[0];
        if (selected is null)
        {
            return $"Savjet: {best.Count} × {best.Offer.SizeText} u {best.Offer.Store} košta {best.Cost:0.00} {best.Offer.Currency}.";
        }
        var selectedCandidate = candidates.FirstOrDefault(x => x.Offer.Id == selected.Id);
        if (selectedCandidate is not null && best.Cost < selectedCandidate.Cost)
        {
            return $"Uštedi {(selectedCandidate.Cost - best.Cost):0.00} {best.Offer.Currency} uz {best.Count} × {best.Offer.SizeText} u {best.Offer.Store}.";
        }
        return string.Empty;
    }

    private bool LoadPersistentState()
    {
        var domainCategories = catalogRepository.GetCategoriesAsync().GetAwaiter().GetResult();
        var domainPackageTypes = catalogRepository.GetPackageTypesAsync().GetAwaiter().GetResult();
        var domainStores = catalogRepository.GetStoresAsync().GetAwaiter().GetResult();
        var domainIngredients = ingredientRepository.GetAllAsync().GetAwaiter().GetResult();
        var domainRecipes = recipeRepository.GetAllAsync().GetAwaiter().GetResult();
        var domainLists = shoppingListRepository.GetAllAsync().GetAwaiter().GetResult();

        if (domainIngredients.Count == 0 && domainRecipes.Count == 0 && domainLists.Count == 0)
        {
            return false;
        }

        foreach (var category in domainCategories)
        {
            Categories.Add(new CategoryItem
            {
                Id = category.Id,
                Name = category.Name,
                Icon = DisplayIcon(category.IconKey)
            });
        }

        foreach (var packageType in domainPackageTypes)
        {
            packageTypeIds[packageType.Name] = packageType.Id;
        }

        foreach (var store in domainStores)
        {
            Stores.Add(new StoreItem { Id = store.Id, Name = store.Name, IsEnabled = store.IsEnabled });
        }

        var storeNames = domainStores.ToDictionary(x => x.Id, x => x.Name);
        var packageTypeNames = domainPackageTypes.ToDictionary(x => x.Id, x => x.Name);
        foreach (var ingredient in domainIngredients)
        {
            var item = new IngredientItem
            {
                Id = ingredient.Id,
                Name = ingredient.Name,
                Icon = ingredient.IconKey ?? "🥣",
                MeasurementFamily = ToUiFamily(ingredient.MeasurementFamily),
                BaseUnit = ingredient.BaseUnit,
                IsArchived = ingredient.IsArchived
            };

            foreach (var package in ingredient.Packages)
            {
                foreach (var offer in package.Offers)
                {
                    var offerItem = new PackageOfferItem
                    {
                        Id = offer.Id,
                        PackageType = packageTypeNames.GetValueOrDefault(package.PackageTypeId, "pakovanje"),
                        Amount = package.NetQuantity.Amount,
                        Unit = package.NetQuantity.Unit,
                        Store = storeNames.GetValueOrDefault(offer.StoreId, "Bez trgovine"),
                        Price = offer.IsAvailable ? offer.CurrentPrice.MinorUnits / 100m : null,
                        PreviousPrice = offer.PreviousPrice?.MinorUnits / 100m,
                        Currency = offer.CurrentPrice.Currency,
                        UpdatedAt = offer.PriceUpdatedAtUtc.LocalDateTime
                    };
                    item.Offers.Add(offerItem);
                    offerPackageIds[offer.Id] = package.Id;
                    offerStoreIds[offer.Id] = offer.StoreId;
                }
            }

            Ingredients.Add(item);
        }

        var categoryById = domainCategories.ToDictionary(x => x.Id);
        var ingredientById = Ingredients.ToDictionary(x => x.Id);
        foreach (var recipe in domainRecipes)
        {
            var category = categoryById.GetValueOrDefault(recipe.CategoryId);
            var item = new RecipeItem
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Category = category?.Name ?? "Ostalo",
                Icon = DisplayIcon(category?.IconKey),
                PhotoPath = recipe.ImagePath,
                Notes = recipe.Notes ?? string.Empty
            };
            foreach (var line in recipe.Ingredients.OrderBy(x => x.SortOrder))
            {
                var ingredient = ingredientById.GetValueOrDefault(line.IngredientId);
                item.Ingredients.Add(new RecipeIngredientItem
                {
                    IngredientId = line.IngredientId,
                    IngredientName = ingredient?.Name ?? "Nepoznata namirnica",
                    Icon = ingredient?.Icon ?? "🥣",
                    Amount = line.Quantity?.Amount,
                    Unit = line.Quantity?.Unit.ToString() ?? string.Empty
                });
            }
            Recipes.Add(item);
        }

        foreach (var list in domainLists)
        {
            var item = new NamedShoppingList
            {
                Id = list.Id,
                Name = list.Name,
                IsArchived = list.Status == DomainEnums.ShoppingListStatus.Completed,
                CreatedAt = list.CreatedAtUtc.LocalDateTime,
                CompletedAt = list.CompletedAtUtc?.LocalDateTime
            };
            foreach (var source in list.Sources)
            {
                foreach (var contribution in source.Contributions)
                {
                    item.Contributions.Add(new ShoppingContribution
                    {
                        Id = contribution.Id,
                        IngredientId = contribution.IngredientId,
                        RecipeId = source.RecipeId,
                        SourceName = source.DisplayName,
                        Amount = contribution.Quantity?.Amount,
                        Unit = contribution.Quantity?.Unit.ToString() ?? string.Empty,
                        Note = contribution.Note ?? string.Empty
                    });
                }
            }
            foreach (var state in list.Items)
            {
                item.CheckStates[state.IngredientId] = state.IsChecked;
                var selection = state.PackageSelections.FirstOrDefault();
                if (selection is not null)
                {
                    var offer = domainIngredients
                        .SelectMany(x => x.Packages)
                        .Where(x => x.Id == selection.PackageDefinitionId)
                        .SelectMany(x => x.Offers)
                        .FirstOrDefault(x => x.StoreId == selection.StoreId);
                    if (offer is not null)
                    {
                        item.SelectedOfferIds[state.IngredientId] = offer.Id;
                    }
                }
            }
            ShoppingLists.Add(item);
        }

        if (ShoppingLists.All(x => x.IsArchived))
        {
            var list = new NamedShoppingList { Name = "Moja kupovina" };
            ShoppingLists.Add(list);
            PersistShoppingList(list);
        }
        activeShoppingListId = ShoppingLists.First(x => !x.IsArchived).Id;

        LoadPlanner();
        LoadSettings();
        return true;
    }

    private void LoadPlanner()
    {
        var slots = mealPlanRepository.GetSlotsAsync().GetAwaiter().GetResult();
        var entries = mealPlanRepository.GetEntriesAsync().GetAwaiter().GetResult();
        CreateWeekFromDomain(WeekA, slots, entries.Where(x => x.Week == DomainEnums.AlternatingWeek.A));
        CreateWeekFromDomain(WeekB, slots, entries.Where(x => x.Week == DomainEnums.AlternatingWeek.B));
    }

    private void LoadSettings()
    {
        var settings = settingsRepository.GetAsync().GetAwaiter().GetResult();
        Settings.Appearance = settings.Theme switch
        {
            DomainEnums.AppTheme.Light => AppAppearance.Light,
            DomainEnums.AppTheme.Dark => AppAppearance.Dark,
            _ => AppAppearance.System
        };
        Settings.Accent = settings.AccentKey switch { "orange" => "Narandžasta", "blue" => "Plava", _ => "Zelena" };
        Settings.DefaultGrouping = settings.DefaultShoppingGrouping switch
        {
            DomainEnums.ShoppingGroupingMode.Recipe => ShoppingGrouping.Recipe,
            DomainEnums.ShoppingGroupingMode.Store => ShoppingGrouping.Store,
            _ => ShoppingGrouping.None
        };
        Settings.MoveCheckedToBottom = settings.CheckedItemBehavior == DomainEnums.CheckedItemBehavior.MoveToBottom;
        Settings.HideChecked = settings.CheckedItemBehavior == DomainEnums.CheckedItemBehavior.Hide;
        Settings.ConfirmDestructiveActions = settings.ConfirmDestructiveActions;
        Settings.ReferenceMonday = settings.WeekAReferenceMonday.ToDateTime(TimeOnly.MinValue);
        Settings.ReminderEnabled = settings.DailyReminderEnabled;
        Settings.ReminderTime = settings.DailyReminderTime.ToTimeSpan();
        Settings.Currency = settings.CurrencyCode;
    }

    private void PersistAll()
    {
        foreach (var category in Categories.Select((item, index) => new DomainEntities.RecipeCategory
        {
            Id = item.Id,
            Name = item.Name,
            IconKey = SemanticIcon(item.Name),
            SortOrder = index
        }))
        {
            catalogRepository.UpsertCategoryAsync(category).GetAwaiter().GetResult();
        }
        foreach (var store in Stores.Select((item, index) => new DomainEntities.Store
        {
            Id = item.Id,
            Name = item.Name,
            IsEnabled = item.IsEnabled,
            SortOrder = index
        }))
        {
            catalogRepository.UpsertStoreAsync(store).GetAwaiter().GetResult();
        }
        EnsurePackageTypes();
        foreach (var ingredient in Ingredients) PersistIngredient(ingredient);
        foreach (var recipe in Recipes) PersistRecipe(recipe);
        foreach (var list in ShoppingLists) PersistShoppingList(list);
        PersistPlanner();
        PersistSettings();
    }

    private void PersistRecipe(RecipeItem item)
    {
        var categoryId = Categories.FirstOrDefault(x => x.Name.Equals(item.Category, StringComparison.OrdinalIgnoreCase))?.Id
                         ?? Categories.FirstOrDefault(x => x.Name == "Ostalo")?.Id
                         ?? Categories.First().Id;
        var recipe = new DomainEntities.Recipe
        {
            Id = item.Id,
            Name = item.Name.Trim(),
            CategoryId = categoryId,
            ImagePath = item.PhotoPath,
            Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes,
            Ingredients = item.Ingredients.Select((line, index) => new DomainEntities.RecipeIngredient
            {
                RecipeId = item.Id,
                IngredientId = line.IngredientId,
                Quantity = line.Amount is > 0 && !string.IsNullOrWhiteSpace(line.Unit)
                    ? new Quantity(line.Amount.Value, new UnitCode(line.Unit))
                    : null,
                SortOrder = index
            }).ToList()
        };
        recipeRepository.UpsertAsync(recipe).GetAwaiter().GetResult();
    }

    private void PersistIngredient(IngredientItem item)
    {
        EnsurePackageTypes();
        var stores = Stores.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var ingredient = new DomainEntities.Ingredient
        {
            Id = item.Id,
            Name = item.Name.Trim(),
            IconKey = item.Icon,
            MeasurementFamily = ToDomainFamily(item.MeasurementFamily),
            BaseUnit = new UnitCode(item.BaseUnit)
        };
        foreach (var offerItem in item.Offers)
        {
            if (!stores.TryGetValue(offerItem.Store, out var store)) continue;
            if (!packageTypeIds.TryGetValue(offerItem.PackageType, out var packageTypeId)) continue;
            var packageId = offerPackageIds.GetValueOrDefault(offerItem.Id, Guid.NewGuid());
            offerPackageIds[offerItem.Id] = packageId;
            offerStoreIds[offerItem.Id] = store.Id;
            ingredient.Packages.Add(new DomainEntities.PackageDefinition
            {
                Id = packageId,
                IngredientId = item.Id,
                PackageTypeId = packageTypeId,
                Label = offerItem.PackageType,
                NetQuantity = new Quantity(Math.Max(offerItem.Amount, 0.0001m), new UnitCode(offerItem.Unit)),
                Offers =
                [
                    new DomainEntities.StoreOffer
                    {
                        Id = offerItem.Id,
                        PackageDefinitionId = packageId,
                        StoreId = store.Id,
                        CurrentPrice = new Money(ToMinorUnits(offerItem.Price), offerItem.Currency),
                        PreviousPrice = offerItem.PreviousPrice is null ? null : new Money(ToMinorUnits(offerItem.PreviousPrice), offerItem.Currency),
                        PriceUpdatedAtUtc = new DateTimeOffset(offerItem.UpdatedAt),
                        IsAvailable = offerItem.Price is not null
                    }
                ]
            });
        }
        ingredientRepository.UpsertAsync(ingredient).GetAwaiter().GetResult();
    }

    private void PersistShoppingList(NamedShoppingList item)
    {
        var list = new DomainEntities.ShoppingList
        {
            Id = item.Id,
            Name = item.Name,
            Status = item.IsArchived ? DomainEnums.ShoppingListStatus.Completed : DomainEnums.ShoppingListStatus.Active,
            CreatedAtUtc = new DateTimeOffset(item.CreatedAt),
            CompletedAtUtc = item.CompletedAt is null ? null : new DateTimeOffset(item.CompletedAt.Value)
        };
        foreach (var group in item.Contributions.GroupBy(x => new { x.RecipeId, x.SourceName }))
        {
            var source = new DomainEntities.ShoppingSource
            {
                ShoppingListId = item.Id,
                Type = group.Key.RecipeId is null ? DomainEnums.ShoppingSourceType.Manual : DomainEnums.ShoppingSourceType.Recipe,
                RecipeId = group.Key.RecipeId,
                DisplayName = group.Key.SourceName,
                Contributions = group.Select(value => new DomainEntities.ShoppingContribution
                {
                    Id = value.Id,
                    IngredientId = value.IngredientId,
                    IngredientNameSnapshot = FindIngredient(value.IngredientId)?.Name ?? "Nepoznata namirnica",
                    Quantity = value.Amount is > 0 && !string.IsNullOrWhiteSpace(value.Unit)
                        ? new Quantity(value.Amount.Value, new UnitCode(value.Unit))
                        : null,
                    Note = string.IsNullOrWhiteSpace(value.Note) ? null : value.Note
                }).ToList()
            };
            list.Sources.Add(source);
        }

        foreach (var ingredientId in item.CheckStates.Keys.Union(item.SelectedOfferIds.Keys).Distinct())
        {
            var state = new DomainEntities.ShoppingListItem
            {
                ShoppingListId = item.Id,
                IngredientId = ingredientId,
                IsChecked = item.CheckStates.GetValueOrDefault(ingredientId)
            };
            if (item.SelectedOfferIds.TryGetValue(ingredientId, out var offerId)
                && offerPackageIds.TryGetValue(offerId, out var packageId)
                && offerStoreIds.TryGetValue(offerId, out var storeId))
            {
                var offer = FindIngredient(ingredientId)?.Offers.FirstOrDefault(x => x.Id == offerId);
                if (offer?.Price is not null)
                {
                    state.SelectedStoreId = storeId;
                    state.PackageSelections.Add(new DomainEntities.PackageSelection
                    {
                        ShoppingListItemId = state.Id,
                        PackageDefinitionId = packageId,
                        StoreId = storeId,
                        Count = 1,
                        PricePerPackageSnapshot = new Money(ToMinorUnits(offer.Price), offer.Currency),
                        NetQuantitySnapshot = new Quantity(Math.Max(offer.Amount, 0.0001m), new UnitCode(offer.Unit))
                    });
                }
            }
            list.Items.Add(state);
        }
        shoppingListRepository.UpsertAsync(list).GetAwaiter().GetResult();
    }

    private void PersistPlanner()
    {
        var slotsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var firstDay = WeekA.FirstOrDefault() ?? WeekB.FirstOrDefault();
        if (firstDay is not null)
        {
            foreach (var slot in firstDay.Slots.Select((item, index) => new { Item = item, Index = index }))
            {
                slotsByName[slot.Item.Name] = slot.Item.Id;
                mealPlanRepository.UpsertSlotAsync(new DomainEntities.MealSlot
                {
                    Id = slot.Item.Id,
                    Name = slot.Item.Name,
                    SortOrder = slot.Index
                }).GetAwaiter().GetResult();
            }
        }

        var existing = mealPlanRepository.GetEntriesAsync().GetAwaiter().GetResult();
        foreach (var entry in existing)
        {
            mealPlanRepository.DeleteEntryAsync(entry.Id).GetAwaiter().GetResult();
        }
        PersistWeek(WeekA, DomainEnums.AlternatingWeek.A, slotsByName);
        PersistWeek(WeekB, DomainEnums.AlternatingWeek.B, slotsByName);
    }

    private void PersistWeek(IEnumerable<DayMealPlan> days, DomainEnums.AlternatingWeek week, IReadOnlyDictionary<string, Guid> slots)
    {
        foreach (var day in days)
            foreach (var slot in day.Slots)
                foreach (var entry in slot.Entries.Select((item, index) => new { Item = item, Index = index }))
                {
                    if (!slots.TryGetValue(slot.Name, out var slotId)) continue;
                    mealPlanRepository.UpsertEntryAsync(new DomainEntities.MealPlanEntry
                    {
                        Id = entry.Item.Id,
                        Week = week,
                        DayOfWeek = ToDayOfWeek(day.DayIndex),
                        MealSlotId = slotId,
                        RecipeId = entry.Item.RecipeId,
                        RecipeMultiplier = entry.Item.Multiplier,
                        SortOrder = entry.Index
                    }).GetAwaiter().GetResult();
                }
    }

    private void PersistSettings()
    {
        settingsRepository.SaveAsync(new DomainEntities.AppSettings
        {
            Theme = Settings.Appearance switch
            {
                AppAppearance.Light => DomainEnums.AppTheme.Light,
                AppAppearance.Dark => DomainEnums.AppTheme.Dark,
                _ => DomainEnums.AppTheme.System
            },
            AccentKey = Settings.Accent switch { "Narandžasta" => "orange", "Plava" => "blue", _ => "green" },
            DefaultShoppingGrouping = Settings.DefaultGrouping switch
            {
                ShoppingGrouping.Recipe => DomainEnums.ShoppingGroupingMode.Recipe,
                ShoppingGrouping.Store => DomainEnums.ShoppingGroupingMode.Store,
                _ => DomainEnums.ShoppingGroupingMode.None
            },
            CheckedItemBehavior = Settings.HideChecked
                ? DomainEnums.CheckedItemBehavior.Hide
                : Settings.MoveCheckedToBottom
                    ? DomainEnums.CheckedItemBehavior.MoveToBottom
                    : DomainEnums.CheckedItemBehavior.KeepInPlace,
            ConfirmDestructiveActions = Settings.ConfirmDestructiveActions,
            WeekAReferenceMonday = DateOnly.FromDateTime(Settings.ReferenceMonday),
            DailyReminderEnabled = Settings.ReminderEnabled,
            DailyReminderTime = TimeOnly.FromTimeSpan(Settings.ReminderTime),
            CurrencyCode = Settings.Currency
        }).GetAwaiter().GetResult();
    }

    private void EnsurePackageTypes()
    {
        var names = Ingredients.SelectMany(x => x.Offers).Select(x => x.PackageType)
            .Concat(["Pakovanje", "Kesica", "Kutija", "Boca", "Tegla", "Konzerva"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var name in names)
        {
            if (packageTypeIds.ContainsKey(name)) continue;
            var packageType = new DomainEntities.PackageType { Name = name, UnitLabel = name.ToLowerInvariant(), SortOrder = packageTypeIds.Count };
            packageTypeIds[name] = packageType.Id;
            catalogRepository.UpsertPackageTypeAsync(packageType).GetAwaiter().GetResult();
        }
    }

    private void CreateWeekFromDomain(
        ObservableCollection<DayMealPlan> target,
        IReadOnlyList<DomainMealSlot> slots,
        IEnumerable<DomainMealPlanEntry> entries)
    {
        var entriesByDay = entries.GroupBy(x => FromDayOfWeek(x.DayOfWeek)).ToDictionary(x => x.Key, x => x.ToList());
        var dayNames = new[] { "Ponedjeljak", "Utorak", "Srijeda", "Četvrtak", "Petak", "Subota", "Nedjelja" };
        for (var dayIndex = 0; dayIndex < dayNames.Length; dayIndex++)
        {
            var day = new DayMealPlan { DayIndex = dayIndex, DayName = dayNames[dayIndex], DateHint = $"Dan {dayIndex + 1}" };
            foreach (var slotDefinition in slots.OrderBy(x => x.SortOrder))
            {
                var slot = new MealSlotPlan { Id = slotDefinition.Id, Name = slotDefinition.Name };
                foreach (var entry in entriesByDay.GetValueOrDefault(dayIndex, []).Where(x => x.MealSlotId == slotDefinition.Id).OrderBy(x => x.SortOrder))
                {
                    var recipe = FindRecipe(entry.RecipeId);
                    slot.Entries.Add(new MealPlanEntry
                    {
                        Id = entry.Id,
                        RecipeId = entry.RecipeId,
                        RecipeName = recipe?.Name ?? "Nepoznat recept",
                        RecipeIcon = recipe?.Icon ?? "🍽️",
                        Multiplier = entry.RecipeMultiplier
                    });
                }
                day.Slots.Add(slot);
            }
            target.Add(day);
        }
    }

    private static UiMeasurementFamily ToUiFamily(DomainEnums.MeasurementFamily family) => family switch
    {
        DomainEnums.MeasurementFamily.Mass => UiMeasurementFamily.Mass,
        DomainEnums.MeasurementFamily.Volume => UiMeasurementFamily.Volume,
        DomainEnums.MeasurementFamily.Count => UiMeasurementFamily.Count,
        _ => UiMeasurementFamily.Custom
    };

    private static DomainEnums.MeasurementFamily ToDomainFamily(UiMeasurementFamily family) => family switch
    {
        UiMeasurementFamily.Mass => DomainEnums.MeasurementFamily.Mass,
        UiMeasurementFamily.Volume => DomainEnums.MeasurementFamily.Volume,
        UiMeasurementFamily.Count => DomainEnums.MeasurementFamily.Count,
        _ => DomainEnums.MeasurementFamily.Container
    };

    private static DayOfWeek ToDayOfWeek(int dayIndex) => (DayOfWeek)((dayIndex + 1) % 7);
    private static int FromDayOfWeek(DayOfWeek day) => ((int)day + 6) % 7;
    private static long ToMinorUnits(decimal? value) => (long)Math.Round((value ?? 0m) * 100m, MidpointRounding.AwayFromZero);

    private static string DisplayIcon(string? iconKey) => iconKey switch
    {
        "category-beef" => "🥩",
        "category-chicken" => "🍗",
        "category-fish" => "🐟",
        "category-soup" => "🍲",
        "category-dough" => "🥖",
        "category-sweet" => "🍰",
        "category-vegetarian" => "🥬",
        "category-salad" => "🥗",
        "category-breakfast" => "🍳",
        _ => "🍽️"
    };

    private static string SemanticIcon(string categoryName) => categoryName switch
    {
        "Govedina" => "category-beef",
        "Piletina" => "category-chicken",
        "Riba" => "category-fish",
        "Supa/Čorba" => "category-soup",
        "Tijesto" => "category-dough",
        "Slatko" => "category-sweet",
        "Vegetarijansko" => "category-vegetarian",
        "Salata" => "category-salad",
        "Doručak" => "category-breakfast",
        _ => "category-other"
    };

    private void Seed()
    {
        var existingCategories = catalogRepository.GetCategoriesAsync(true).GetAwaiter().GetResult()
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var existingStores = catalogRepository.GetStoresAsync(true).GetAwaiter().GetResult()
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var packageType in catalogRepository.GetPackageTypesAsync(true).GetAwaiter().GetResult())
        {
            packageTypeIds[packageType.Name] = packageType.Id;
        }
        seedIngredientIds = ingredientRepository.GetAllAsync(true).GetAwaiter().GetResult()
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
        seedRecipeIds = recipeRepository.GetAllAsync(true).GetAwaiter().GetResult()
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Id, StringComparer.OrdinalIgnoreCase);
        seedMealSlotIds = mealPlanRepository.GetSlotsAsync(true).GetAwaiter().GetResult()
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var item in new[]
        {
            ("Govedina", "🥩"), ("Piletina", "🍗"), ("Riba", "🐟"), ("Supa/Čorba", "🍲"),
            ("Tijesto", "🥖"), ("Slatko", "🍰"), ("Vegetarijansko", "🥬"), ("Salata", "🥗"),
            ("Doručak", "🍳"), ("Ostalo", "🍽️")
        })
        {
            Categories.Add(new CategoryItem
            {
                Id = existingCategories.GetValueOrDefault(item.Item1, Guid.NewGuid()),
                Name = item.Item1,
                Icon = item.Item2
            });
        }

        foreach (var store in new[] { "Bingo", "Konzum", "Mercator", "Lokalna pijaca" })
        {
            Stores.Add(new StoreItem { Id = existingStores.GetValueOrDefault(store, Guid.NewGuid()), Name = store });
        }

        var flour = AddIngredient("Brašno", "🌾", MeasurementFamily.Mass, "g",
            ("vreća", 1000m, "g", "Bingo", 2.20m), ("vreća", 2000m, "g", "Bingo", 3.80m), ("vreća", 5000m, "g", "Konzum", 9.70m));
        var eggs = AddIngredient("Jaja", "🥚", MeasurementFamily.Count, "kom",
            ("pakovanje", 10m, "kom", "Bingo", 4.20m), ("pakovanje", 30m, "kom", "Lokalna pijaca", 10.00m));
        var milk = AddIngredient("Mlijeko", "🥛", MeasurementFamily.Volume, "ml",
            ("boca", 1000m, "ml", "Konzum", 2.45m), ("boca", 1500m, "ml", "Bingo", 3.40m));
        var sugar = AddIngredient("Šećer", "🧂", MeasurementFamily.Mass, "g", ("vreća", 1000m, "g", "Bingo", 2.30m));
        var chicken = AddIngredient("Pileći file", "🍗", MeasurementFamily.Mass, "g", ("pakovanje", 500m, "g", "Mercator", 7.90m));
        var rice = AddIngredient("Riža", "🍚", MeasurementFamily.Mass, "g", ("vrećica", 1000m, "g", "Bingo", 4.50m));
        var carrots = AddIngredient("Mrkva", "🥕", MeasurementFamily.Mass, "g", ("vezica", 500m, "g", "Lokalna pijaca", 1.80m));
        var onion = AddIngredient("Luk", "🧅", MeasurementFamily.Mass, "g", ("mreža", 1000m, "g", "Lokalna pijaca", 2.50m));
        var butter = AddIngredient("Maslac", "🧈", MeasurementFamily.Mass, "g", ("pakovanje", 250m, "g", "Konzum", 5.20m));
        var cocoa = AddIngredient("Kakao", "🍫", MeasurementFamily.Mass, "g", ("kesica", 100m, "g", "Bingo", 2.10m));

        var pancakes = AddRecipe("Palačinke", "Slatko", "🥞", "Poslužiti uz omiljeni namaz.",
            (flour, 250m, "g"), (milk, 500m, "ml"), (eggs, 2m, "kom"), (sugar, 30m, "g"));
        var soup = AddRecipe("Pileća supa", "Supa/Čorba", "🍲", "Kuhati na laganoj vatri oko 45 minuta.",
            (chicken, 400m, "g"), (carrots, 250m, "g"), (onion, 100m, "g"));
        var riceDish = AddRecipe("Piletina s rižom", "Piletina", "🍛", "Začiniti po ukusu.",
            (chicken, 500m, "g"), (rice, 300m, "g"), (onion, 100m, "g"));
        AddRecipe("Čokoladni kolač", "Slatko", "🍰", "Peći 30 minuta na 180 °C.",
            (flour, 300m, "g"), (eggs, 4m, "kom"), (butter, 200m, "g"), (cocoa, 80m, "g"), (sugar, 200m, "g"));

        var list = new NamedShoppingList { Name = "Sedmična kupovina" };
        ShoppingLists.Add(list);
        activeShoppingListId = list.Id;
        AddRecipeToShoppingList(pancakes.Id, 2m);
        AddRecipeToShoppingList(soup.Id, 1m);
        list.SelectedOfferIds[flour.Id] = flour.Offers[1].Id;
        list.SelectedOfferIds[eggs.Id] = eggs.Offers[0].Id;

        CreateWeek(WeekA);
        CreateWeek(WeekB);
        WeekA[0].Slots[1].Entries.Add(new MealPlanEntry { RecipeId = soup.Id, RecipeName = soup.Name, RecipeIcon = soup.Icon });
        WeekA[1].Slots[1].Entries.Add(new MealPlanEntry { RecipeId = riceDish.Id, RecipeName = riceDish.Name, RecipeIcon = riceDish.Icon });
        WeekB[0].Slots[0].Entries.Add(new MealPlanEntry { RecipeId = pancakes.Id, RecipeName = pancakes.Name, RecipeIcon = pancakes.Icon, Multiplier = 1.5m });
    }

    private IngredientItem AddIngredient(string name, string icon, MeasurementFamily family, string baseUnit,
        params (string Type, decimal Amount, string Unit, string Store, decimal Price)[] offers)
    {
        var ingredient = new IngredientItem
        {
            Id = seedIngredientIds.GetValueOrDefault(name, Guid.NewGuid()),
            Name = name,
            Icon = icon,
            MeasurementFamily = family,
            BaseUnit = baseUnit
        };
        foreach (var offer in offers)
        {
            ingredient.Offers.Add(new PackageOfferItem
            {
                PackageType = offer.Type,
                Amount = offer.Amount,
                Unit = offer.Unit,
                Store = offer.Store,
                Price = offer.Price,
                Currency = Settings.Currency
            });
        }
        Ingredients.Add(ingredient);
        return ingredient;
    }

    private RecipeItem AddRecipe(string name, string category, string icon, string notes,
        params (IngredientItem Ingredient, decimal Amount, string Unit)[] ingredients)
    {
        var recipe = new RecipeItem
        {
            Id = seedRecipeIds.GetValueOrDefault(name, Guid.NewGuid()),
            Name = name,
            Category = category,
            Icon = icon,
            Notes = notes
        };
        foreach (var line in ingredients)
        {
            recipe.Ingredients.Add(new RecipeIngredientItem
            {
                IngredientId = line.Ingredient.Id,
                IngredientName = line.Ingredient.Name,
                Icon = line.Ingredient.Icon,
                Amount = line.Amount,
                Unit = line.Unit
            });
        }
        Recipes.Add(recipe);
        return recipe;
    }

    private void CreateWeek(ObservableCollection<DayMealPlan> week)
    {
        var days = new[] { "Ponedjeljak", "Utorak", "Srijeda", "Četvrtak", "Petak", "Subota", "Nedjelja" };
        for (var dayIndex = 0; dayIndex < days.Length; dayIndex++)
        {
            var day = new DayMealPlan { DayIndex = dayIndex, DayName = days[dayIndex], DateHint = $"Dan {dayIndex + 1}" };
            foreach (var slotName in new[] { "Doručak", "Ručak", "Večera", "Ostalo" })
            {
                day.Slots.Add(new MealSlotPlan
                {
                    Id = seedMealSlotIds.GetValueOrDefault(slotName, Guid.NewGuid()),
                    Name = slotName
                });
            }
            week.Add(day);
        }
    }
}
