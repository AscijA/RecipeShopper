using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;

namespace RecipeShopper.Application.Abstractions.Persistence;

public interface IRecipeRepository
{
    Task<IReadOnlyList<Recipe>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(Recipe recipe, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IIngredientRepository
{
    Task<IReadOnlyList<Ingredient>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<Ingredient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(Ingredient ingredient, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ICatalogRepository
{
    Task<IReadOnlyList<RecipeCategory>> GetCategoriesAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task UpsertCategoryAsync(RecipeCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackageType>> GetPackageTypesAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task UpsertPackageTypeAsync(PackageType packageType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Store>> GetStoresAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task UpsertStoreAsync(Store store, CancellationToken cancellationToken = default);
}

public interface IShoppingListRepository
{
    Task<IReadOnlyList<ShoppingList>> GetAllAsync(ShoppingListStatus? status = null, CancellationToken cancellationToken = default);
    Task<ShoppingList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(ShoppingList shoppingList, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IMealPlanRepository
{
    Task<IReadOnlyList<MealSlot>> GetSlotsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task UpsertSlotAsync(MealSlot slot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MealPlanEntry>> GetEntriesAsync(AlternatingWeek? week = null, CancellationToken cancellationToken = default);
    Task UpsertEntryAsync(MealPlanEntry entry, CancellationToken cancellationToken = default);
    Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISettingsRepository
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
