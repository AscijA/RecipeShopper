using System.Globalization;
using RecipeShopper.Domain.Common;
using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Infrastructure.Persistence;

internal static class RowMapper
{
    public static RecipeCategoryRow ToRow(RecipeCategory value) => Stamp(value, new RecipeCategoryRow
    {
        Id = Id(value.Id),
        Name = value.Name.Trim(),
        NameKey = TextNormalization.NameKey(value.Name),
        IconKey = value.IconKey,
        SortOrder = value.SortOrder
    });

    public static RecipeCategory ToEntity(RecipeCategoryRow row) => Stamp(row, new RecipeCategory
    {
        Id = Guid.Parse(row.Id),
        Name = row.Name,
        IconKey = row.IconKey,
        SortOrder = row.SortOrder
    });

    public static RecipeRow ToRow(Recipe value) => Stamp(value, new RecipeRow
    {
        Id = Id(value.Id),
        CategoryId = Id(value.CategoryId),
        Name = value.Name.Trim(),
        ImagePath = value.ImagePath,
        Notes = value.Notes
    });

    public static Recipe ToEntity(RecipeRow row) => Stamp(row, new Recipe
    {
        Id = Guid.Parse(row.Id),
        CategoryId = Guid.Parse(row.CategoryId),
        Name = row.Name,
        ImagePath = row.ImagePath,
        Notes = row.Notes
    });

    public static RecipeIngredientRow ToRow(RecipeIngredient value) => Stamp(value, new RecipeIngredientRow
    {
        Id = Id(value.Id),
        RecipeId = Id(value.RecipeId),
        IngredientId = Id(value.IngredientId),
        Amount = value.Quantity is { } quantity ? Decimal(quantity.Amount) : null,
        Unit = value.Quantity?.Unit.Value ?? string.Empty,
        Note = value.Note,
        SortOrder = value.SortOrder
    });

    public static RecipeIngredient ToEntity(RecipeIngredientRow row) => Stamp(row, new RecipeIngredient
    {
        Id = Guid.Parse(row.Id),
        RecipeId = Guid.Parse(row.RecipeId),
        IngredientId = Guid.Parse(row.IngredientId),
        Quantity = Quantity(row.Amount, row.Unit),
        Note = row.Note,
        SortOrder = row.SortOrder
    });

    public static IngredientRow ToRow(Ingredient value) => Stamp(value, new IngredientRow
    {
        Id = Id(value.Id),
        Name = value.Name.Trim(),
        NameKey = TextNormalization.NameKey(value.Name),
        IconKey = value.IconKey,
        MeasurementFamily = value.MeasurementFamily.ToString(),
        BaseUnit = value.BaseUnit.Value
    });

    public static Ingredient ToEntity(IngredientRow row) => Stamp(row, new Ingredient
    {
        Id = Guid.Parse(row.Id),
        Name = row.Name,
        IconKey = row.IconKey,
        MeasurementFamily = Enum.Parse<MeasurementFamily>(row.MeasurementFamily),
        BaseUnit = new UnitCode(row.BaseUnit)
    });

    public static PackageTypeRow ToRow(PackageType value) => Stamp(value, new PackageTypeRow
    {
        Id = Id(value.Id),
        Name = value.Name.Trim(),
        NameKey = TextNormalization.NameKey(value.Name),
        UnitLabel = value.UnitLabel,
        SortOrder = value.SortOrder
    });

    public static PackageType ToEntity(PackageTypeRow row) => Stamp(row, new PackageType
    {
        Id = Guid.Parse(row.Id),
        Name = row.Name,
        UnitLabel = row.UnitLabel,
        SortOrder = row.SortOrder
    });

    public static StoreRow ToRow(Store value) => Stamp(value, new StoreRow
    {
        Id = Id(value.Id),
        Name = value.Name.Trim(),
        NameKey = TextNormalization.NameKey(value.Name),
        IsEnabled = value.IsEnabled,
        SortOrder = value.SortOrder
    });

    public static Store ToEntity(StoreRow row) => Stamp(row, new Store
    {
        Id = Guid.Parse(row.Id),
        Name = row.Name,
        IsEnabled = row.IsEnabled,
        SortOrder = row.SortOrder
    });

    public static PackageDefinitionRow ToRow(PackageDefinition value, int sortOrder) => Stamp(value, new PackageDefinitionRow
    {
        Id = Id(value.Id),
        IngredientId = Id(value.IngredientId),
        PackageTypeId = Id(value.PackageTypeId),
        Label = value.Label,
        Amount = Decimal(value.NetQuantity.Amount),
        Unit = value.NetQuantity.Unit.Value,
        SortOrder = sortOrder
    });

