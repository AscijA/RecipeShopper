using RecipeShopper.Domain.Services;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Tests;

public sealed class ShoppingAggregationServiceTests
{
    [Fact]
    public void Aggregate_SharedIngredientInCompatibleUnits_SumsAndKeepsProvenance()
    {
        var ingredient = Guid.NewGuid();
        var result = ShoppingAggregationService.Aggregate(
        [
            Input(ingredient, "Brašno", 500, UnitCode.Gram, "Hljeb"),
            Input(ingredient, "Brašno", 1.5m, UnitCode.Kilogram, "Pita")
        ]);

        var item = Assert.Single(result);
        Assert.Equal(new Quantity(2, UnitCode.Kilogram), item.TotalQuantity);
        Assert.False(item.RequiresAmount);
        Assert.Equal(2, item.Sources.Count);
        Assert.Equal(["Hljeb", "Pita"], item.Sources.Select(source => source.SourceName));
    }

    [Fact]
    public void Aggregate_MissingAmount_MarksKnownAggregateIncomplete()
    {
        var ingredient = Guid.NewGuid();
        var source = Guid.NewGuid();
        var result = ShoppingAggregationService.Aggregate(
        [
            Input(ingredient, "So", 10, UnitCode.Gram, "Supa"),
            new ShoppingContributionInput(source, "Salata", ingredient, "So", null, "po ukusu")
        ]);

        var item = Assert.Single(result);
        Assert.True(item.RequiresAmount);
        Assert.Equal(new Quantity(10, UnitCode.Gram), item.TotalQuantity);
        Assert.Contains(item.Sources, entry => entry.Note == "po ukusu");
    }

    [Fact]
    public void Aggregate_AllMissing_ReturnsVisibleUnspecifiedLine()
    {
        var ingredient = Guid.NewGuid();
        var result = ShoppingAggregationService.Aggregate(
        [
            new ShoppingContributionInput(Guid.NewGuid(), "Supa", ingredient, "So", null)
        ]);

        var item = Assert.Single(result);
        Assert.Null(item.TotalQuantity);
        Assert.True(item.RequiresAmount);
        Assert.Equal("unspecified", item.QuantityBucket);
    }

    [Fact]
    public void Aggregate_UnmappedContainers_SumsOnlyExactUnit()
    {
        var ingredient = Guid.NewGuid();
        var result = ShoppingAggregationService.Aggregate(
        [
            Input(ingredient, "Pavlaka", 1, UnitCode.Box, "A"),
            Input(ingredient, "Pavlaka", 2, UnitCode.Box, "B"),
            Input(ingredient, "Pavlaka", 1, UnitCode.Package, "C")
        ]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.TotalQuantity == new Quantity(3, UnitCode.Box));
        Assert.Contains(result, item => item.TotalQuantity == new Quantity(1, UnitCode.Package));
    }

    [Fact]
    public void Aggregate_OrdersItemsByIngredientName()
    {
        var result = ShoppingAggregationService.Aggregate(
        [
            Input(Guid.NewGuid(), "Ulje", 1, UnitCode.Liter, "A"),
            Input(Guid.NewGuid(), "Brašno", 1, UnitCode.Kilogram, "A")
        ]);

        Assert.Equal(["Brašno", "Ulje"], result.Select(item => item.IngredientName));
    }

    private static ShoppingContributionInput Input(Guid ingredientId, string ingredientName, decimal amount, UnitCode unit, string source) =>
        new(Guid.NewGuid(), source, ingredientId, ingredientName, new Quantity(amount, unit));
}
