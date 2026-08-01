using System.Globalization;
using RecipeShopper.Application.Models.ImportExport;
using RecipeShopper.Domain.Common;
using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Infrastructure.ImportExport;

internal static class TransferMapper
{
    public static CategoryDto ToDto(RecipeCategory value, bool metadata) => new(value.Id.ToString("D"), value.Name, value.IconKey, value.SortOrder)
    {
        CreatedAtUtc = metadata ? value.CreatedAtUtc : null,
        UpdatedAtUtc = metadata ? value.UpdatedAtUtc : null,
        ArchivedAtUtc = metadata ? value.ArchivedAtUtc : null
    };

    public static PackageTypeDto ToDto(PackageType value, bool metadata) => new(value.Id.ToString("D"), value.Name, value.UnitLabel, value.SortOrder)
    {
        CreatedAtUtc = metadata ? value.CreatedAtUtc : null,
        UpdatedAtUtc = metadata ? value.UpdatedAtUtc : null,
        ArchivedAtUtc = metadata ? value.ArchivedAtUtc : null
    };

    public static StoreDto ToDto(Store value, bool metadata) => new(value.Id.ToString("D"), value.Name, value.IsEnabled, value.SortOrder)
    {
        CreatedAtUtc = metadata ? value.CreatedAtUtc : null,
        UpdatedAtUtc = metadata ? value.UpdatedAtUtc : null,
        ArchivedAtUtc = metadata ? value.ArchivedAtUtc : null
    };

    public static IngredientDto ToDto(Ingredient value, bool metadata) => new()
    {
        Id = value.Id.ToString("D"),
        Name = value.Name,
        IconKey = value.IconKey,
        MeasurementFamily = value.MeasurementFamily.ToString(),
        BaseUnit = value.BaseUnit.Value,
        Packages = value.Packages.Select(package => ToDto(package, metadata)).ToList(),
        CreatedAtUtc = metadata ? value.CreatedAtUtc : null,
        UpdatedAtUtc = metadata ? value.UpdatedAtUtc : null,
        ArchivedAtUtc = metadata ? value.ArchivedAtUtc : null
    };

    public static PackageDto ToDto(PackageDefinition value, bool metadata) => new()
    {
        Id = value.Id.ToString("D"),
        PackageTypeId = value.PackageTypeId.ToString("D"),
        Label = value.Label,
        NetQuantity = ToDto(value.NetQuantity),
        Offers = value.Offers.Select(offer => ToDto(offer, metadata)).ToList(),
        CreatedAtUtc = metadata ? value.CreatedAtUtc : null,
        UpdatedAtUtc = metadata ? value.UpdatedAtUtc : null,
        ArchivedAtUtc = metadata ? value.ArchivedAtUtc : null
    };

    public static OfferDto ToDto(StoreOffer value, bool metadata) => new()
    {
        Id = value.Id.ToString("D"),
        StoreId = value.StoreId.ToString("D"),
        CurrentPrice = ToDto(value.CurrentPrice),
        PreviousPrice = value.PreviousPrice is { } price ? ToDto(price) : null,
        PriceUpdatedAtUtc = value.PriceUpdatedAtUtc,
        IsAvailable = value.IsAvailable,
        CreatedAtUtc = metadata ? value.CreatedAtUtc : null,
        UpdatedAtUtc = metadata ? value.UpdatedAtUtc : null,
        ArchivedAtUtc = metadata ? value.ArchivedAtUtc : null
    };

    public static RecipeDto ToDto(Recipe value, bool metadata) => new()
    {
        Id = value.Id.ToString("D"),
        Name = value.Name,
        CategoryId = value.CategoryId.ToString("D"),
        ImagePath = metadata ? value.ImagePath : null,
        Notes = value.Notes,
        Ingredients = value.Ingredients.OrderBy(item => item.SortOrder).Select(item => ToDto(item, metadata)).ToList(),
        CreatedAtUtc = metadata ? value.CreatedAtUtc : null,
        UpdatedAtUtc = metadata ? value.UpdatedAtUtc : null,
        ArchivedAtUtc = metadata ? value.ArchivedAtUtc : null
    };

