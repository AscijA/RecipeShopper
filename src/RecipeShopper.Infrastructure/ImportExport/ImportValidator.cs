using System.Globalization;
using RecipeShopper.Application.Models.ImportExport;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;
using RecipeShopper.Infrastructure.Files;
using RecipeShopper.Infrastructure.Persistence;

namespace RecipeShopper.Infrastructure.ImportExport;

internal static class ImportValidator
{
    public static IReadOnlyList<string> Validate(CatalogDocumentDto catalog, BackupDocumentDto? backup)
    {
        var errors = new List<string>();
        if (catalog.SchemaVersion != TransferSchema.CurrentVersion)
            errors.Add($"Unsupported catalog schema version {catalog.SchemaVersion}.");
        if (!string.Equals(catalog.DocumentType, TransferSchema.CatalogDocumentType, StringComparison.Ordinal))
            errors.Add("The embedded catalog document type is invalid.");

        ValidateNamed(catalog.Categories.Select(item => (item.Id, item.Name)), "category", errors);
        ValidateNamed(catalog.PackageTypes.Select(item => (item.Id, item.Name)), "package type", errors);
        ValidateNamed(catalog.Stores.Select(item => (item.Id, item.Name)), "store", errors);
        ValidateNamed(catalog.Ingredients.Select(item => (item.Id, item.Name)), "ingredient", errors);
        ValidateNamed(catalog.Recipes.Select(item => (item.Id, item.Name)), "recipe", errors, namesMustBeUnique: false);

        foreach (var ingredient in catalog.Ingredients)
        {
            if (!Enum.TryParse<MeasurementFamily>(ingredient.MeasurementFamily, true, out _))
                errors.Add($"Ingredient '{ingredient.Name}' has an invalid measurement family.");
            Try(() => _ = new UnitCode(ingredient.BaseUnit), $"Ingredient '{ingredient.Name}' has an invalid base unit.", errors);
            foreach (var package in ingredient.Packages)
            {
                ValidateId(package.Id, "package", errors);
                ValidateQuantity(package.NetQuantity, $"Package for '{ingredient.Name}'", errors);
                if (package.PackageTypeId is null && string.IsNullOrWhiteSpace(package.PackageTypeName))
                    errors.Add($"A package for '{ingredient.Name}' has no package type reference.");
                foreach (var offer in package.Offers)
                {
                    ValidateId(offer.Id, "offer", errors);
                    if (offer.StoreId is null && string.IsNullOrWhiteSpace(offer.StoreName))
                        errors.Add($"An offer for '{ingredient.Name}' has no store reference.");
                    ValidateMoney(offer.CurrentPrice, $"Offer for '{ingredient.Name}'", errors);
                    if (offer.PreviousPrice is not null) ValidateMoney(offer.PreviousPrice, $"Previous offer for '{ingredient.Name}'", errors);
                }
            }
        }

        foreach (var recipe in catalog.Recipes)
        {
            if (recipe.CategoryId is null && string.IsNullOrWhiteSpace(recipe.CategoryName))
                errors.Add($"Recipe '{recipe.Name}' has no category reference.");
            foreach (var item in recipe.Ingredients)
            {
                ValidateId(item.Id, "recipe ingredient", errors);
                if (item.IngredientId is null && string.IsNullOrWhiteSpace(item.IngredientName))
                    errors.Add($"Recipe '{recipe.Name}' contains an ingredient without a reference.");
                if (item.Quantity is not null) ValidateQuantity(item.Quantity, $"Recipe '{recipe.Name}'", errors);
            }
        }

        if (backup is null) return errors;
        if (backup.SchemaVersion != TransferSchema.CurrentVersion)
            errors.Add($"Unsupported backup schema version {backup.SchemaVersion}.");
        if (!string.Equals(backup.DocumentType, TransferSchema.BackupDocumentType, StringComparison.Ordinal))
            errors.Add("The backup document type is invalid.");

        foreach (var image in backup.Images)
        {
            try
            {
                if (Path.IsPathRooted(image.RelativePath) || image.RelativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
                    throw new InvalidDataException("unsafe path");
                var bytes = Convert.FromBase64String(image.Base64Data);
                if (!string.Equals(AppImageStore.Sha256(bytes), image.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("checksum mismatch");
            }
            catch (Exception exception) when (exception is FormatException or InvalidDataException)
            {
                errors.Add($"Image '{image.RelativePath}' is invalid: {exception.Message}.");
            }
        }

        foreach (var list in backup.ShoppingLists)
        {
            ValidateRequiredGuid(list.Id, "shopping list", errors);
            if (!Enum.TryParse<ShoppingListStatus>(list.Status, true, out _)) errors.Add($"Shopping list '{list.Name}' has an invalid status.");
        }
        foreach (var slot in backup.MealSlots) ValidateRequiredGuid(slot.Id, "meal slot", errors);
        foreach (var entry in backup.MealPlanEntries)
        {
            ValidateRequiredGuid(entry.Id, "meal plan entry", errors);
            if (!Enum.TryParse<AlternatingWeek>(entry.Week, true, out _) || !Enum.TryParse<DayOfWeek>(entry.DayOfWeek, true, out _))
                errors.Add($"Meal plan entry '{entry.Id}' has an invalid position.");
            if (!TryPositiveDecimal(entry.Multiplier)) errors.Add($"Meal plan entry '{entry.Id}' has an invalid multiplier.");
        }

        Try(() => _ = TransferMapper.FromDto(backup.Settings), "Backup settings are invalid.", errors);
        return errors;
    }

    private static void ValidateNamed(IEnumerable<(string? Id, string Name)> values, string type, List<string> errors, bool namesMustBeUnique = true)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<Guid>();
        foreach (var (id, name) in values)
        {
            if (string.IsNullOrWhiteSpace(name)) errors.Add($"An imported {type} has no name.");
            else if (namesMustBeUnique && !names.Add(TextNormalization.NameKey(name))) errors.Add($"Duplicate {type} name '{name}'.");
            if (id is not null && (!Guid.TryParse(id, out var parsed) || !ids.Add(parsed))) errors.Add($"Invalid or duplicate {type} id '{id}'.");
        }
    }

    private static void ValidateId(string? id, string type, List<string> errors)
    {
        if (id is not null && !Guid.TryParse(id, out _)) errors.Add($"Invalid {type} id '{id}'.");
    }

    private static void ValidateRequiredGuid(string id, string type, List<string> errors)
    {
        if (!Guid.TryParse(id, out _)) errors.Add($"Invalid {type} id '{id}'.");
    }

    private static void ValidateQuantity(QuantityDto value, string context, List<string> errors)
    {
        if (!TryPositiveDecimal(value.Amount) || string.IsNullOrWhiteSpace(value.Unit)) errors.Add($"{context} has an invalid quantity.");
    }

    private static bool TryPositiveDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0;

    private static void ValidateMoney(MoneyDto value, string context, List<string> errors) =>
        Try(() => _ = new Money(value.MinorUnits, value.Currency), $"{context} has invalid money.", errors);

    private static void Try(Action action, string error, List<string> errors)
    {
        try { action(); }
        catch { errors.Add(error); }
    }
}
