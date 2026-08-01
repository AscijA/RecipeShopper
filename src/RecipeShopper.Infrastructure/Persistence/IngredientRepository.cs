using RecipeShopper.Application.Abstractions.Persistence;
using RecipeShopper.Domain.Entities;
using SQLite;

namespace RecipeShopper.Infrastructure.Persistence;

public sealed class IngredientRepository(RecipeShopperDatabase database) : IIngredientRepository
{
    public Task<IReadOnlyList<Ingredient>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<Ingredient>>(connection =>
        {
            var sql = "SELECT * FROM ingredients" + (includeArchived ? string.Empty : " WHERE ArchivedUtc IS NULL") +
                      " ORDER BY Name COLLATE NOCASE, Id";
            return connection.Query<IngredientRow>(sql).Select(row => Load(connection, row)).ToArray();
        }, cancellationToken);

    public Task<Ingredient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.ReadAsync(connection =>
        {
            var row = connection.Find<IngredientRow>(RowMapper.Id(id));
            return row is null ? null : Load(connection, row);
        }, cancellationToken);

    public Task UpsertAsync(Ingredient ingredient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        ArgumentException.ThrowIfNullOrWhiteSpace(ingredient.Name);

        return database.WriteAsync(connection =>
        {
            connection.InsertOrReplace(RowMapper.ToRow(ingredient));
            connection.Execute(
                "DELETE FROM store_offers WHERE PackageDefinitionId IN (SELECT Id FROM package_definitions WHERE IngredientId = ?)",
                RowMapper.Id(ingredient.Id));
            connection.Execute("DELETE FROM package_definitions WHERE IngredientId = ?", RowMapper.Id(ingredient.Id));

            for (var index = 0; index < ingredient.Packages.Count; index++)
            {
                var package = ingredient.Packages[index];
                package.IngredientId = ingredient.Id;
                connection.Insert(RowMapper.ToRow(package, index));

                foreach (var offer in package.Offers)
                {
                    offer.PackageDefinitionId = package.Id;
                    connection.Insert(RowMapper.ToRow(offer));
                }
            }
        }, cancellationToken);
    }

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.WriteAsync(connection => RecipeRepository.Archive<IngredientRow>(connection, id), cancellationToken);

    private static Ingredient Load(SQLiteConnection connection, IngredientRow row)
    {
        var result = RowMapper.ToEntity(row);
        result.Packages = connection.Query<PackageDefinitionRow>(
                "SELECT * FROM package_definitions WHERE IngredientId = ? ORDER BY SortOrder, Id", row.Id)
            .Select(packageRow =>
            {
                var package = RowMapper.ToEntity(packageRow);
                package.Offers = connection.Query<StoreOfferRow>(
                        "SELECT * FROM store_offers WHERE PackageDefinitionId = ? ORDER BY StoreId, Currency", packageRow.Id)
                    .Select(RowMapper.ToEntity).ToList();
                return package;
            }).ToList();
        return result;
    }
}
