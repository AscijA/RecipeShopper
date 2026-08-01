using System.Globalization;
using System.Text.Json;
using RecipeShopper.Application.Abstractions.ImportExport;
using RecipeShopper.Application.Models.ImportExport;
using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;
using RecipeShopper.Infrastructure.Files;
using RecipeShopper.Infrastructure.Persistence;

namespace RecipeShopper.Infrastructure.ImportExport;

public sealed class JsonImportExportService : IImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly RecipeShopperDatabase _database;
    private readonly IImageAssetStore? _images;
    private readonly CatalogRepository _catalog;
    private readonly IngredientRepository _ingredients;
    private readonly RecipeRepository _recipes;
    private readonly ShoppingListRepository _shoppingLists;
    private readonly MealPlanRepository _mealPlan;
    private readonly SettingsRepository _settings;
    private readonly SqliteUnitOfWork _unitOfWork;

    public JsonImportExportService(RecipeShopperDatabase database, IImageAssetStore? images = null)
    {
        _database = database;
        _images = images;
        _catalog = new(database);
        _ingredients = new(database);
        _recipes = new(database);
        _shoppingLists = new(database);
        _mealPlan = new(database);
        _settings = new(database);
        _unitOfWork = new(database);
    }

    public async Task<ImportPreview> PreviewAsync(Stream json, ImportMode mode, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await ReadAsync(json, cancellationToken).ConfigureAwait(false);
            return await AnalyzeAsync(document, mode, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
        {
            return new ImportPreview(0, 0, [], [exception.Message]);
        }
    }

    public async Task<ImportResult> ImportAsync(
        Stream json,
        ImportMode mode,
        IReadOnlyDictionary<string, ImportConflictResolution> resolutions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolutions);
        var document = await ReadAsync(json, cancellationToken).ConfigureAwait(false);
        var preview = await AnalyzeAsync(document, mode, cancellationToken).ConfigureAwait(false);
        if (!preview.CanCommit) throw new InvalidDataException(string.Join(Environment.NewLine, preview.Errors));

        foreach (var conflict in preview.Conflicts)
        {
            if (!resolutions.ContainsKey(conflict.Key))
                throw new InvalidDataException($"Conflict '{conflict.Key}' has not been resolved.");
        }

        if (document.Backup is not null && _images is not null)
        {
            foreach (var image in document.Backup.Images)
            {
                var bytes = Convert.FromBase64String(image.Base64Data);
                await _images.WriteAsync(image.RelativePath, bytes, cancellationToken).ConfigureAwait(false);
            }
        }

        var counters = new ImportCounters();
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (mode == ImportMode.Replace) await ClearForReplaceAsync(document.Backup is not null, token).ConfigureAwait(false);
            var context = await ApplyCatalogAsync(document.Catalog, mode, preview, resolutions, counters, token).ConfigureAwait(false);
            if (document.Backup is not null)
                await ApplyBackupAsync(document.Backup, context, mode, preview, resolutions, counters, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return new ImportResult(counters.Added, counters.Merged, counters.Replaced, counters.KeptBoth);
    }

    public async Task ExportCatalogAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var document = await BuildCatalogAsync(metadata: false, cancellationToken).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(destination, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportBackupAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var catalog = await BuildCatalogAsync(metadata: true, cancellationToken).ConfigureAwait(false);
        var images = new List<ImageAssetDto>();
        if (_images is not null)
        {
            foreach (var path in catalog.Recipes.Select(recipe => recipe.ImagePath).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal))
            {
                var bytes = await _images.ReadAsync(path!, cancellationToken).ConfigureAwait(false);
                if (bytes is null) continue;
                images.Add(new ImageAssetDto(path!, MediaType(path!), AppImageStore.Sha256(bytes), Convert.ToBase64String(bytes)));
            }
        }

        var document = new BackupDocumentDto
        {
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Catalog = catalog,
            ShoppingLists = (await _shoppingLists.GetAllAsync(null, cancellationToken).ConfigureAwait(false)).Select(TransferMapper.ToDto).ToList(),
            MealSlots = (await _mealPlan.GetSlotsAsync(true, cancellationToken).ConfigureAwait(false)).Select(TransferMapper.ToDto).ToList(),
            MealPlanEntries = (await _mealPlan.GetEntriesAsync(null, cancellationToken).ConfigureAwait(false)).Select(TransferMapper.ToDto).ToList(),
            Settings = TransferMapper.ToDto(await _settings.GetAsync(cancellationToken).ConfigureAwait(false)),
            Images = images
        };
        await JsonSerializer.SerializeAsync(destination, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CatalogDocumentDto> BuildCatalogAsync(bool metadata, CancellationToken cancellationToken)
    {
        var categories = await _catalog.GetCategoriesAsync(metadata, cancellationToken).ConfigureAwait(false);
        var packageTypes = await _catalog.GetPackageTypesAsync(metadata, cancellationToken).ConfigureAwait(false);
        var stores = await _catalog.GetStoresAsync(metadata, cancellationToken).ConfigureAwait(false);
        var ingredients = await _ingredients.GetAllAsync(metadata, cancellationToken).ConfigureAwait(false);
        var recipes = await _recipes.GetAllAsync(metadata, cancellationToken).ConfigureAwait(false);
        return new CatalogDocumentDto
        {
            Categories = categories.Select(item => TransferMapper.ToDto(item, metadata)).ToList(),
            PackageTypes = packageTypes.Select(item => TransferMapper.ToDto(item, metadata)).ToList(),
            Stores = stores.Select(item => TransferMapper.ToDto(item, metadata)).ToList(),
            Ingredients = ingredients.Select(item => TransferMapper.ToDto(item, metadata)).ToList(),
            Recipes = recipes.Select(item => TransferMapper.ToDto(item, metadata)).ToList()
        };
    }

    private async Task<ImportPreview> AnalyzeAsync(ParsedDocument document, ImportMode mode, CancellationToken cancellationToken)
    {
        var errors = ImportValidator.Validate(document.Catalog, document.Backup).ToList();
        var localCategories = await _catalog.GetCategoriesAsync(true, cancellationToken).ConfigureAwait(false);
        var localPackageTypes = await _catalog.GetPackageTypesAsync(true, cancellationToken).ConfigureAwait(false);
        var localStores = await _catalog.GetStoresAsync(true, cancellationToken).ConfigureAwait(false);
        var localIngredients = await _ingredients.GetAllAsync(true, cancellationToken).ConfigureAwait(false);
        var localRecipes = await _recipes.GetAllAsync(true, cancellationToken).ConfigureAwait(false);

        ValidateReferences(document.Catalog, localCategories, localPackageTypes, localStores, localIngredients, errors);
        if (document.Backup is not null)
            ValidateBackupReferences(document.Backup, document.Catalog, localRecipes, localIngredients, localStores, errors);
        if (errors.Count > 0) return new ImportPreview(0, 0, [], errors.Distinct().ToArray());

        var incomingCount = document.Catalog.Categories.Count + document.Catalog.PackageTypes.Count + document.Catalog.Stores.Count +
                            document.Catalog.Ingredients.Count + document.Catalog.Recipes.Count;
        if (document.Backup is not null)
            incomingCount += document.Backup.ShoppingLists.Count + document.Backup.MealSlots.Count + document.Backup.MealPlanEntries.Count + 1;
        if (mode == ImportMode.Replace) return new ImportPreview(incomingCount, 0, [], []);

        var additions = 0;
        var merges = 0;
        var conflicts = new List<ImportConflict>();
        AnalyzeNamed(document.Catalog.Categories, localCategories, item => item.Id, item => item.Name,
            local => local.Id, local => local.Name,
            (incoming, local) => Different(incoming.IconKey, local.IconKey), "category", ref additions, ref merges, conflicts);
        AnalyzeNamed(document.Catalog.PackageTypes, localPackageTypes, item => item.Id, item => item.Name,
            local => local.Id, local => local.Name,
            (incoming, local) => Different(incoming.UnitLabel, local.UnitLabel), "package-type", ref additions, ref merges, conflicts);
        AnalyzeNamed(document.Catalog.Stores, localStores, item => item.Id, item => item.Name,
            local => local.Id, local => local.Name, (_, _) => false, "store", ref additions, ref merges, conflicts);
        AnalyzeNamed(document.Catalog.Ingredients, localIngredients, item => item.Id, item => item.Name,
            local => local.Id, local => local.Name,
            (incoming, local) => !string.Equals(incoming.MeasurementFamily, local.MeasurementFamily.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                 !string.Equals(incoming.BaseUnit, local.BaseUnit.Value, StringComparison.OrdinalIgnoreCase) || Different(incoming.IconKey, local.IconKey),
            "ingredient", ref additions, ref merges, conflicts);
        AnalyzeNamed(document.Catalog.Recipes, localRecipes, item => item.Id, item => item.Name,
            local => local.Id, local => local.Name,
            (incoming, local) => Different(incoming.Notes, local.Notes) ||
                                 (Guid.TryParse(incoming.CategoryId, out var categoryId) && categoryId != local.CategoryId),
            "recipe", ref additions, ref merges, conflicts);

        if (document.Backup is not null)
        {
            var lists = await _shoppingLists.GetAllAsync(null, cancellationToken).ConfigureAwait(false);
            var slots = await _mealPlan.GetSlotsAsync(true, cancellationToken).ConfigureAwait(false);
            var entries = await _mealPlan.GetEntriesAsync(null, cancellationToken).ConfigureAwait(false);
            additions += document.Backup.ShoppingLists.Count(item => lists.All(local => local.Id.ToString("D") != item.Id));
            merges += document.Backup.ShoppingLists.Count(item => lists.Any(local => local.Id.ToString("D") == item.Id));
            AnalyzeNamed(document.Backup.MealSlots, slots, item => item.Id, item => item.Name, local => local.Id, local => local.Name,
                (_, _) => false, "meal-slot", ref additions, ref merges, conflicts);
            additions += document.Backup.MealPlanEntries.Count(item => entries.All(local => local.Id.ToString("D") != item.Id));
            merges += document.Backup.MealPlanEntries.Count(item => entries.Any(local => local.Id.ToString("D") == item.Id));
            merges++;
        }

        return new ImportPreview(additions, merges, conflicts, []);
    }

    private async Task<ImportContext> ApplyCatalogAsync(
        CatalogDocumentDto document,
        ImportMode mode,
        ImportPreview preview,
        IReadOnlyDictionary<string, ImportConflictResolution> resolutions,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var context = new ImportContext();
        var categories = (await _catalog.GetCategoriesAsync(true, cancellationToken).ConfigureAwait(false)).ToList();
        var packageTypes = (await _catalog.GetPackageTypesAsync(true, cancellationToken).ConfigureAwait(false)).ToList();
        var stores = (await _catalog.GetStoresAsync(true, cancellationToken).ConfigureAwait(false)).ToList();
        var ingredients = (await _ingredients.GetAllAsync(true, cancellationToken).ConfigureAwait(false)).ToList();
        var recipes = (await _recipes.GetAllAsync(true, cancellationToken).ConfigureAwait(false)).ToList();

        foreach (var dto in document.Categories)
        {
            var local = Find(dto.Id, dto.Name, categories, item => item.Id, item => item.Name);
            var decision = Decision("category", dto.Id, dto.Name, local is not null, mode, preview, resolutions, counters);
            var finalId = FinalId(dto.Id, local?.Id, decision);
            context.CategoryIds.Add(dto.Id, finalId);
            if (decision == ImportDecision.KeepLocal) { context.CategoryNames[TextNormalization.NameKey(dto.Name)] = local!.Id; continue; }
            var entity = TransferMapper.FromDto(dto, finalId, decision == ImportDecision.Merge ? local : null);
            if (decision == ImportDecision.KeepBoth) entity.Name = UniqueName(entity.Name, categories.Select(item => item.Name));
            await _catalog.UpsertCategoryAsync(entity, cancellationToken).ConfigureAwait(false);
            Replace(categories, local, entity);
            context.CategoryNames[TextNormalization.NameKey(dto.Name)] = entity.Id;
        }

        foreach (var dto in document.PackageTypes)
        {
            var local = Find(dto.Id, dto.Name, packageTypes, item => item.Id, item => item.Name);
            var decision = Decision("package-type", dto.Id, dto.Name, local is not null, mode, preview, resolutions, counters);
            var finalId = FinalId(dto.Id, local?.Id, decision);
            context.PackageTypeIds.Add(dto.Id, finalId);
            if (decision == ImportDecision.KeepLocal) { context.PackageTypeNames[TextNormalization.NameKey(dto.Name)] = local!.Id; continue; }
            var entity = TransferMapper.FromDto(dto, finalId, decision == ImportDecision.Merge ? local : null);
            if (decision == ImportDecision.KeepBoth) entity.Name = UniqueName(entity.Name, packageTypes.Select(item => item.Name));
            await _catalog.UpsertPackageTypeAsync(entity, cancellationToken).ConfigureAwait(false);
            Replace(packageTypes, local, entity);
            context.PackageTypeNames[TextNormalization.NameKey(dto.Name)] = entity.Id;
        }

        foreach (var dto in document.Stores)
        {
            var local = Find(dto.Id, dto.Name, stores, item => item.Id, item => item.Name);
            var decision = Decision("store", dto.Id, dto.Name, local is not null, mode, preview, resolutions, counters);
            var finalId = FinalId(dto.Id, local?.Id, decision);
            context.StoreIds.Add(dto.Id, finalId);
            if (decision == ImportDecision.KeepLocal) { context.StoreNames[TextNormalization.NameKey(dto.Name)] = local!.Id; continue; }
            var entity = TransferMapper.FromDto(dto, finalId, decision == ImportDecision.Merge ? local : null);
            if (decision == ImportDecision.KeepBoth) entity.Name = UniqueName(entity.Name, stores.Select(item => item.Name));
            await _catalog.UpsertStoreAsync(entity, cancellationToken).ConfigureAwait(false);
            Replace(stores, local, entity);
            context.StoreNames[TextNormalization.NameKey(dto.Name)] = entity.Id;
        }

        foreach (var dto in document.Ingredients)
        {
            var local = Find(dto.Id, dto.Name, ingredients, item => item.Id, item => item.Name);
            var decision = Decision("ingredient", dto.Id, dto.Name, local is not null, mode, preview, resolutions, counters);
            var finalId = FinalId(dto.Id, local?.Id, decision);
            context.IngredientIds.Add(dto.Id, finalId);
            context.IngredientNames[TextNormalization.NameKey(dto.Name)] = finalId;
            if (decision == ImportDecision.KeepLocal)
            {
                MapLocalPackages(dto, local!, context);
                continue;
            }

            var entity = TransferMapper.FromDto(dto, finalId, decision == ImportDecision.Merge ? local : null);
            if (decision == ImportDecision.KeepBoth) entity.Name = UniqueName(entity.Name, ingredients.Select(item => item.Name));
            entity.Packages = BuildPackages(dto, entity.Id, decision == ImportDecision.Merge ? local : null, context);
            await _ingredients.UpsertAsync(entity, cancellationToken).ConfigureAwait(false);
            Replace(ingredients, local, entity);
        }

        foreach (var dto in document.Recipes)
        {
            var local = Find(dto.Id, dto.Name, recipes, item => item.Id, item => item.Name);
            var decision = Decision("recipe", dto.Id, dto.Name, local is not null, mode, preview, resolutions, counters);
            var finalId = FinalId(dto.Id, local?.Id, decision);
            context.RecipeIds.Add(dto.Id, finalId);
            context.RecipeNames[TextNormalization.NameKey(dto.Name)] = finalId;
            if (decision == ImportDecision.KeepLocal) continue;
            var categoryId = Resolve(dto.CategoryId, dto.CategoryName, context.CategoryIds, context.CategoryNames);
            var entity = TransferMapper.FromDto(dto, finalId, categoryId, decision == ImportDecision.Merge ? local : null);
            var importedItems = dto.Ingredients.Select(item =>
            {
                var ingredientId = Resolve(item.IngredientId, item.IngredientName, context.IngredientIds, context.IngredientNames);
                var existing = local?.Ingredients.FirstOrDefault(value =>
                    (Guid.TryParse(item.Id, out var itemId) && value.Id == itemId) || value.IngredientId == ingredientId);
                return TransferMapper.FromDto(item, ParsedOrNew(item.Id, existing?.Id), entity.Id, ingredientId,
                    decision == ImportDecision.Merge ? existing : null);
            }).ToList();
            if (decision == ImportDecision.Merge && local is not null)
                importedItems.AddRange(local.Ingredients.Where(old => importedItems.All(item => item.IngredientId != old.IngredientId)));
            entity.Ingredients = importedItems.OrderBy(item => item.SortOrder).ToList();
            await _recipes.UpsertAsync(entity, cancellationToken).ConfigureAwait(false);
            Replace(recipes, local, entity);
        }

        return context;
    }

    private async Task ApplyBackupAsync(
        BackupDocumentDto backup,
        ImportContext context,
        ImportMode mode,
        ImportPreview preview,
        IReadOnlyDictionary<string, ImportConflictResolution> resolutions,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var existingLists = await _shoppingLists.GetAllAsync(null, cancellationToken).ConfigureAwait(false);
        foreach (var dto in backup.ShoppingLists)
        {
            var local = existingLists.FirstOrDefault(item => item.Id.ToString("D") == dto.Id);
            var decision = Decision("shopping-list", dto.Id, dto.Name, local is not null, mode, preview, resolutions, counters);
            if (decision == ImportDecision.KeepLocal) continue;
            var clone = decision == ImportDecision.KeepBoth;
            var list = TransferMapper.FromDto(dto, clone ? Guid.NewGuid() : ParsedOrNew(dto.Id, local?.Id));
            foreach (var sourceDto in dto.Sources)
            {
                var source = new ShoppingSource
                {
                    Id = clone ? Guid.NewGuid() : ParsedOrNew(sourceDto.Id),
                    ShoppingListId = list.Id,
                    Type = Enum.Parse<ShoppingSourceType>(sourceDto.Type, true),
                    RecipeId = sourceDto.RecipeId is null ? null : ResolveId(sourceDto.RecipeId, context.RecipeIds),
                    DisplayName = sourceDto.DisplayName,
                    Multiplier = decimal.Parse(sourceDto.Multiplier, NumberStyles.Number, CultureInfo.InvariantCulture),
                    CreatedAtUtc = sourceDto.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                    UpdatedAtUtc = sourceDto.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
                    ArchivedAtUtc = sourceDto.ArchivedAtUtc
                };
                source.Contributions = sourceDto.Contributions.Select(value => new ShoppingContribution
                {
                    Id = clone ? Guid.NewGuid() : ParsedOrNew(value.Id),
                    ShoppingSourceId = source.Id,
                    IngredientId = ResolveId(value.IngredientId, context.IngredientIds),
                    IngredientNameSnapshot = value.IngredientName,
                    Quantity = value.Quantity is null ? null : TransferMapper.FromDto(value.Quantity),
                    Note = value.Note,
                    CreatedAtUtc = value.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                    UpdatedAtUtc = value.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
                    ArchivedAtUtc = value.ArchivedAtUtc
                }).ToList();
                list.Sources.Add(source);
            }

            foreach (var itemDto in dto.Items)
            {
                var item = new ShoppingListItem
                {
                    Id = clone ? Guid.NewGuid() : ParsedOrNew(itemDto.Id),
                    ShoppingListId = list.Id,
                    IngredientId = ResolveId(itemDto.IngredientId, context.IngredientIds),
                    IsChecked = itemDto.IsChecked,
                    SelectedStoreId = itemDto.SelectedStoreId is null ? null : ResolveId(itemDto.SelectedStoreId, context.StoreIds),
                    Note = itemDto.Note,
                    CreatedAtUtc = itemDto.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                    UpdatedAtUtc = itemDto.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
                    ArchivedAtUtc = itemDto.ArchivedAtUtc
                };
                item.PackageSelections = itemDto.PackageSelections.Select(value => new PackageSelection
                {
                    Id = clone ? Guid.NewGuid() : ParsedOrNew(value.Id),
                    ShoppingListItemId = item.Id,
                    PackageDefinitionId = ResolveId(value.PackageDefinitionId, context.PackageIds),
                    StoreId = ResolveId(value.StoreId, context.StoreIds),
                    Count = value.Count,
                    PricePerPackageSnapshot = new Money(value.PricePerPackage.MinorUnits, value.PricePerPackage.Currency),
                    NetQuantitySnapshot = TransferMapper.FromDto(value.NetQuantity),
                    CreatedAtUtc = value.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                    UpdatedAtUtc = value.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
                    ArchivedAtUtc = value.ArchivedAtUtc
                }).ToList();
                list.Items.Add(item);
            }
            await _shoppingLists.UpsertAsync(list, cancellationToken).ConfigureAwait(false);
        }

        var slots = (await _mealPlan.GetSlotsAsync(true, cancellationToken).ConfigureAwait(false)).ToList();
        foreach (var dto in backup.MealSlots)
        {
            var local = Find(dto.Id, dto.Name, slots, item => item.Id, item => item.Name);
            var decision = Decision("meal-slot", dto.Id, dto.Name, local is not null, mode, preview, resolutions, counters);
            var id = FinalId(dto.Id, local?.Id, decision);
            context.MealSlotIds.Add(dto.Id, id);
            if (decision == ImportDecision.KeepLocal) continue;
            var slot = new MealSlot
            {
                Id = id,
                Name = decision == ImportDecision.KeepBoth ? UniqueName(dto.Name, slots.Select(item => item.Name)) : dto.Name,
                SortOrder = dto.SortOrder,
                CreatedAtUtc = dto.CreatedAtUtc ?? local?.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                UpdatedAtUtc = dto.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
                ArchivedAtUtc = dto.ArchivedAtUtc
            };
            await _mealPlan.UpsertSlotAsync(slot, cancellationToken).ConfigureAwait(false);
            Replace(slots, local, slot);
        }

        var entries = await _mealPlan.GetEntriesAsync(null, cancellationToken).ConfigureAwait(false);
        foreach (var dto in backup.MealPlanEntries)
        {
            var local = entries.FirstOrDefault(item => item.Id.ToString("D") == dto.Id);
            var decision = Decision("meal-plan-entry", dto.Id, dto.Id, local is not null, mode, preview, resolutions, counters);
            if (decision == ImportDecision.KeepLocal) continue;
            var entry = new MealPlanEntry
            {
                Id = decision == ImportDecision.KeepBoth ? Guid.NewGuid() : ParsedOrNew(dto.Id, local?.Id),
                Week = Enum.Parse<AlternatingWeek>(dto.Week, true),
                DayOfWeek = Enum.Parse<DayOfWeek>(dto.DayOfWeek, true),
                MealSlotId = ResolveId(dto.MealSlotId, context.MealSlotIds),
                RecipeId = ResolveId(dto.RecipeId, context.RecipeIds),
                RecipeMultiplier = decimal.Parse(dto.Multiplier, NumberStyles.Number, CultureInfo.InvariantCulture),
                SortOrder = dto.SortOrder,
                CreatedAtUtc = dto.CreatedAtUtc ?? local?.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                UpdatedAtUtc = dto.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
                ArchivedAtUtc = dto.ArchivedAtUtc
            };
            await _mealPlan.UpsertEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        await _settings.SaveAsync(TransferMapper.FromDto(backup.Settings), cancellationToken).ConfigureAwait(false);
    }

    private List<PackageDefinition> BuildPackages(IngredientDto dto, Guid ingredientId, Ingredient? local, ImportContext context)
    {
        var result = local?.Packages.ToList() ?? [];
        foreach (var packageDto in dto.Packages)
        {
            var packageTypeId = Resolve(packageDto.PackageTypeId, packageDto.PackageTypeName, context.PackageTypeIds, context.PackageTypeNames);
            var existing = result.FirstOrDefault(item =>
                (Guid.TryParse(packageDto.Id, out var id) && item.Id == id) ||
                (item.PackageTypeId == packageTypeId && item.NetQuantity == TransferMapper.FromDto(packageDto.NetQuantity) && item.Label == packageDto.Label));
            var packageId = ParsedOrNew(packageDto.Id, existing?.Id);
            context.PackageIds.Add(packageDto.Id, packageId);
            var package = TransferMapper.FromDto(packageDto, packageId, ingredientId, packageTypeId, existing);
            var offers = existing?.Offers.ToList() ?? [];
            foreach (var offerDto in packageDto.Offers)
            {
                var storeId = Resolve(offerDto.StoreId, offerDto.StoreName, context.StoreIds, context.StoreNames);
                var oldOffer = offers.FirstOrDefault(item =>
                    (Guid.TryParse(offerDto.Id, out var offerId) && item.Id == offerId) ||
                    (item.StoreId == storeId && item.CurrentPrice.Currency == offerDto.CurrentPrice.Currency));
                var offer = TransferMapper.FromDto(offerDto, ParsedOrNew(offerDto.Id, oldOffer?.Id), package.Id, storeId, oldOffer);
                if (oldOffer is not null) offers.Remove(oldOffer);
                offers.Add(offer);
            }
            package.Offers = offers;
            if (existing is not null) result.Remove(existing);
            result.Add(package);
        }
        return result;
    }

    private static void MapLocalPackages(IngredientDto dto, Ingredient local, ImportContext context)
    {
        foreach (var packageDto in dto.Packages)
        {
            var match = Guid.TryParse(packageDto.Id, out var id) ? local.Packages.FirstOrDefault(item => item.Id == id) : null;
            match ??= local.Packages.FirstOrDefault();
            if (match is not null) context.PackageIds.Add(packageDto.Id, match.Id);
        }
    }

    private async Task ClearForReplaceAsync(bool fullBackup, CancellationToken cancellationToken) =>
        await _database.WriteAsync(connection =>
        {
            var tables = new[]
            {
                "package_selections", "shopping_item_states", "shopping_contributions", "shopping_sources", "shopping_lists",
                "meal_plan_entries", "store_offers", "package_definitions", "recipe_ingredients", "recipes", "ingredients",
                "stores", "package_types", "recipe_categories"
            };
            foreach (var table in tables) connection.Execute($"DELETE FROM {table}");
            if (fullBackup)
            {
                connection.Execute("DELETE FROM meal_slots");
                connection.Execute("DELETE FROM app_settings");
            }
        }, cancellationToken).ConfigureAwait(false);

    private static void ValidateReferences(
        CatalogDocumentDto document,
        IReadOnlyList<RecipeCategory> localCategories,
        IReadOnlyList<PackageType> localPackageTypes,
        IReadOnlyList<Store> localStores,
        IReadOnlyList<Ingredient> localIngredients,
        List<string> errors)
    {
        var categoryIds = IdSet(document.Categories.Select(item => item.Id), localCategories.Select(item => item.Id));
        var categoryNames = NameSet(document.Categories.Select(item => item.Name), localCategories.Select(item => item.Name));
        var typeIds = IdSet(document.PackageTypes.Select(item => item.Id), localPackageTypes.Select(item => item.Id));
        var typeNames = NameSet(document.PackageTypes.Select(item => item.Name), localPackageTypes.Select(item => item.Name));
        var storeIds = IdSet(document.Stores.Select(item => item.Id), localStores.Select(item => item.Id));
        var storeNames = NameSet(document.Stores.Select(item => item.Name), localStores.Select(item => item.Name));
        var ingredientIds = IdSet(document.Ingredients.Select(item => item.Id), localIngredients.Select(item => item.Id));
        var ingredientNames = NameSet(document.Ingredients.Select(item => item.Name), localIngredients.Select(item => item.Name));

        foreach (var ingredient in document.Ingredients)
            foreach (var package in ingredient.Packages)
            {
                if (!ReferenceExists(package.PackageTypeId, package.PackageTypeName, typeIds, typeNames))
                    errors.Add($"Ingredient '{ingredient.Name}' references an unknown package type.");
                foreach (var offer in package.Offers)
                    if (!ReferenceExists(offer.StoreId, offer.StoreName, storeIds, storeNames))
                        errors.Add($"Ingredient '{ingredient.Name}' references an unknown store.");
            }

        foreach (var recipe in document.Recipes)
        {
            if (!ReferenceExists(recipe.CategoryId, recipe.CategoryName, categoryIds, categoryNames))
                errors.Add($"Recipe '{recipe.Name}' references an unknown category.");
            foreach (var ingredient in recipe.Ingredients)
                if (!ReferenceExists(ingredient.IngredientId, ingredient.IngredientName, ingredientIds, ingredientNames))
                    errors.Add($"Recipe '{recipe.Name}' references an unknown ingredient.");
        }
    }

    private static void ValidateBackupReferences(
        BackupDocumentDto backup,
        CatalogDocumentDto catalog,
        IReadOnlyList<Recipe> localRecipes,
        IReadOnlyList<Ingredient> localIngredients,
        IReadOnlyList<Store> localStores,
        List<string> errors)
    {
        var recipeIds = IdSet(catalog.Recipes.Select(item => item.Id), localRecipes.Select(item => item.Id));
        var ingredientIds = IdSet(catalog.Ingredients.Select(item => item.Id), localIngredients.Select(item => item.Id));
        var storeIds = IdSet(catalog.Stores.Select(item => item.Id), localStores.Select(item => item.Id));
        foreach (var list in backup.ShoppingLists)
        {
            foreach (var source in list.Sources)
            {
                if (source.RecipeId is not null && !recipeIds.Contains(Guid.Parse(source.RecipeId))) errors.Add($"Shopping list '{list.Name}' references an unknown recipe.");
                foreach (var item in source.Contributions)
                    if (!ingredientIds.Contains(Guid.Parse(item.IngredientId))) errors.Add($"Shopping list '{list.Name}' references an unknown ingredient.");
            }
            foreach (var item in list.Items)
            {
                if (!ingredientIds.Contains(Guid.Parse(item.IngredientId))) errors.Add($"Shopping list '{list.Name}' references an unknown ingredient.");
                if (item.SelectedStoreId is not null && !storeIds.Contains(Guid.Parse(item.SelectedStoreId))) errors.Add($"Shopping list '{list.Name}' references an unknown store.");
            }
        }
    }

    private static void AnalyzeNamed<TDto, TLocal>(
        IEnumerable<TDto> incoming,
        IReadOnlyList<TLocal> local,
        Func<TDto, string?> incomingId,
        Func<TDto, string> incomingName,
        Func<TLocal, Guid> localId,
        Func<TLocal, string> localName,
        Func<TDto, TLocal, bool> conflict,
        string entityType,
        ref int additions,
        ref int merges,
        List<ImportConflict> conflicts) where TLocal : class
    {
        foreach (var item in incoming)
        {
            var match = Find(incomingId(item), incomingName(item), local, localId, localName);
            if (match is null) { additions++; continue; }
            if (conflict(item, match))
            {
                var key = ConflictKey(entityType, incomingId(item), incomingName(item));
                conflicts.Add(new ImportConflict(key, entityType, incomingName(item), "Local and imported values differ."));
            }
            else merges++;
        }
    }

    private static ImportDecision Decision(
        string type,
        string? id,
        string name,
        bool hasLocal,
        ImportMode mode,
        ImportPreview preview,
        IReadOnlyDictionary<string, ImportConflictResolution> resolutions,
        ImportCounters counters)
    {
        if (!hasLocal || mode == ImportMode.Replace) { counters.Added++; return ImportDecision.Add; }
        var key = ConflictKey(type, id, name);
        if (preview.Conflicts.Any(item => item.Key == key))
        {
            return resolutions[key] switch
            {
                ImportConflictResolution.KeepLocal => ImportDecision.KeepLocal,
                ImportConflictResolution.UseImported => Count(ImportDecision.UseImported, () => counters.Replaced++),
                ImportConflictResolution.KeepBoth => Count(ImportDecision.KeepBoth, () => counters.KeptBoth++),
                _ => throw new ArgumentOutOfRangeException(nameof(resolutions))
            };
        }
        counters.Merged++;
        return ImportDecision.Merge;
    }

    private static T Count<T>(T value, Action counter) { counter(); return value; }
    private static bool Different(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && !string.Equals(left, right, StringComparison.Ordinal);
    private static string ConflictKey(string type, string? id, string name) => $"{type}:{(Guid.TryParse(id, out var parsed) ? parsed.ToString("D") : TextNormalization.NameKey(name))}";
    private static T? Find<T>(string? id, string name, IEnumerable<T> local, Func<T, Guid> idSelector, Func<T, string> nameSelector) where T : class =>
        (Guid.TryParse(id, out var parsed) ? local.FirstOrDefault(item => idSelector(item) == parsed) : null) ??
        local.FirstOrDefault(item => TextNormalization.NameKey(nameSelector(item)) == TextNormalization.NameKey(name));

    private static void Replace<T>(List<T> values, T? oldValue, T newValue) where T : class
    {
        if (oldValue is not null) values.Remove(oldValue);
        values.Add(newValue);
    }

    private static Guid FinalId(string? imported, Guid? local, ImportDecision decision) =>
        decision switch
        {
            ImportDecision.KeepBoth => Guid.NewGuid(),
            ImportDecision.Merge or ImportDecision.UseImported or ImportDecision.KeepLocal when local.HasValue => local.Value,
            _ => ParsedOrNew(imported)
        };

    private static Guid ParsedOrNew(string? value, Guid? fallback = null) => Guid.TryParse(value, out var id) ? id : fallback ?? Guid.NewGuid();

    private static Guid Resolve(string? id, string? name, IdMap ids, Dictionary<string, Guid> names)
    {
        if (Guid.TryParse(id, out var parsed) && ids.TryGet(parsed, out var mapped)) return mapped;
        if (!string.IsNullOrWhiteSpace(name) && names.TryGetValue(TextNormalization.NameKey(name), out mapped)) return mapped;
        throw new InvalidDataException($"Unresolved imported reference '{id ?? name}'.");
    }

    private static Guid ResolveId(string id, IdMap ids) =>
        Guid.TryParse(id, out var parsed) && ids.TryGet(parsed, out var mapped) ? mapped : throw new InvalidDataException($"Unresolved imported reference '{id}'.");

    private static string UniqueName(string name, IEnumerable<string> existing)
    {
        var keys = existing.Select(TextNormalization.NameKey).ToHashSet(StringComparer.Ordinal);
        var candidate = name + " (uvezeno)";
        var index = 2;
        while (!keys.Add(TextNormalization.NameKey(candidate))) candidate = $"{name} (uvezeno {index++})";
        return candidate;
    }

    private static HashSet<Guid> IdSet(IEnumerable<string?> imported, IEnumerable<Guid> local) =>
        imported.Where(value => Guid.TryParse(value, out _)).Select(value => Guid.Parse(value!)).Concat(local).ToHashSet();
    private static HashSet<string> NameSet(IEnumerable<string> imported, IEnumerable<string> local) =>
        imported.Concat(local).Where(value => !string.IsNullOrWhiteSpace(value)).Select(TextNormalization.NameKey).ToHashSet(StringComparer.Ordinal);
    private static bool ReferenceExists(string? id, string? name, HashSet<Guid> ids, HashSet<string> names) =>
        (Guid.TryParse(id, out var parsed) && ids.Contains(parsed)) || (!string.IsNullOrWhiteSpace(name) && names.Contains(TextNormalization.NameKey(name)));

    private static string MediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".heic" or ".heif" => "image/heic",
        _ => "image/jpeg"
    };

    private static async Task<ParsedDocument> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var documentType = json.RootElement.EnumerateObject()
            .FirstOrDefault(property => property.Name.Equals("documentType", StringComparison.OrdinalIgnoreCase)).Value;
        if (documentType.ValueKind != JsonValueKind.String) throw new InvalidDataException("JSON has no documentType.");
        return documentType.GetString() switch
        {
            TransferSchema.CatalogDocumentType => new(
                json.RootElement.Deserialize<CatalogDocumentDto>(JsonOptions) ?? throw new InvalidDataException("Catalog JSON is empty."), null),
            TransferSchema.BackupDocumentType => FromBackup(
                json.RootElement.Deserialize<BackupDocumentDto>(JsonOptions) ?? throw new InvalidDataException("Backup JSON is empty.")),
            var type => throw new InvalidDataException($"Unsupported document type '{type}'.")
        };
    }

    private static ParsedDocument FromBackup(BackupDocumentDto backup) => new(backup.Catalog, backup);

    private sealed record ParsedDocument(CatalogDocumentDto Catalog, BackupDocumentDto? Backup);
    private sealed class ImportCounters { public int Added; public int Merged; public int Replaced; public int KeptBoth; }
    private enum ImportDecision { Add, Merge, KeepLocal, UseImported, KeepBoth }

    private sealed class IdMap
    {
        private readonly Dictionary<Guid, Guid> _values = [];
        public void Add(string? imported, Guid finalId) { if (Guid.TryParse(imported, out var id)) _values[id] = finalId; }
        public bool TryGet(Guid imported, out Guid finalId)
        {
            if (_values.TryGetValue(imported, out finalId)) return true;
            finalId = imported;
            return true;
        }
    }

    private sealed class ImportContext
    {
        public IdMap CategoryIds { get; } = new();
        public IdMap PackageTypeIds { get; } = new();
        public IdMap StoreIds { get; } = new();
        public IdMap IngredientIds { get; } = new();
        public IdMap PackageIds { get; } = new();
        public IdMap RecipeIds { get; } = new();
        public IdMap MealSlotIds { get; } = new();
        public Dictionary<string, Guid> CategoryNames { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Guid> PackageTypeNames { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Guid> StoreNames { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Guid> IngredientNames { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Guid> RecipeNames { get; } = new(StringComparer.Ordinal);
    }
}
