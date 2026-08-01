using RecipeShopper.Domain.ValueObjects;

namespace RecipeShopper.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_NormalizesCurrencyAndExposesMajorUnits()
    {
        var money = new Money(1234, "bam");

        Assert.Equal("BAM", money.Currency);
        Assert.Equal(12.34m, money.MajorUnits);
    }

    [Fact]
    public void Add_DifferentCurrencies_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Money(100, "BAM").Add(new Money(100, "EUR")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("BA")]
    [InlineData("12A")]
    [InlineData("EURO")]
    public void Constructor_InvalidCurrency_Throws(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(100, currency));
    }
}