    public static RecipeIngredientDto ToDto(RecipeIngredient value, bool metadata) => new()
    {
        Id = value.Id.ToString("D"),
        IngredientId = value.IngredientId.ToString("D"),
        Quantity = value.Quantity is { } quantity ? ToDto(quantity) : null,
        Note = value.Note,
        SortOrder = value.SortOrder,
        CreatedAtUtc = metadata ? value.CreatedAtUtc : null,
        UpdatedAtUtc = metadata ? value.UpdatedAtUtc : null,
        ArchivedAtUtc = metadata ? value.ArchivedAtUtc : null
    };

    public static ShoppingListDto ToDto(ShoppingList value) => new()
    {
        Id = value.Id.ToString("D"),
        Name = value.Name,
        Status = value.Status.ToString(),
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
        ArchivedAtUtc = value.ArchivedAtUtc,
        CompletedAtUtc = value.CompletedAtUtc,
        Sources = value.Sources.Select(ToDto).ToList(),
        Items = value.Items.Select(ToDto).ToList()
    };

    private static ShoppingSourceDto ToDto(ShoppingSource value) => new()
    {
        Id = value.Id.ToString("D"),
        Type = value.Type.ToString(),
        RecipeId = value.RecipeId?.ToString("D"),
        DisplayName = value.DisplayName,
        Multiplier = Decimal(value.Multiplier),
        Contributions = value.Contributions.Select(ToDto).ToList(),
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
        ArchivedAtUtc = value.ArchivedAtUtc
    };

    private static ShoppingContributionDto ToDto(ShoppingContribution value) =>
        new(value.Id.ToString("D"), value.IngredientId.ToString("D"), value.IngredientNameSnapshot,
            value.Quantity is { } quantity ? ToDto(quantity) : null, value.Note)
        {
            CreatedAtUtc = value.CreatedAtUtc,
            UpdatedAtUtc = value.UpdatedAtUtc,
            ArchivedAtUtc = value.ArchivedAtUtc
        };

    private static ShoppingListItemDto ToDto(ShoppingListItem value) => new()
    {
        Id = value.Id.ToString("D"),
        IngredientId = value.IngredientId.ToString("D"),
        IsChecked = value.IsChecked,
        SelectedStoreId = value.SelectedStoreId?.ToString("D"),
        Note = value.Note,
        PackageSelections = value.PackageSelections.Select(ToDto).ToList(),
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
        ArchivedAtUtc = value.ArchivedAtUtc
    };

    private static PackageSelectionDto ToDto(PackageSelection value) =>
        new(value.Id.ToString("D"), value.PackageDefinitionId.ToString("D"), value.StoreId.ToString("D"), value.Count,
            ToDto(value.PricePerPackageSnapshot), ToDto(value.NetQuantitySnapshot))
        {
            CreatedAtUtc = value.CreatedAtUtc,
            UpdatedAtUtc = value.UpdatedAtUtc,
            ArchivedAtUtc = value.ArchivedAtUtc
        };

    public static MealSlotDto ToDto(MealSlot value) =>
        new(value.Id.ToString("D"), value.Name, value.SortOrder, value.ArchivedAtUtc)
        {
            CreatedAtUtc = value.CreatedAtUtc,
            UpdatedAtUtc = value.UpdatedAtUtc
        };

    public static MealPlanEntryDto ToDto(MealPlanEntry value) =>
        new(value.Id.ToString("D"), value.Week.ToString(), value.DayOfWeek.ToString(), value.MealSlotId.ToString("D"),
            value.RecipeId.ToString("D"), Decimal(value.RecipeMultiplier), value.SortOrder)
        {
            CreatedAtUtc = value.CreatedAtUtc,
            UpdatedAtUtc = value.UpdatedAtUtc,
            ArchivedAtUtc = value.ArchivedAtUtc
        };

