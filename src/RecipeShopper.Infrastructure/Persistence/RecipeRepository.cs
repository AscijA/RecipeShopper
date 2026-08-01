using RecipeShopper.Application.Abstractions.Persistence;
using RecipeShopper.Domain.Entities;
using SQLite;

namespace RecipeShopper.Infrastructure.Persistence;

public sealed class RecipeRepository(RecipeShopperDatabase database) : IRecipeRepository
{
    public Task<IReadOnlyList<Recipe>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<Recipe>>(connection =>
        {
            var sql = "SELECT * FROM recipes" + (includeArchived ? string.Empty : " WHERE ArchivedUtc IS NULL") +
                      " ORDER BY Name COLLATE NOCASE, Id";
            return connection.Query<RecipeRow>(sql).Select(row => Load(connection, row)).ToArray();
        }, cancellationToken);

    public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.ReadAsync(connection =>
        {
            var row = connection.Find<RecipeRow>(RowMapper.Id(id));
            return row is null ? null : Load(connection, row);
        }, cancellationToken);

    public Task UpsertAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipe.Name);
        if (recipe.CategoryId == Guid.Empty) throw new ArgumentException("A recipe category is required.", nameof(recipe));

        return database.WriteAsync(connection =>
        {
            connection.InsertOrReplace(RowMapper.ToRow(recipe));
            connection.Execute("DELETE FROM recipe_ingredients WHERE RecipeId = ?", RowMapper.Id(recipe.Id));

            foreach (var ingredient in recipe.Ingredients.OrderBy(value => value.SortOrder))
            {
                ingredient.RecipeId = recipe.Id;
                connection.Insert(RowMapper.ToRow(ingredient));
            }
        }, cancellationToken);
    }

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.WriteAsync(connection => Archive<RecipeRow>(connection, id), cancellationToken);

    private static Recipe Load(SQLiteConnection connection, RecipeRow row)
    {
        var result = RowMapper.ToEntity(row);
        result.Ingredients = connection.Query<RecipeIngredientRow>(
                "SELECT * FROM recipe_ingredients WHERE RecipeId = ? ORDER BY SortOrder, Id", row.Id)
            .Select(RowMapper.ToEntity).ToList();
        return result;
    }

    internal static void Archive<TRow>(SQLiteConnection connection, Guid id) where TRow : EntityRow, new()
    {
        var row = connection.Find<TRow>(RowMapper.Id(id));
        if (row is null) return;
        var timestamp = RowMapper.Date(DateTimeOffset.UtcNow);
        row.ArchivedUtc = timestamp;
        row.UpdatedUtc = timestamp;
        connection.Update(row);
    }
}
