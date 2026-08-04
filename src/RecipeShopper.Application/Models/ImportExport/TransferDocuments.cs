namespace RecipeShopper.Application.Models.ImportExport;

public static class TransferSchema
{
    public const int CurrentVersion = 1;
    public const string CatalogDocumentType = "recipe-shopper-catalog";
    public const string BackupDocumentType = "recipe-shopper-backup";
}

public sealed record CatalogDocumentDto
{
    public int SchemaVersion { get; init; } = TransferSchema.CurrentVersion;
    public string DocumentType { get; init; } = TransferSchema.CatalogDocumentType;
    public List<CategoryDto> Categories { get; init; } = [];
    public List<IngredientDto> Ingredients { get; init; } = [];
    public List<PackageTypeDto> PackageTypes { get; init; } = [];
    public List<StoreDto> Stores { get; init; } = [];
    public List<RecipeDto> Recipes { get; init; } = [];
}

public sealed record BackupDocumentDto
{
    public int SchemaVersion { get; init; } = TransferSchema.CurrentVersion;
    public string DocumentType { get; init; } = TransferSchema.BackupDocumentType;
    public DateTimeOffset ExportedAtUtc { get; init; }
    public CatalogDocumentDto Catalog { get; init; } = new();
    public List<ShoppingListDto> ShoppingLists { get; init; } = [];
    public List<MealSlotDto> MealSlots { get; init; } = [];
    public List<MealPlanEntryDto> MealPlanEntries { get; init; } = [];
    public SettingsDto Settings { get; init; } = new();
    public List<ImageAssetDto> Images { get; init; } = [];
}

public sealed record CategoryDto(string? Id, string Name, string IconKey, int SortOrder)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record PackageTypeDto(string? Id, string Name, string UnitLabel, int SortOrder)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record StoreDto(string? Id, string Name, bool IsEnabled, int SortOrder)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record IngredientDto
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? IconKey { get; init; }
    public string? ImagePath { get; init; }
    public string MeasurementFamily { get; init; } = string.Empty;
    public string BaseUnit { get; init; } = string.Empty;
    public List<PackageDto> Packages { get; init; } = [];
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record PackageDto
{
    public string? Id { get; init; }
    public string? PackageTypeId { get; init; }
    public string? PackageTypeName { get; init; }
    public string? Label { get; init; }
    public QuantityDto NetQuantity { get; init; } = new();
    public List<OfferDto> Offers { get; init; } = [];
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record OfferDto
{
    public string? Id { get; init; }
    public string? StoreId { get; init; }
    public string? StoreName { get; init; }
    public MoneyDto CurrentPrice { get; init; } = new();
    public MoneyDto? PreviousPrice { get; init; }
    public DateTimeOffset PriceUpdatedAtUtc { get; init; }
    public bool IsAvailable { get; init; } = true;
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record RecipeDto
{
    public string? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? ImagePath { get; init; }
    public string? Notes { get; init; }
    public List<RecipeIngredientDto> Ingredients { get; init; } = [];
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record RecipeIngredientDto
{
    public string? Id { get; init; }
    public string? IngredientId { get; init; }
    public string? IngredientName { get; init; }
    public QuantityDto? Quantity { get; init; }
    public string? Note { get; init; }
    public int SortOrder { get; init; }
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record QuantityDto
{
    public string Amount { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
}

public sealed record MoneyDto
{
    public long MinorUnits { get; init; }
    public string Currency { get; init; } = "BAM";
}

public sealed record ShoppingListDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public List<ShoppingSourceDto> Sources { get; init; } = [];
    public List<ShoppingListItemDto> Items { get; init; } = [];
}

public sealed record ShoppingSourceDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? RecipeId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Multiplier { get; init; } = "1";
    public List<ShoppingContributionDto> Contributions { get; init; } = [];
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record ShoppingContributionDto(string Id, string IngredientId, string IngredientName, QuantityDto? Quantity, string? Note)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record ShoppingListItemDto
{
    public string Id { get; init; } = string.Empty;
    public string IngredientId { get; init; } = string.Empty;
    public bool IsChecked { get; init; }
    public string? SelectedStoreId { get; init; }
    public string? Note { get; init; }
    public List<PackageSelectionDto> PackageSelections { get; init; } = [];
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record PackageSelectionDto(string Id, string PackageDefinitionId, string StoreId, int Count, MoneyDto PricePerPackage, QuantityDto NetQuantity)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record MealSlotDto(string Id, string Name, int SortOrder, DateTimeOffset? ArchivedAtUtc)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
}

public sealed record MealPlanEntryDto(string Id, string Week, string DayOfWeek, string MealSlotId, string RecipeId, string Multiplier, int SortOrder)
{
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record SettingsDto
{
    public string? Id { get; init; }
    public string Theme { get; init; } = "System";
    public string AccentKey { get; init; } = "green";
    public string DefaultShoppingGrouping { get; init; } = "None";
    public string CheckedItemBehavior { get; init; } = "MoveToBottom";
    public bool ConfirmDestructiveActions { get; init; } = true;
    public string WeekAReferenceMonday { get; init; } = string.Empty;
    public bool DailyReminderEnabled { get; init; }
    public string DailyReminderTime { get; init; } = "09:00";
    public string CurrencyCode { get; init; } = "BAM";
    public DateTimeOffset? CreatedAtUtc { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
}

public sealed record ImageAssetDto(string RelativePath, string MediaType, string Sha256, string Base64Data);
