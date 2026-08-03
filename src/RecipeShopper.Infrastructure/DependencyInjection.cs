using Microsoft.Extensions.DependencyInjection;
using RecipeShopper.Application.Abstractions.ImportExport;
using RecipeShopper.Application.Abstractions.Persistence;
using RecipeShopper.Infrastructure.Files;
using RecipeShopper.Infrastructure.ImportExport;
using RecipeShopper.Infrastructure.Persistence;
using RecipeShopper.Application.Abstractions.Sync;
using RecipeShopper.Infrastructure.Sync;

namespace RecipeShopper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRecipeShopperInfrastructure(
        this IServiceCollection services,
        string databasePath,
        string? imageDirectory = null,
        SupabaseSyncOptions? syncOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        imageDirectory ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, "images");

        services.AddSingleton(new RecipeShopperDatabase(new DatabaseOptions(databasePath)));
        services.AddSingleton<IImageAssetStore>(new AppImageStore(imageDirectory));
        services.AddSingleton<IRecipeRepository, RecipeRepository>();
        services.AddSingleton<IIngredientRepository, IngredientRepository>();
        services.AddSingleton<ICatalogRepository, CatalogRepository>();
        services.AddSingleton<IShoppingListRepository, ShoppingListRepository>();
        services.AddSingleton<IMealPlanRepository, MealPlanRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IUnitOfWork, SqliteUnitOfWork>();
        services.AddSingleton<IImportExportService>(provider => new JsonImportExportService(
            provider.GetRequiredService<RecipeShopperDatabase>(),
            provider.GetRequiredService<IImageAssetStore>()));
        syncOptions ??= new SupabaseSyncOptions(string.Empty, string.Empty);
        services.AddSingleton(syncOptions);
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        services.AddSingleton<IWorkspaceSyncService, SupabaseWorkspaceSyncService>();
        return services;
    }
}
