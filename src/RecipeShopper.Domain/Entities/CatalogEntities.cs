using RecipeShopper.Domain.Common;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Entities;

public sealed class RecipeCategory : Entity
{
    public string Name { get; set; } = string.Empty;
    public string IconKey { get; set; } = "category-other";
    public int SortOrder { get; set; }
}

public sealed class Recipe : Entity
{
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string? ImagePath { get; set; }
    public string? Notes { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

public sealed class RecipeIngredient : Entity
{
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public Quantity? Quantity { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Ingredient : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? IconKey { get; set; }
    public string? ImagePath { get; set; }
    public MeasurementFamily MeasurementFamily { get; set; }
    public UnitCode BaseUnit { get; set; } = UnitCode.Gram;
    public List<PackageDefinition> Packages { get; set; } = [];
}

public sealed class PackageType : Entity
{
    public string Name { get; set; } = string.Empty;
    public string UnitLabel { get; set; } = "pakovanje";
    public int SortOrder { get; set; }
}

public sealed class PackageDefinition : Entity
{
    public Guid IngredientId { get; set; }
    public Guid PackageTypeId { get; set; }
    public string? Label { get; set; }
    public Quantity NetQuantity { get; set; } = new(1, UnitCode.Piece);
    public List<StoreOffer> Offers { get; set; } = [];
}

public sealed class Store : Entity
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class StoreOffer : Entity
{
    public Guid PackageDefinitionId { get; set; }
    public Guid StoreId { get; set; }
    public Money CurrentPrice { get; set; } = new(0, "BAM");
    public Money? PreviousPrice { get; set; }
    public DateTimeOffset PriceUpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsAvailable { get; set; } = true;
}
