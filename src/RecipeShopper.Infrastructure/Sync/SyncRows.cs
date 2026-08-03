using SQLite;

namespace RecipeShopper.Infrastructure.Sync;

[Table("sync_workspace")]
internal sealed class SyncWorkspaceRow
{
    [PrimaryKey] public int Id { get; set; } = 1;
    public string WorkspaceId { get; set; } = string.Empty;
    public string ShareCode { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string? LastSyncedUtc { get; set; }
}

[Table("sync_outbox")]
internal sealed class SyncOutboxRow
{
    [PrimaryKey] public string Id { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
}
