using SQLite;

namespace RecipeShopper.Infrastructure.Persistence;

public sealed class RecipeShopperDatabase : IAsyncDisposable
{
    private readonly DatabaseOptions _options;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly AsyncLocal<SQLiteConnection?> _ambientTransaction = new();
    private SQLiteAsyncConnection? _connection;
    private bool _initialized;

    public RecipeShopperDatabase(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DatabasePath);
        _options = options;
    }

    internal async Task<SQLiteAsyncConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        return _connection!;
    }

    internal async Task<T> ReadAsync<T>(Func<SQLiteConnection, T> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_ambientTransaction.Value is { } transaction)
        {
            return query(transaction);
        }

        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lockable = connection.GetConnection();
            using (lockable.Lock())
            {
                return query(lockable);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    internal async Task WriteAsync(Action<SQLiteConnection> operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_ambientTransaction.Value is { } transaction)
        {
            operation(transaction);
            return;
        }

        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.RunInTransactionAsync(operation).ConfigureAwait(false);
    }

    internal async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        if (_ambientTransaction.Value is not null)
        {
            await operation(cancellationToken).ConfigureAwait(false);
            return;
        }

        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.RunInTransactionAsync(syncConnection =>
        {
            _ambientTransaction.Value = syncConnection;
            try
            {
                operation(cancellationToken).GetAwaiter().GetResult();
            }
            finally
            {
                _ambientTransaction.Value = null;
            }
        }).ConfigureAwait(false);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(_options.DatabasePath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connection = new SQLiteAsyncConnection(
                _options.DatabasePath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

            await _connection.ExecuteAsync("PRAGMA foreign_keys = ON").ConfigureAwait(false);
            _ = await _connection.ExecuteScalarAsync<string>("PRAGMA journal_mode = WAL").ConfigureAwait(false);
            await _connection.ExecuteAsync("PRAGMA synchronous = NORMAL").ConfigureAwait(false);

            var version = await _connection.ExecuteScalarAsync<int>("PRAGMA user_version").ConfigureAwait(false);
            if (version > DatabaseSchema.CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"Database schema {version} is newer than supported schema {DatabaseSchema.CurrentVersion}.");
            }

            if (version < DatabaseSchema.CurrentVersion)
            {
                await _connection.RunInTransactionAsync(connection => DatabaseSchema.Migrate(connection, version))
                    .ConfigureAwait(false);
            }

            _initialized = true;
        }
        catch
        {
            if (_connection is not null)
            {
                await _connection.CloseAsync().ConfigureAwait(false);
                _connection = null;
            }

            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>("PRAGMA user_version").ConfigureAwait(false);
    }

    public async Task<bool> AreForeignKeysEnabledAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>("PRAGMA foreign_keys").ConfigureAwait(false) == 1;
    }

    public async Task<string> GetJournalModeAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode").ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _connection = null;
        }

        _initializationLock.Dispose();
    }
}
