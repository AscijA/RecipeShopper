using RecipeShopper.Domain.Services;

namespace RecipeShopper.Domain.Tests;

public sealed class DefaultCatalogTests
{
    [Fact]
    public void CreateCategories_ReturnsEditableSeedCategoriesInStableOrder()
    {
        var categories = DefaultCatalog.CreateCategories();

        Assert.Equal(12, categories.Count);
        Assert.Equal("Govedina", categories[0].Name);
        Assert.Contains(categories, category => category.Name == "Ručak");
        Assert.Contains(categories, category => category.Name == "Večera");
        Assert.Equal("Ostalo", categories[^1].Name);
        Assert.Equal(Enumerable.Range(0, categories.Count), categories.Select(category => category.SortOrder));
        Assert.All(categories, category => Assert.False(string.IsNullOrWhiteSpace(category.IconKey)));
    }

    [Fact]
    public void CreateMealSlots_ReturnsFourIndependentEditableSlots()
    {
        var first = DefaultCatalog.CreateMealSlots();
        var second = DefaultCatalog.CreateMealSlots();

        Assert.Equal(["Doručak", "Ručak", "Večera", "Ostalo"], first.Select(slot => slot.Name));
        Assert.NotEqual(first[0].Id, second[0].Id);
    }
}