    public static PackageDefinition ToEntity(PackageDefinitionRow row) => Stamp(row, new PackageDefinition
    {
        Id = Guid.Parse(row.Id),
        IngredientId = Guid.Parse(row.IngredientId),
        PackageTypeId = Guid.Parse(row.PackageTypeId),
        Label = row.Label,
        NetQuantity = new Quantity(ParseDecimal(row.Amount), new UnitCode(row.Unit))
    });

    public static StoreOfferRow ToRow(StoreOffer value) => Stamp(value, new StoreOfferRow
    {
        Id = Id(value.Id),
        PackageDefinitionId = Id(value.PackageDefinitionId),
        StoreId = Id(value.StoreId),
        PriceMinor = value.CurrentPrice.MinorUnits,
        PreviousPriceMinor = value.PreviousPrice?.MinorUnits,
        Currency = value.CurrentPrice.Currency,
        IsAvailable = value.IsAvailable,
        PriceUpdatedUtc = Date(value.PriceUpdatedAtUtc)
    });

    public static StoreOffer ToEntity(StoreOfferRow row) => Stamp(row, new StoreOffer
    {
        Id = Guid.Parse(row.Id),
        PackageDefinitionId = Guid.Parse(row.PackageDefinitionId),
        StoreId = Guid.Parse(row.StoreId),
        CurrentPrice = new Money(row.PriceMinor, row.Currency),
        PreviousPrice = row.PreviousPriceMinor is { } previous ? new Money(previous, row.Currency) : null,
        PriceUpdatedAtUtc = ParseDate(row.PriceUpdatedUtc!),
        IsAvailable = row.IsAvailable
    });

    public static MealSlotRow ToRow(MealSlot value) => Stamp(value, new MealSlotRow
    {
        Id = Id(value.Id),
        Name = value.Name.Trim(),
        NameKey = TextNormalization.NameKey(value.Name),
        SortOrder = value.SortOrder
    });

    public static MealSlot ToEntity(MealSlotRow row) => Stamp(row, new MealSlot
    {
        Id = Guid.Parse(row.Id),
        Name = row.Name,
        SortOrder = row.SortOrder
    });

    public static MealPlanEntryRow ToRow(MealPlanEntry value) => Stamp(value, new MealPlanEntryRow
    {
        Id = Id(value.Id),
        WeekIndex = (int)value.Week,
        DayOfWeek = (int)value.DayOfWeek,
        MealSlotId = Id(value.MealSlotId),
        RecipeId = Id(value.RecipeId),
        Multiplier = Decimal(value.RecipeMultiplier),
        SortOrder = value.SortOrder
    });

    public static MealPlanEntry ToEntity(MealPlanEntryRow row) => Stamp(row, new MealPlanEntry
    {
        Id = Guid.Parse(row.Id),
        Week = (AlternatingWeek)row.WeekIndex,
        DayOfWeek = (DayOfWeek)row.DayOfWeek,
        MealSlotId = Guid.Parse(row.MealSlotId),
        RecipeId = Guid.Parse(row.RecipeId),
        RecipeMultiplier = ParseDecimal(row.Multiplier),
        SortOrder = row.SortOrder
    });

    public static ShoppingListRow ToRow(ShoppingList value) => Stamp(value, new ShoppingListRow
    {
        Id = Id(value.Id),
        Name = value.Name.Trim(),
        IsCompleted = value.Status == ShoppingListStatus.Completed,
        CompletedUtc = value.CompletedAtUtc is { } completed ? Date(completed) : null
    });

    public static ShoppingList ToEntity(ShoppingListRow row) => Stamp(row, new ShoppingList
    {
        Id = Guid.Parse(row.Id),
        Name = row.Name,
        Status = row.IsCompleted ? ShoppingListStatus.Completed : ShoppingListStatus.Active,
        CompletedAtUtc = row.CompletedUtc is null ? null : ParseDate(row.CompletedUtc)
    });

    public static ShoppingSourceRow ToRow(ShoppingSource value) => Stamp(value, new ShoppingSourceRow
    {
        Id = Id(value.Id),
        ShoppingListId = Id(value.ShoppingListId),
        Kind = value.Type.ToString(),
        RecipeId = value.RecipeId is { } recipeId ? Id(recipeId) : null,
        Label = value.DisplayName,
        Multiplier = Decimal(value.Multiplier)
    });

    public static ShoppingSource ToEntity(ShoppingSourceRow row) => Stamp(row, new ShoppingSource
    {
        Id = Guid.Parse(row.Id),
        ShoppingListId = Guid.Parse(row.ShoppingListId),
        Type = Enum.Parse<ShoppingSourceType>(row.Kind),
        RecipeId = row.RecipeId is null ? null : Guid.Parse(row.RecipeId),
        DisplayName = row.Label ?? string.Empty,
        Multiplier = ParseDecimal(row.Multiplier)
    });