    public static SettingsDto ToDto(AppSettings value) => new()
    {
        Id = value.Id.ToString("D"),
        Theme = value.Theme.ToString(),
        AccentKey = value.AccentKey,
        DefaultShoppingGrouping = value.DefaultShoppingGrouping.ToString(),
        CheckedItemBehavior = value.CheckedItemBehavior.ToString(),
        ConfirmDestructiveActions = value.ConfirmDestructiveActions,
        WeekAReferenceMonday = value.WeekAReferenceMonday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DailyReminderEnabled = value.DailyReminderEnabled,
        DailyReminderTime = value.DailyReminderTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        CurrencyCode = value.CurrencyCode,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
        ArchivedAtUtc = value.ArchivedAtUtc
    };

    public static RecipeCategory FromDto(CategoryDto value, Guid id, RecipeCategory? local = null) => Stamp(value, new RecipeCategory
    {
        Id = id,
        Name = value.Name.Trim(),
        IconKey = value.IconKey,
        SortOrder = value.SortOrder
    }, local);

    public static PackageType FromDto(PackageTypeDto value, Guid id, PackageType? local = null) => Stamp(value, new PackageType
    {
        Id = id,
        Name = value.Name.Trim(),
        UnitLabel = value.UnitLabel,
        SortOrder = value.SortOrder
    }, local);

    public static Store FromDto(StoreDto value, Guid id, Store? local = null) => Stamp(value, new Store
    {
        Id = id,
        Name = value.Name.Trim(),
        IsEnabled = value.IsEnabled,
        SortOrder = value.SortOrder
    }, local);

    public static Ingredient FromDto(IngredientDto value, Guid id, Ingredient? local = null) => Stamp(value, new Ingredient
    {
        Id = id,
        Name = value.Name.Trim(),
        IconKey = value.IconKey,
        MeasurementFamily = Enum.Parse<MeasurementFamily>(value.MeasurementFamily, true),
        BaseUnit = new UnitCode(value.BaseUnit)
    }, local);

    public static PackageDefinition FromDto(PackageDto value, Guid id, Guid ingredientId, Guid packageTypeId, PackageDefinition? local = null) =>
        Stamp(value, new PackageDefinition
        {
            Id = id,
            IngredientId = ingredientId,
            PackageTypeId = packageTypeId,
            Label = value.Label,
            NetQuantity = FromDto(value.NetQuantity)
        }, local);

    public static StoreOffer FromDto(OfferDto value, Guid id, Guid packageId, Guid storeId, StoreOffer? local = null) =>
        Stamp(value, new StoreOffer
        {
            Id = id,
            PackageDefinitionId = packageId,
            StoreId = storeId,
            CurrentPrice = FromDto(value.CurrentPrice),
            PreviousPrice = value.PreviousPrice is null ? null : FromDto(value.PreviousPrice),
            PriceUpdatedAtUtc = value.PriceUpdatedAtUtc,
            IsAvailable = value.IsAvailable
        }, local);

    public static Recipe FromDto(RecipeDto value, Guid id, Guid categoryId, Recipe? local = null) => Stamp(value, new Recipe
    {
        Id = id,
        Name = value.Name.Trim(),
        CategoryId = categoryId,
        ImagePath = value.ImagePath,
        Notes = value.Notes
    }, local);

    public static RecipeIngredient FromDto(RecipeIngredientDto value, Guid id, Guid recipeId, Guid ingredientId, RecipeIngredient? local = null) =>
        Stamp(value, new RecipeIngredient
        {
            Id = id,
            RecipeId = recipeId,
            IngredientId = ingredientId,
            Quantity = value.Quantity is null ? null : FromDto(value.Quantity),
            Note = value.Note,
            SortOrder = value.SortOrder
        }, local);

    public static ShoppingList FromDto(ShoppingListDto value, Guid id) => new()
    {
        Id = id,
        Name = value.Name.Trim(),
        Status = Enum.Parse<ShoppingListStatus>(value.Status, true),
        CompletedAtUtc = value.CompletedAtUtc,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc ?? value.CreatedAtUtc,
        ArchivedAtUtc = value.ArchivedAtUtc
    };

