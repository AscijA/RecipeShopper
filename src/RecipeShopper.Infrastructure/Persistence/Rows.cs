using SQLite;

namespace RecipeShopper.Infrastructure.Persistence;

internal abstract class EntityRow
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
    public string? ArchivedUtc { get; set; }
}

[Table("recipe_categories")]
internal sealed class RecipeCategoryRow : EntityRow
{
    [Indexed(Unique = true)]
    public string NameKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

[Table("ingredients")]
internal sealed class IngredientRow : EntityRow
{
    [Indexed(Unique = true)]
    public string NameKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? IconKey { get; set; }
    public string MeasurementFamily { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
}

[Table("recipes")]
internal sealed class RecipeRow : EntityRow
{
    [Indexed]
    public string CategoryId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? Notes { get; set; }
}

[Table("recipe_ingredients")]
internal sealed class RecipeIngredientRow : EntityRow
{
    [Indexed]
    public string RecipeId { get; set; } = string.Empty;

    [Indexed]
    public string IngredientId { get; set; } = string.Empty;

    public string? Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

[Table("package_types")]
internal sealed class PackageTypeRow : EntityRow
{
    [Indexed(Unique = true)]
    public string NameKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string UnitLabel { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

[Table("stores")]
internal sealed class StoreRow : EntityRow
{
    [Indexed(Unique = true)]
    public string NameKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
}

[Table("package_definitions")]
internal sealed class PackageDefinitionRow : EntityRow
{
    [Indexed]
    public string IngredientId { get; set; } = string.Empty;

    [Indexed]
    public string PackageTypeId { get; set; } = string.Empty;

    public string? Label { get; set; }
    public string Amount { get; set; } = "1";
    public string Unit { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

[Table("store_offers")]
internal sealed class StoreOfferRow : EntityRow
{
    [Indexed]
    public string PackageDefinitionId { get; set; } = string.Empty;

    [Indexed]
    public string StoreId { get; set; } = string.Empty;

    public long PriceMinor { get; set; }
    public long? PreviousPriceMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? PriceUpdatedUtc { get; set; }
}

[Table("meal_slots")]
internal sealed class MealSlotRow : EntityRow
{
    [Indexed(Unique = true)]
    public string NameKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

[Table("meal_plan_entries")]
internal sealed class MealPlanEntryRow : EntityRow
{
    public int WeekIndex { get; set; }
    public int DayOfWeek { get; set; }

    [Indexed]
    public string MealSlotId { get; set; } = string.Empty;

    [Indexed]
    public string RecipeId { get; set; } = string.Empty;

    public string Multiplier { get; set; } = "1";
    public int SortOrder { get; set; }
}

[Table("shopping_lists")]
internal sealed class ShoppingListRow : EntityRow
{
    public string Name { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? CompletedUtc { get; set; }
}

[Table("shopping_sources")]
internal sealed class ShoppingSourceRow : EntityRow
{
    [Indexed]
    public string ShoppingListId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
    public string? RecipeId { get; set; }
    public string? Label { get; set; }
    public string Multiplier { get; set; } = "1";
}

[Table("shopping_contributions")]
internal sealed class ShoppingContributionRow : EntityRow
{
    [Indexed]
    public string ShoppingSourceId { get; set; } = string.Empty;

    [Indexed]
    public string IngredientId { get; set; } = string.Empty;

    public string? Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string IngredientNameSnapshot { get; set; } = string.Empty;
}

[Table("shopping_item_states")]
internal sealed class ShoppingItemStateRow
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string ShoppingListId { get; set; } = string.Empty;

    [Indexed]
    public string IngredientId { get; set; } = string.Empty;

    public bool IsChecked { get; set; }
    public string? SelectedStoreId { get; set; }
    public string? Note { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
    public string? ArchivedUtc { get; set; }
}

[Table("package_selections")]
internal sealed class PackageSelectionRow : EntityRow
{
    [Indexed]
    public string ShoppingListItemId { get; set; } = string.Empty;

    [Indexed]
    public string PackageDefinitionId { get; set; } = string.Empty;

    [Indexed]
    public string StoreId { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public long PriceMinorSnapshot { get; set; }
    public string CurrencySnapshot { get; set; } = string.Empty;
    public string NetAmountSnapshot { get; set; } = "1";
    public string NetUnitSnapshot { get; set; } = string.Empty;
}

[Table("app_settings")]
internal sealed class AppSettingRow
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string JsonValue { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}
