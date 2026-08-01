using RecipeShopper.Application.Abstractions.Persistence;
using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;

namespace RecipeShopper.Infrastructure.Persistence;

public sealed class MealPlanRepository(RecipeShopperDatabase database) : IMealPlanRepository
{
    public Task<IReadOnlyList<MealSlot>> GetSlotsAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<MealSlot>>(connection =>
        {
            var sql = "SELECT * FROM meal_slots" + (includeArchived ? string.Empty : " WHERE ArchivedUtc IS NULL") +
                      " ORDER BY SortOrder, Name COLLATE NOCASE";
            return connection.Query<MealSlotRow>(sql).Select(RowMapper.ToEntity).ToArray();
        }, cancellationToken);

    public Task UpsertSlotAsync(MealSlot slot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot.Name);
        return database.WriteAsync(connection => connection.InsertOrReplace(RowMapper.ToRow(slot)), cancellationToken);
    }

    public Task<IReadOnlyList<MealPlanEntry>> GetEntriesAsync(AlternatingWeek? week = null, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<MealPlanEntry>>(connection =>
        {
            var rows = week is null
                ? connection.Query<MealPlanEntryRow>("SELECT * FROM meal_plan_entries ORDER BY WeekIndex, DayOfWeek, SortOrder, Id")
                : connection.Query<MealPlanEntryRow>(
                    "SELECT * FROM meal_plan_entries WHERE WeekIndex = ? ORDER BY DayOfWeek, SortOrder, Id", (int)week.Value);
            return rows.Select(RowMapper.ToEntity).ToArray();
        }, cancellationToken);

    public Task UpsertEntryAsync(MealPlanEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.RecipeMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(entry), "Recipe multiplier must be positive.");
        return database.WriteAsync(connection => connection.InsertOrReplace(RowMapper.ToRow(entry)), cancellationToken);
    }

    public Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.WriteAsync(connection => connection.Delete<MealPlanEntryRow>(RowMapper.Id(id)), cancellationToken);
}