    public static AppSettings FromDto(SettingsDto value) => new()
    {
        Id = Guid.TryParse(value.Id, out var id) ? id : Guid.NewGuid(),
        Theme = Enum.Parse<AppTheme>(value.Theme, true),
        AccentKey = value.AccentKey,
        DefaultShoppingGrouping = Enum.Parse<ShoppingGroupingMode>(value.DefaultShoppingGrouping, true),
        CheckedItemBehavior = Enum.Parse<CheckedItemBehavior>(value.CheckedItemBehavior, true),
        ConfirmDestructiveActions = value.ConfirmDestructiveActions,
        WeekAReferenceMonday = DateOnly.ParseExact(value.WeekAReferenceMonday, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        DailyReminderEnabled = value.DailyReminderEnabled,
        DailyReminderTime = TimeOnly.Parse(value.DailyReminderTime, CultureInfo.InvariantCulture),
        CurrencyCode = value.CurrencyCode,
        CreatedAtUtc = value.CreatedAtUtc ?? DateTimeOffset.UtcNow,
        UpdatedAtUtc = value.UpdatedAtUtc ?? DateTimeOffset.UtcNow,
        ArchivedAtUtc = value.ArchivedAtUtc
    };

    public static Quantity FromDto(QuantityDto value) =>
        new(decimal.Parse(value.Amount, NumberStyles.Number, CultureInfo.InvariantCulture), new UnitCode(value.Unit));

    private static Money FromDto(MoneyDto value) => new(value.MinorUnits, value.Currency);
    private static QuantityDto ToDto(Quantity value) => new() { Amount = Decimal(value.Amount), Unit = value.Unit.Value };
    private static MoneyDto ToDto(Money value) => new() { MinorUnits = value.MinorUnits, Currency = value.Currency };
    private static string Decimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    private static T Stamp<T>(CategoryDto dto, T entity, T? local) where T : Entity => Stamp(entity, local, dto.CreatedAtUtc, dto.UpdatedAtUtc, dto.ArchivedAtUtc);
    private static T Stamp<T>(PackageTypeDto dto, T entity, T? local) where T : Entity => Stamp(entity, local, dto.CreatedAtUtc, dto.UpdatedAtUtc, dto.ArchivedAtUtc);
    private static T Stamp<T>(StoreDto dto, T entity, T? local) where T : Entity => Stamp(entity, local, dto.CreatedAtUtc, dto.UpdatedAtUtc, dto.ArchivedAtUtc);
    private static T Stamp<T>(IngredientDto dto, T entity, T? local) where T : Entity => Stamp(entity, local, dto.CreatedAtUtc, dto.UpdatedAtUtc, dto.ArchivedAtUtc);
    private static T Stamp<T>(PackageDto dto, T entity, T? local) where T : Entity => Stamp(entity, local, dto.CreatedAtUtc, dto.UpdatedAtUtc, dto.ArchivedAtUtc);
    private static T Stamp<T>(OfferDto dto, T entity, T? local) where T : Entity => Stamp(entity, local, dto.CreatedAtUtc, dto.UpdatedAtUtc, dto.ArchivedAtUtc);
    private static T Stamp<T>(RecipeDto dto, T entity, T? local) where T : Entity => Stamp(entity, local, dto.CreatedAtUtc, dto.UpdatedAtUtc, dto.ArchivedAtUtc);
    private static T Stamp<T>(RecipeIngredientDto dto, T entity, T? local) where T : Entity => Stamp(entity, local, dto.CreatedAtUtc, dto.UpdatedAtUtc, dto.ArchivedAtUtc);

    private static T Stamp<T>(T entity, T? local, DateTimeOffset? created, DateTimeOffset? updated, DateTimeOffset? archived) where T : Entity
    {
        var now = DateTimeOffset.UtcNow;
        entity.CreatedAtUtc = created ?? local?.CreatedAtUtc ?? now;
        entity.UpdatedAtUtc = updated ?? now;
        entity.ArchivedAtUtc = archived ?? local?.ArchivedAtUtc;
        return entity;
    }
}