    public static ShoppingContributionRow ToRow(ShoppingContribution value) => Stamp(value, new ShoppingContributionRow
    {
        Id = Id(value.Id),
        ShoppingSourceId = Id(value.ShoppingSourceId),
        IngredientId = Id(value.IngredientId),
        IngredientNameSnapshot = value.IngredientNameSnapshot,
        Amount = value.Quantity is { } quantity ? Decimal(quantity.Amount) : null,
        Unit = value.Quantity?.Unit.Value ?? string.Empty,
        Note = value.Note
    });

    public static ShoppingContribution ToEntity(ShoppingContributionRow row) => Stamp(row, new ShoppingContribution
    {
        Id = Guid.Parse(row.Id),
        ShoppingSourceId = Guid.Parse(row.ShoppingSourceId),
        IngredientId = Guid.Parse(row.IngredientId),
        IngredientNameSnapshot = row.IngredientNameSnapshot,
        Quantity = Quantity(row.Amount, row.Unit),
        Note = row.Note
    });

    public static ShoppingItemStateRow ToRow(ShoppingListItem value) => new()
    {
        Id = Id(value.Id),
        ShoppingListId = Id(value.ShoppingListId),
        IngredientId = Id(value.IngredientId),
        IsChecked = value.IsChecked,
        SelectedStoreId = value.SelectedStoreId is { } id ? Id(id) : null,
        Note = value.Note,
        CreatedUtc = Date(value.CreatedAtUtc),
        UpdatedUtc = Date(value.UpdatedAtUtc),
        ArchivedUtc = value.ArchivedAtUtc is { } archived ? Date(archived) : null
    };

    public static ShoppingListItem ToEntity(ShoppingItemStateRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        ShoppingListId = Guid.Parse(row.ShoppingListId),
        IngredientId = Guid.Parse(row.IngredientId),
        IsChecked = row.IsChecked,
        SelectedStoreId = row.SelectedStoreId is null ? null : Guid.Parse(row.SelectedStoreId),
        Note = row.Note,
        CreatedAtUtc = ParseDate(row.CreatedUtc),
        UpdatedAtUtc = ParseDate(row.UpdatedUtc),
        ArchivedAtUtc = row.ArchivedUtc is null ? null : ParseDate(row.ArchivedUtc)
    };

    public static PackageSelectionRow ToRow(PackageSelection value) => Stamp(value, new PackageSelectionRow
    {
        Id = Id(value.Id),
        ShoppingListItemId = Id(value.ShoppingListItemId),
        PackageDefinitionId = Id(value.PackageDefinitionId),
        StoreId = Id(value.StoreId),
        Quantity = value.Count,
        PriceMinorSnapshot = value.PricePerPackageSnapshot.MinorUnits,
        CurrencySnapshot = value.PricePerPackageSnapshot.Currency,
        NetAmountSnapshot = Decimal(value.NetQuantitySnapshot.Amount),
        NetUnitSnapshot = value.NetQuantitySnapshot.Unit.Value
    });

    public static PackageSelection ToEntity(PackageSelectionRow row) => Stamp(row, new PackageSelection
    {
        Id = Guid.Parse(row.Id),
        ShoppingListItemId = Guid.Parse(row.ShoppingListItemId),
        PackageDefinitionId = Guid.Parse(row.PackageDefinitionId),
        StoreId = Guid.Parse(row.StoreId),
        Count = row.Quantity,
        PricePerPackageSnapshot = new Money(row.PriceMinorSnapshot, row.CurrencySnapshot),
        NetQuantitySnapshot = new Quantity(ParseDecimal(row.NetAmountSnapshot), new UnitCode(row.NetUnitSnapshot))
    });

    public static string Id(Guid value) => value.ToString("D");
    public static string Decimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    public static decimal ParseDecimal(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
    public static string Date(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    public static DateTimeOffset ParseDate(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static Quantity? Quantity(string? amount, string unit) =>
        amount is null ? null : new Quantity(ParseDecimal(amount), new UnitCode(unit));

    private static TRow Stamp<TRow>(Entity value, TRow row) where TRow : EntityRow
    {
        row.CreatedUtc = Date(value.CreatedAtUtc);
        row.UpdatedUtc = Date(value.UpdatedAtUtc);
        row.ArchivedUtc = value.ArchivedAtUtc is { } archived ? Date(archived) : null;
        return row;
    }

    private static TEntity Stamp<TEntity>(EntityRow row, TEntity value) where TEntity : Entity
    {
        value.CreatedAtUtc = ParseDate(row.CreatedUtc);
        value.UpdatedAtUtc = ParseDate(row.UpdatedUtc);
        value.ArchivedAtUtc = row.ArchivedUtc is null ? null : ParseDate(row.ArchivedUtc);
        return value;
    }
}
