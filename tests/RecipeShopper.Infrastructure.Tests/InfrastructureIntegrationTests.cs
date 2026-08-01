using System.Text.Json;
using RecipeShopper.Application.Models.ImportExport;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;
using RecipeShopper.Infrastructure.ImportExport;
using RecipeShopper.Infrastructure.Persistence;

namespace RecipeShopper.Infrastructure.Tests;

public sealed class InfrastructureIntegrationTests
{
    [Fact]
    public async Task Initialization_sets_pragmas_migrates_and_seeds_catalogs()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();

        Assert.Equal(DatabaseSchema.CurrentVersion, await fixture.Database.GetSchemaVersionAsync());
        Assert.True(await fixture.Database.AreForeignKeysEnabledAsync());
        Assert.Equal("wal", (await fixture.Database.GetJournalModeAsync()).ToLowerInvariant());
        Assert.Equal(10, (await fixture.Catalog.GetCategoriesAsync()).Count);
        Assert.Equal(6, (await fixture.Catalog.GetPackageTypesAsync()).Count);
        Assert.Equal(4, (await fixture.MealPlan.GetSlotsAsync()).Count);
        Assert.Equal("BAM", (await new SettingsRepository(fixture.Database).GetAsync()).CurrencyCode);
    }

    [Fact]
    public async Task Presentation_store_seeds_a_fresh_database_in_foreign_key_order()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Assert.True(await fixture.Database.AreForeignKeysEnabledAsync());

        var store = new DemoAppDataStore(
            fixture.Recipes,
            fixture.Ingredients,
            fixture.Catalog,
            fixture.Shopping,
            fixture.MealPlan,
            new SettingsRepository(fixture.Database));

        Assert.Equal(10, store.Ingredients.Count);
        Assert.Equal(4, store.Recipes.Count);
        Assert.Equal(7, store.ActiveShoppingList.Contributions.Count);

        var persistedIngredients = await fixture.Ingredients.GetAllAsync();
        var persistedRecipes = await fixture.Recipes.GetAllAsync();
        var persistedLists = await fixture.Shopping.GetAllAsync();
        var persistedList = Assert.Single(persistedLists);
        var persistedContributions = persistedList.Sources.SelectMany(source => source.Contributions).ToArray();

        Assert.Equal(10, persistedIngredients.Count);
        Assert.Equal(4, persistedRecipes.Count);
        Assert.Equal(2, persistedList.Sources.Count);
        Assert.Equal(7, persistedContributions.Length);
        Assert.Equal(2, persistedList.Items.Count);
        Assert.All(
            persistedContributions,
            contribution => Assert.Contains(persistedIngredients, ingredient => ingredient.Id == contribution.IngredientId));

        var reloadedStore = new DemoAppDataStore(
            fixture.Recipes,
            fixture.Ingredients,
            fixture.Catalog,
            fixture.Shopping,
            fixture.MealPlan,
            new SettingsRepository(fixture.Database));

        Assert.Equal(10, reloadedStore.Ingredients.Count);
        Assert.Equal(4, reloadedStore.Recipes.Count);
        Assert.Equal(7, reloadedStore.ActiveShoppingList.Contributions.Count);
    }

    [Fact]
    public async Task Repositories_round_trip_nested_catalog_recipe_and_shopping_snapshots()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var category = (await fixture.Catalog.GetCategoriesAsync()).First();
        var packageType = (await fixture.Catalog.GetPackageTypesAsync()).First();
        var store = new Store { Name = "Lokalna trgovina" };
        await fixture.Catalog.UpsertStoreAsync(store);

        var ingredient = new Ingredient
        {
            Name = "Brašno",
            MeasurementFamily = MeasurementFamily.Mass,
            BaseUnit = UnitCode.Gram,
            Packages =
            [
                new PackageDefinition
                {
                    PackageTypeId = packageType.Id, NetQuantity = new Quantity(1, UnitCode.Kilogram),
                    Offers = [new StoreOffer { StoreId = store.Id, CurrentPrice = new Money(265, "BAM") }]
                }
            ]
        };
        await fixture.Ingredients.UpsertAsync(ingredient);

        var recipe = new Recipe
        {
            Name = "Hljeb",
            CategoryId = category.Id,
            Ingredients = [new RecipeIngredient { IngredientId = ingredient.Id, Quantity = new Quantity(500, UnitCode.Gram) }]
        };
        await fixture.Recipes.UpsertAsync(recipe);
        var storedRecipe = await fixture.Recipes.GetByIdAsync(recipe.Id);
        Assert.Equal(500m, storedRecipe!.Ingredients.Single().Quantity!.Value.Amount);

        var list = new ShoppingList
        {
            Name = "Sedmična kupovina",
            Sources =
            [
                new ShoppingSource
                {
                    Type = ShoppingSourceType.Recipe, RecipeId = recipe.Id, DisplayName = recipe.Name,
                    Contributions =
                    [
                        new ShoppingContribution
                        {
                            IngredientId = ingredient.Id, IngredientNameSnapshot = ingredient.Name,
                            Quantity = new Quantity(500, UnitCode.Gram)
                        }
                    ]
                }
            ],
            Items =
            [
                new ShoppingListItem
                {
                    IngredientId = ingredient.Id, SelectedStoreId = store.Id,
                    PackageSelections =
                    [
                        new PackageSelection
                        {
                            PackageDefinitionId = ingredient.Packages[0].Id, StoreId = store.Id,
                            PricePerPackageSnapshot = new Money(265, "BAM"),
                            NetQuantitySnapshot = new Quantity(1, UnitCode.Kilogram)
                        }
                    ]
                }
            ]
        };
        await fixture.Shopping.UpsertAsync(list);
        var storedList = await fixture.Shopping.GetByIdAsync(list.Id);
        Assert.Equal("Brašno", storedList!.Sources.Single().Contributions.Single().IngredientNameSnapshot);
        Assert.Equal(265, storedList.Items.Single().PackageSelections.Single().PricePerPackageSnapshot.MinorUnits);
    }

    [Fact]
    public async Task Catalog_export_previews_and_round_trips_into_another_database()
    {
        await using var source = await DatabaseFixture.CreateAsync();
        var ingredient = new Ingredient { Name = "So", MeasurementFamily = MeasurementFamily.Mass, BaseUnit = UnitCode.Gram };
        await source.Ingredients.UpsertAsync(ingredient);
        var service = new JsonImportExportService(source.Database);
        await using var json = new MemoryStream();
        await service.ExportCatalogAsync(json);

        await using var target = await DatabaseFixture.CreateAsync();
        var importer = new JsonImportExportService(target.Database);
        json.Position = 0;
        var preview = await importer.PreviewAsync(json, ImportMode.Merge);
        Assert.True(preview.CanCommit);
        Assert.True(preview.Additions >= 1);

        json.Position = 0;
        await importer.ImportAsync(json, ImportMode.Merge, new Dictionary<string, ImportConflictResolution>());
        Assert.Contains(await target.Ingredients.GetAllAsync(), value => value.Name == "So");
    }

    [Fact]
    public async Task Failed_json_commit_rolls_back_all_prior_inserts()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var categoryId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var duplicateLineId = Guid.NewGuid().ToString("D");
        var document = new CatalogDocumentDto
        {
            Categories = [new CategoryDto(categoryId.ToString("D"), "Nova kategorija", "category-other", 99)],
            Ingredients =
            [
                new IngredientDto
                {
                    Id = ingredientId.ToString("D"), Name = "Nova namirnica",
                    MeasurementFamily = "Mass", BaseUnit = "g"
                }
            ],
            Recipes =
            [
                Recipe("Prvi", categoryId, ingredientId, duplicateLineId),
                Recipe("Drugi", categoryId, ingredientId, duplicateLineId)
            ]
        };
        await using var json = new MemoryStream();
        await JsonSerializer.SerializeAsync(json, document, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Position = 0;
        var service = new JsonImportExportService(fixture.Database);

        await Assert.ThrowsAnyAsync<Exception>(() => service.ImportAsync(json, ImportMode.Merge, new Dictionary<string, ImportConflictResolution>()));
        Assert.DoesNotContain(await fixture.Catalog.GetCategoriesAsync(true), item => item.Name == "Nova kategorija");
        Assert.DoesNotContain(await fixture.Ingredients.GetAllAsync(true), item => item.Name == "Nova namirnica");
    }

    private static RecipeDto Recipe(string name, Guid categoryId, Guid ingredientId, string lineId) => new()
    {
        Id = Guid.NewGuid().ToString("D"),
        Name = name,
        CategoryId = categoryId.ToString("D"),
        Ingredients =
        [
            new RecipeIngredientDto
            {
                Id = lineId, IngredientId = ingredientId.ToString("D"),
                Quantity = new QuantityDto { Amount = "1", Unit = "g" }
            }
        ]
    };

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        private DatabaseFixture(string directory, RecipeShopperDatabase database)
        {
            Directory = directory;
            Database = database;
            Catalog = new(database);
            Ingredients = new(database);
            Recipes = new(database);
            Shopping = new(database);
            MealPlan = new(database);
        }

        private string Directory { get; }
        public RecipeShopperDatabase Database { get; }
        public CatalogRepository Catalog { get; }
        public IngredientRepository Ingredients { get; }
        public RecipeRepository Recipes { get; }
        public ShoppingListRepository Shopping { get; }
        public MealPlanRepository MealPlan { get; }

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RecipeShopper.Tests", Guid.NewGuid().ToString("N"));
            var database = new RecipeShopperDatabase(new DatabaseOptions(Path.Combine(directory, "test.db3")));
            await database.InitializeAsync();
            return new DatabaseFixture(directory, database);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true);
        }
    }
}
