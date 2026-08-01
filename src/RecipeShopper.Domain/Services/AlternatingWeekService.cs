using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;

namespace RecipeShopper.Domain.Services;

public static class AlternatingWeekService
{
    public static AlternatingWeek GetWeek(DateOnly date, DateOnly weekAReferenceMonday)
    {
        if (weekAReferenceMonday.DayOfWeek != DayOfWeek.Monday)
        {
            throw new ArgumentException("The Week A reference date must be a Monday.", nameof(weekAReferenceMonday));
        }

        var monday = StartOfWeek(date);
        var weeks = (monday.DayNumber - weekAReferenceMonday.DayNumber) / 7;
        return PositiveModulo(weeks, 2) == 0 ? AlternatingWeek.A : AlternatingWeek.B;
    }

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    public static IReadOnlyList<MealPlanEntry> EntriesForDate(
        DateOnly date,
        DateOnly weekAReferenceMonday,
        IEnumerable<MealPlanEntry> entries) =>
        entries.Where(entry => entry.Week == GetWeek(date, weekAReferenceMonday) && entry.DayOfWeek == date.DayOfWeek)
            .OrderBy(entry => entry.SortOrder)
            .ToList();

    private static int PositiveModulo(int value, int divisor) => ((value % divisor) + divisor) % divisor;
}
