using RecipeShopper.Application.Abstractions.Persistence;
using RecipeShopper.Domain.Entities;
using RecipeShopper.Domain.Enums;
using SQLite;

namespace RecipeShopper.Infrastructure.Persistence;

public sealed class ShoppingListRepository(RecipeShopperDatabase database) : IShoppingListRepository
{
    public Task<IReadOnlyList<ShoppingList>> GetAllAsync(ShoppingListStatus? status = null, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<ShoppingList>>(connection =>
        {
            var rows = status is null
                ? connection.Query<ShoppingListRow>("SELECT * FROM shopping_lists ORDER BY IsCompleted, UpdatedUtc DESC, Id")
                : connection.Query<ShoppingListRow>(
                    "SELECT * FROM shopping_lists WHERE IsCompleted = ? ORDER BY UpdatedUtc DESC, Id",
                    status == ShoppingListStatus.Completed);
            return rows.Select(row => Load(connection, row)).ToArray();
        }, cancellationToken);

    public Task<ShoppingList?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.ReadAsync(connection =>
        {
            var row = connection.Find<ShoppingListRow>(RowMapper.Id(id));
            return row is null ? null : Load(connection, row);
        }, cancellationToken);

    public Task UpsertAsync(ShoppingList shoppingList, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shoppingList);
        ArgumentException.ThrowIfNullOrWhiteSpace(shoppingList.Name);

        return database.WriteAsync(connection =>
        {
            connection.InsertOrReplace(RowMapper.ToRow(shoppingList));
            connection.Execute(
                "DELETE FROM shopping_contributions WHERE ShoppingSourceId IN (SELECT Id FROM shopping_sources WHERE ShoppingListId = ?)",
                RowMapper.Id(shoppingList.Id));
            connection.Execute("DELETE FROM shopping_sources WHERE ShoppingListId = ?", RowMapper.Id(shoppingList.Id));
            connection.Execute(
                "DELETE FROM package_selections WHERE ShoppingListItemId IN (SELECT Id FROM shopping_item_states WHERE ShoppingListId = ?)",
                RowMapper.Id(shoppingList.Id));
            connection.Execute("DELETE FROM shopping_item_states WHERE ShoppingListId = ?", RowMapper.Id(shoppingList.Id));

            foreach (var source in shoppingList.Sources)
            {
                source.ShoppingListId = shoppingList.Id;
                connection.Insert(RowMapper.ToRow(source));
                foreach (var contribution in source.Contributions)
                {
                    contribution.ShoppingSourceId = source.Id;
                    connection.Insert(RowMapper.ToRow(contribution));
                }
            }

            foreach (var item in shoppingList.Items)
            {
                item.ShoppingListId = shoppingList.Id;
                connection.Insert(RowMapper.ToRow(item));
                foreach (var selection in item.PackageSelections)
                {
                    selection.ShoppingListItemId = item.Id;
                    connection.Insert(RowMapper.ToRow(selection));
                }
            }
        }, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.WriteAsync(connection => connection.Delete<ShoppingListRow>(RowMapper.Id(id)), cancellationToken);

    private static ShoppingList Load(SQLiteConnection connection, ShoppingListRow row)
    {
        var result = RowMapper.ToEntity(row);
        result.Sources = connection.Query<ShoppingSourceRow>(
                "SELECT * FROM shopping_sources WHERE ShoppingListId = ? ORDER BY CreatedUtc, Id", row.Id)
            .Select(sourceRow =>
            {
                var source = RowMapper.ToEntity(sourceRow);
                source.Contributions = connection.Query<ShoppingContributionRow>(
                        "SELECT * FROM shopping_contributions WHERE ShoppingSourceId = ? ORDER BY CreatedUtc, Id", sourceRow.Id)
                    .Select(RowMapper.ToEntity).ToList();
                return source;
            }).ToList();

        result.Items = connection.Query<ShoppingItemStateRow>(
                "SELECT * FROM shopping_item_states WHERE ShoppingListId = ? ORDER BY CreatedUtc, Id", row.Id)
            .Select(itemRow =>
            {
                var item = RowMapper.ToEntity(itemRow);
                item.PackageSelections = connection.Query<PackageSelectionRow>(
                        "SELECT * FROM package_selections WHERE ShoppingListItemId = ? ORDER BY CreatedUtc, Id", itemRow.Id)
                    .Select(RowMapper.ToEntity).ToList();
                return item;
            }).ToList();
        return result;
    }
}
