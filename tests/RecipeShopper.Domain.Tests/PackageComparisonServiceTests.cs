using RecipeShopper.Domain.Services;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Tests;

public sealed class PackageComparisonServiceTests
{
    [Fact]
    public void Compare_LargerPackageCheaperThanTwoSmallPackages_RecommendsItAndReportsSavings()
    {
        var store = Guid.NewGuid();
        var oneKg = Guid.NewGuid();
        var twoKg = Guid.NewGuid();

        var result = PackageComparisonService.Compare(
            new Quantity(2, UnitCode.Kilogram),
            [
                Offer(oneKg, store, 1, UnitCode.Kilogram, 150),
                Offer(twoKg, store, 2, UnitCode.Kilogram, 250)
            ],
            "BAM");

        Assert.NotNull(result.Best);
        var selected = Assert.Single(result.Best.Packages);
        Assert.Equal(twoKg, selected.PackageDefinitionId);
        Assert.Equal(1, selected.Count);
        Assert.Equal(new Money(250, "BAM"), result.Best.TotalPrice);
        Assert.Equal(new Money(50, "BAM"), result.Best.SavingsComparedWithSmallestPackages);
    }

    [Fact]
    public void Compare_CombinationIsCheapest_ReturnsMixedPackageCounts()
    {
        var store = Guid.NewGuid();
        var oneKg = Guid.NewGuid();
        var twoKg = Guid.NewGuid();

        var result = PackageComparisonService.Compare(
            new Quantity(3, UnitCode.Kilogram),
            [
                Offer(oneKg, store, 1, UnitCode.Kilogram, 120),
                Offer(twoKg, store, 2, UnitCode.Kilogram, 200)
            ],
            "BAM");

        Assert.Equal(320, result.Best!.TotalPrice.MinorUnits);
        Assert.Contains(result.Best.Packages, package => package.PackageDefinitionId == oneKg && package.Count == 1);
        Assert.Contains(result.Best.Packages, package => package.PackageDefinitionId == twoKg && package.Count == 1);
        Assert.Equal(0, result.Best.ExcessInRequiredUnit);
    }

    [Fact]
    public void Compare_MultipleStores_KeepsEachRecommendationAndSelectsCheapestStore()
    {
        var cheapStore = Guid.NewGuid();
        var expensiveStore = Guid.NewGuid();

        var result = PackageComparisonService.Compare(
            new Quantity(1000, UnitCode.Gram),
            [
                Offer(Guid.NewGuid(), expensiveStore, 1, UnitCode.Kilogram, 300),
                Offer(Guid.NewGuid(), cheapStore, 1, UnitCode.Kilogram, 220)
            ],
            "BAM");

        Assert.Equal(2, result.ByStore.Count);
        Assert.Equal(cheapStore, result.Best!.StoreId);
        Assert.Equal(new Money(80, "BAM"), result.SavingsComparedWithNextStore);
    }

    [Fact]
    public void Compare_TieOnPrice_PrefersLessExcess()
    {
        var store = Guid.NewGuid();
        var exact = Guid.NewGuid();
        var excess = Guid.NewGuid();

        var result = PackageComparisonService.Compare(
            new Quantity(1, UnitCode.Kilogram),
            [
                Offer(excess, store, 1.5m, UnitCode.Kilogram, 200),
                Offer(exact, store, 1, UnitCode.Kilogram, 200)
            ],
            "BAM");

        Assert.Equal(exact, Assert.Single(result.Best!.Packages).PackageDefinitionId);
        Assert.Equal(0, result.Best.ExcessInRequiredUnit);
    }

    [Fact]
    public void Compare_FiltersUnavailableWrongCurrencyAndIncompatibleOffers()
    {
        var store = Guid.NewGuid();
        var unavailable = Offer(Guid.NewGuid(), store, 1, UnitCode.Kilogram, 50) with { IsAvailable = false };
        var wrongCurrency = Offer(Guid.NewGuid(), store, 1, UnitCode.Kilogram, 60) with { Price = new Money(60, "EUR") };
        var incompatible = Offer(Guid.NewGuid(), store, 1, UnitCode.Liter, 70);

        var result = PackageComparisonService.Compare(
            new Quantity(1, UnitCode.Kilogram),
            [unavailable, wrongCurrency, incompatible],
            "BAM");

        Assert.Null(result.Best);
        Assert.Empty(result.ByStore);
    }

    private static PackageOfferCandidate Offer(Guid packageId, Guid storeId, decimal amount, UnitCode unit, long price) =>
        new(packageId, storeId, new Quantity(amount, unit), new Money(price, "BAM"));
}
