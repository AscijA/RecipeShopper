using RecipeShopper.Application.Abstractions.Persistence;
using RecipeShopper.Domain.Entities;

namespace RecipeShopper.Infrastructure.Persistence;

public sealed class CatalogRepository(RecipeShopperDatabase database) : ICatalogRepository
{
    public Task<IReadOnlyList<RecipeCategory>> GetCategoriesAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<RecipeCategory>>(connection =>
        {
            var sql = "SELECT * FROM recipe_categories" + (includeArchived ? string.Empty : " WHERE ArchivedUtc IS NULL") +
                      " ORDER BY SortOrder, Name COLLATE NOCASE";
            return connection.Query<RecipeCategoryRow>(sql).Select(RowMapper.ToEntity).ToArray();
        }, cancellationToken);

    public Task UpsertCategoryAsync(RecipeCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.Name);
        return database.WriteAsync(connection => connection.InsertOrReplace(RowMapper.ToRow(category)), cancellationToken);
    }

    public Task<IReadOnlyList<PackageType>> GetPackageTypesAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<PackageType>>(connection =>
        {
            var sql = "SELECT * FROM package_types" + (includeArchived ? string.Empty : " WHERE ArchivedUtc IS NULL") +
                      " ORDER BY SortOrder, Name COLLATE NOCASE";
            return connection.Query<PackageTypeRow>(sql).Select(RowMapper.ToEntity).ToArray();
        }, cancellationToken);

    public Task UpsertPackageTypeAsync(PackageType packageType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageType.Name);
        return database.WriteAsync(connection => connection.InsertOrReplace(RowMapper.ToRow(packageType)), cancellationToken);
    }

    public Task<IReadOnlyList<Store>> GetStoresAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<Store>>(connection =>
        {
            var sql = "SELECT * FROM stores" + (includeArchived ? string.Empty : " WHERE ArchivedUtc IS NULL") +
                      " ORDER BY SortOrder, Name COLLATE NOCASE";
            return connection.Query<StoreRow>(sql).Select(RowMapper.ToEntity).ToArray();
        }, cancellationToken);

    public Task UpsertStoreAsync(Store store, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(store.Name);
        return database.WriteAsync(connection => connection.InsertOrReplace(RowMapper.ToRow(store)), cancellationToken);
    }
}
