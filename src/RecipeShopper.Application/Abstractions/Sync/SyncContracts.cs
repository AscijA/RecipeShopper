namespace RecipeShopper.Application.Abstractions.Sync;

public enum SyncConnectionState
{
    NotConfigured,
    Disconnected,
    Offline,
    Syncing,
    Synced,
    Failed
}

public sealed record SyncStatus(
    SyncConnectionState State,
    string? ShareCode = null,
    DateTimeOffset? LastSyncedAt = null,
    string? Message = null)
{
    public bool IsConnected => !string.IsNullOrWhiteSpace(ShareCode);
}

public interface IWorkspaceSyncService
{
    SyncStatus Status { get; }
    event EventHandler<SyncStatus>? StatusChanged;

    void MarkLocalChange();
    Task<string> CreateWorkspaceAsync(CancellationToken cancellationToken = default);
    Task JoinWorkspaceAsync(string shareCode, CancellationToken cancellationToken = default);
    Task SyncAsync(CancellationToken cancellationToken = default);
    Task LeaveWorkspaceAsync(CancellationToken cancellationToken = default);
}
