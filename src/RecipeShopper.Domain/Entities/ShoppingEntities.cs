using RecipeShopper.Domain.Common;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Entities;

public sealed class ShoppingList : Entity
{
    public string Name { get; set; } = string.Empty;
    public ShoppingListStatus Status { get; set; } = ShoppingListStatus.Active;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public List<ShoppingSource> Sources { get; set; } = [];
    public List<ShoppingListItem> Items { get; set; } = [];
}

public sealed class ShoppingSource : Entity
{
    public Guid ShoppingListId { get; set; }
    public ShoppingSourceType Type { get; set; }
    public Guid? RecipeId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal Multiplier { get; set; } = 1m;
    public List<ShoppingContribution> Contributions { get; set; } = [];
}

public sealed class ShoppingContribution : Entity
{
    public Guid ShoppingSourceId { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientNameSnapshot { get; set; } = string.Empty;
    public Quantity? Quantity { get; set; }
    public string? Note { get; set; }
}

public sealed class ShoppingListItem : Entity
{
    public Guid ShoppingListId { get; set; }
    public Guid IngredientId { get; set; }
    public bool IsChecked { get; set; }
    public Guid? SelectedStoreId { get; set; }
    public string? Note { get; set; }
    public List<PackageSelection> PackageSelections { get; set; } = [];
}

public sealed class PackageSelection : Entity
{
    public Guid ShoppingListItemId { get; set; }
    public Guid PackageDefinitionId { get; set; }
    public Guid StoreId { get; set; }
    public int Count { get; set; } = 1;
    public Money PricePerPackageSnapshot { get; set; } = new(0, "BAM");
    public Quantity NetQuantitySnapshot { get; set; } = new(1, UnitCode.Piece);
}
