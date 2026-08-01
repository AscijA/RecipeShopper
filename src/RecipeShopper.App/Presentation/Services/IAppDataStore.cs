using System.Collections.ObjectModel;
using RecipeShopper.App.Presentation.Models;

namespace RecipeShopper.App.Presentation.Services;

public interface IAppDataStore
{
    ObservableCollection<RecipeItem> Recipes { get; }
    ObservableCollection<IngredientItem> Ingredients { get; }
    ObservableCollection<CategoryItem> Categories { get; }
    ObservableCollection<StoreItem> Stores { get; }
    ObservableCollection<NamedShoppingList> ShoppingLists { get; }
    ObservableCollection<DayMealPlan> WeekA { get; }
    ObservableCollection<DayMealPlan> WeekB { get; }
    AppSettings Settings { get; }
    NamedShoppingList ActiveShoppingList { get; }
    Guid ActiveShoppingListId { get; set; }

    RecipeItem? FindRecipe(Guid id);
    IngredientItem? FindIngredient(Guid id);
    void SaveRecipe(RecipeItem recipe);
    void DeleteRecipe(Guid id);
    void SaveIngredient(IngredientItem ingredient);
    void DeleteIngredient(Guid id);
    void SaveStore(StoreItem store);
    void AddRecipeToShoppingList(Guid recipeId, decimal multiplier, IEnumerable<Guid>? ingredientIds = null);
    void AddManualShoppingItem(Guid ingredientId, decimal? amount, string unit, string note);
    IReadOnlyList<ShoppingGroup> BuildShoppingGroups(ShoppingGrouping grouping, bool hideChecked);
    ShoppingTotals CalculateShoppingTotals();
    void SetShoppingItemChecked(Guid ingredientId, bool value);
    void SelectOffer(Guid ingredientId, Guid? offerId);
    void RemoveShoppingItem(Guid ingredientId);
    void ClearActiveShoppingList();
    NamedShoppingList CreateShoppingList(string name);
    NamedShoppingList DuplicateShoppingList(Guid id);
    void CompleteShoppingList(Guid id);
    void DeleteShoppingList(Guid id);
    void AddPlannerSelectionToShoppingList(bool weekA, int? dayIndex = null);
    void AddMealPlanEntry(bool weekA, int dayIndex, Guid slotId, Guid recipeId, decimal multiplier);
    void RemoveMealPlanEntry(bool weekA, int dayIndex, Guid slotId, Guid entryId);
    string BuildShareText(ShoppingGrouping grouping);
    string ExportJson();
    void ReloadPersistentState();
    void ResetDemoData();
}

public readonly record struct ShoppingTotals(decimal KnownTotal, int MissingCount, string Currency)
{
    public bool IsComplete => MissingCount == 0;
    public string DisplayText => IsComplete
        ? $"Ukupno {KnownTotal:0.00} {Currency}"
        : $"Poznato {KnownTotal:0.00} {Currency} · nedostaje {MissingCount}";
}
