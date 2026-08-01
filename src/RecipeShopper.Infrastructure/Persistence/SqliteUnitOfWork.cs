using RecipeShopper.Application.Abstractions.Persistence;

namespace RecipeShopper.Infrastructure.Persistence;

public sealed class SqliteUnitOfWork(RecipeShopperDatabase database) : IUnitOfWork
{
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
        database.ExecuteInTransactionAsync(operation, cancellationToken);
}
