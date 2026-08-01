using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.Services;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Tests;

public sealed class ShoppingPricingServiceTests
{
    [Fact]
    public void Calculate_AllRemainingItemsCoveredAndPriced_IsComplete()
    {
        var store = Guid.NewGuid();
        var result = ShoppingPricingService.Calculate(
        [
            Item(new Quantity(2, UnitCode.Kilogram), Selection(store, 2, UnitCode.Kilogram, 1, 450)),
            Item(new Quantity(500, UnitCode.Milliliter), Selection(store, 1, UnitCode.Liter, 1, 300))
        ], "BAM");

        Assert.True(result.IsComplete);
        Assert.Equal(PriceCoverageStatus.Complete, result.Status);
        Assert.Equal(new Money(750, "BAM"), result.KnownSubtotal);
        Assert.Equal(0, result.MissingItemCount);
    }

    [Fact]
    public void Calculate_CheckedItem_IsExcludedFromCoverageAndSubtotal()
    {
        var uncheckedItem = Item(new Quantity(1, UnitCode.Kilogram), Selection(Guid.NewGuid(), 1, UnitCode.Kilogram, 1, 200));
        var checkedItem = Item(new Quantity(1, UnitCode.Liter), Selection(Guid.NewGuid(), 1, UnitCode.Liter, 1, 900)) with { IsChecked = true };

        var result = ShoppingPricingService.Calculate([uncheckedItem, checkedItem], "BAM");

        Assert.True(result.IsComplete);
        Assert.Equal(200, result.KnownSubtotal.MinorUnits);
    }

    [Fact]
    public void Calculate_InsufficientCoverage_IsIncompleteButIncludesKnownSubtotal()
    {
        var result = ShoppingPricingService.Calculate(
        [Item(new Quantity(2, UnitCode.Kilogram), Selection(Guid.NewGuid(), 1, UnitCode.Kilogram, 1, 250))],
        "BAM");

        Assert.False(result.IsComplete);
        Assert.Equal(1, result.MissingItemCount);
        Assert.Equal(250, result.KnownSubtotal.MinorUnits);
    }

    [Fact]
    public void Calculate_MissingRequiredAmount_IsIncomplete()
    {
        var item = new ShoppingPricingItem(
            Guid.NewGuid(),
            false,
            [null],
            [Selection(Guid.NewGuid(), 1, UnitCode.Package, 1, 100)]);

        var result = ShoppingPricingService.Calculate([item], "BAM");

        Assert.False(result.IsComplete);
        Assert.Equal(1, result.MissingItemCount);
        Assert.Equal(100, result.KnownSubtotal.MinorUnits);
    }

    [Fact]
    public void Calculate_MissingOrWrongCurrencyPrice_IsIncompleteAndExcludedFromSubtotal()
    {
        var store = Guid.NewGuid();
        var noPrice = Selection(store, 1, UnitCode.Kilogram, 1, null);
        var euroPrice = Selection(store, 1, UnitCode.Liter, 1, 100) with { PricePerPackage = new Money(100, "EUR") };

        var result = ShoppingPricingService.Calculate(
        [
            Item(new Quantity(1, UnitCode.Kilogram), noPrice),
            Item(new Quantity(1, UnitCode.Liter), euroPrice)
        ], "BAM");

        Assert.False(result.IsComplete);
        Assert.Equal(2, result.MissingItemCount);
        Assert.Equal(0, result.KnownSubtotal.MinorUnits);
    }

    [Fact]
    public void Calculate_SelectionsFromMultipleStores_IsIncomplete()
    {
        var item = new ShoppingPricingItem(
            Guid.NewGuid(),
            false,
            [new Quantity(2, UnitCode.Kilogram)],
            [
                Selection(Guid.NewGuid(), 1, UnitCode.Kilogram, 1, 100),
                Selection(Guid.NewGuid(), 1, UnitCode.Kilogram, 1, 100)
            ]);

        var result = ShoppingPricingService.Calculate([item], "BAM");

        Assert.False(result.IsComplete);
        Assert.Equal(1, result.MissingItemCount);
    }

    [Fact]
    public void Calculate_NoUncheckedItems_IsCompleteZeroTotal()
    {
        var result = ShoppingPricingService.Calculate([], "BAM");

        Assert.True(result.IsComplete);
        Assert.Equal(new Money(0, "BAM"), result.KnownSubtotal);
    }

    private static ShoppingPricingItem Item(Quantity required, PricedPackageSelection selection) =>
        new(Guid.NewGuid(), false, [required], [selection]);

    private static PricedPackageSelection Selection(Guid store, decimal amount, UnitCode unit, int count, long? price) =>
        new(Guid.NewGuid(), store, count, new Quantity(amount, unit), price.HasValue ? new Money(price.Value, "BAM") : null);
}
