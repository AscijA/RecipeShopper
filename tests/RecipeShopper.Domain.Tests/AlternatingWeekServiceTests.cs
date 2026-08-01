using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Domain.Services;

namespace RecipeShopper.Domain.Tests;

public sealed class AlternatingWeekServiceTests
{
    private static readonly DateOnly ReferenceMonday = new(2026, 1, 5);

    [Theory]
    [InlineData(2026, 1, 5, AlternatingWeek.A)]
    [InlineData(2026, 1, 11, AlternatingWeek.A)]
    [InlineData(2026, 1, 12, AlternatingWeek.B)]
    [InlineData(2026, 1, 19, AlternatingWeek.A)]
    [InlineData(2025, 12, 29, AlternatingWeek.B)]
    [InlineData(2025, 12, 22, AlternatingWeek.A)]
    public void GetWeek_AlternatesInBothDirections(int year, int month, int day, AlternatingWeek expected)
    {
        Assert.Equal(expected, AlternatingWeekService.GetWeek(new DateOnly(year, month, day), ReferenceMonday));
    }

    [Fact]
    public void GetWeek_NonMondayReference_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            AlternatingWeekService.GetWeek(ReferenceMonday, ReferenceMonday.AddDays(1)));
    }

    [Theory]
    [InlineData(2026, 1, 5)]
    [InlineData(2026, 1, 7)]
    [InlineData(2026, 1, 11)]
    public void StartOfWeek_ReturnsMonday(int year, int month, int day)
    {
        Assert.Equal(ReferenceMonday, AlternatingWeekService.StartOfWeek(new DateOnly(year, month, day)));
    }

    [Fact]
    public void EntriesForDate_FiltersPatternAndDayAndOrders()
    {
        var slot = Guid.NewGuid();
        var correctSecond = Entry(AlternatingWeek.A, DayOfWeek.Wednesday, slot, 2);
        var wrongWeek = Entry(AlternatingWeek.B, DayOfWeek.Wednesday, slot, 0);
        var wrongDay = Entry(AlternatingWeek.A, DayOfWeek.Thursday, slot, 0);
        var correctFirst = Entry(AlternatingWeek.A, DayOfWeek.Wednesday, slot, 1);

        var result = AlternatingWeekService.EntriesForDate(
            new DateOnly(2026, 1, 7),
            ReferenceMonday,
            [correctSecond, wrongWeek, wrongDay, correctFirst]);

        Assert.Equal([correctFirst.Id, correctSecond.Id], result.Select(entry => entry.Id));
    }

    private static MealPlanEntry Entry(AlternatingWeek week, DayOfWeek day, Guid slot, int order) => new()
    {
        Week = week,
        DayOfWeek = day,
        MealSlotId = slot,
        RecipeId = Guid.NewGuid(),
        RecipeMultiplier = 1m,
        SortOrder = order
    };
}
