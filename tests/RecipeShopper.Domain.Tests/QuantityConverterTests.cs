using RecipeShopper.Domain.Services;
using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Tests;

public sealed class QuantityConverterTests
{
    [Theory]
    [InlineData(1.5, "kg", "g", 1500)]
    [InlineData(2, "l", "ml", 2000)]
    [InlineData(2, "tsp", "ml", 10)]
    [InlineData(2, "tbsp", "ml", 30)]
    [InlineData(2, "cup", "ml", 480)]
    public void Convert_CompatibleUnits_ReturnsExactValue(double amount, string source, string target, double expected)
    {
        var result = QuantityConverter.Convert(new Quantity((decimal)amount, new UnitCode(source)), new UnitCode(target));

        Assert.Equal((decimal)expected, result.Amount);
        Assert.Equal(new UnitCode(target), result.Unit);
    }

    [Fact]
    public void Convert_IncompatibleFamilies_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            QuantityConverter.Convert(new Quantity(1, UnitCode.Kilogram), UnitCode.Liter));
    }

    [Fact]
    public void Convert_DifferentContainerLabels_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            QuantityConverter.Convert(new Quantity(1, UnitCode.Box), UnitCode.Package));
    }

    [Fact]
    public void Add_SameCustomUnit_SumsExactly()
    {
        var result = QuantityConverter.Add(new Quantity(2, new UnitCode("snop")), new Quantity(3, new UnitCode("snop")));

        Assert.Equal(new Quantity(5, new UnitCode("snop")), result);
    }

    [Theory]
    [InlineData(999, "g", 999, "g")]
    [InlineData(1000, "g", 1, "kg")]
    [InlineData(2500, "ml", 2.5, "l")]
    public void ToFriendlyDisplay_UsesLargeUnitAtThreshold(double amount, string unit, double expected, string expectedUnit)
    {
        var result = QuantityConverter.ToFriendlyDisplay(new Quantity((decimal)amount, new UnitCode(unit)));

        Assert.Equal((decimal)expected, result.Amount);
        Assert.Equal(new UnitCode(expectedUnit), result.Unit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_NonPositiveAmount_Throws(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity((decimal)value, UnitCode.Gram));
    }

    [Fact]
    public void UnitCode_NormalizesWhitespaceAndCase()
    {
        Assert.Equal(UnitCode.Kilogram, new UnitCode(" KG "));
    }
}
