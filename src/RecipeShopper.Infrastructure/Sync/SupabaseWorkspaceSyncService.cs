using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RecipeShopper.Application.Abstractions.ImportExport;
using RecipeShopper.Application.Abstractions.Sync;
using RecipeShopper.Domain.Enums;
using RecipeShopper.Infrastructure.Persistence;

namespace RecipeShopper.Infrastructure.Sync;

public sealed class SupabaseWorkspaceSyncService : IWorkspaceSyncService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly RecipeShopperDatabase database;
    private readonly IImportExportService transfers;
    private readonly HttpClient http;
    private readonly SupabaseSyncOptions options;
    private readonly SemaphoreSlim gate = new(1, 1);
    private SyncStatus status;

    public SupabaseWorkspaceSyncService(
        RecipeShopperDatabase database,
        IImportExportService transfers,
        HttpClient http,
        SupabaseSyncOptions options)
    {
        this.database = database;
        this.transfers = transfers;
        this.http = http;
        this.options = options;
        status = options.IsConfigured
            ? new(SyncConnectionState.Disconnected)
            : new(SyncConnectionState.NotConfigured, Message: "Supabase nije konfigurisan");
        RestoreStatus();
    }

    public SyncStatus Status => status;
    public event EventHandler<SyncStatus>? StatusChanged;

    public void MarkLocalChange()
    {
        database.WriteAsync(connection => connection.InsertOrReplace(new SyncOutboxRow
        {
            Id = "pending",
            CreatedUtc = DateTimeOffset.UtcNow.ToString("O")
        })).GetAwaiter().GetResult();

        if (status.IsConnected) SetStatus(status with { State = SyncConnectionState.Offline, Message = "Lokalne promjene čekaju sinhronizaciju" });
    }

    public async Task<string> CreateWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetStatus(new(SyncConnectionState.Syncing, Message: "Kreiranje zajedničkog prostora…"));
            var snapshot = await ExportAsync(cancellationToken).ConfigureAwait(false);
            using var response = await SendRpcAsync("create_recipe_shopper_workspace", new { p_snapshot = snapshot }, cancellationToken).ConfigureAwait(false);
            var result = await ReadRpcResultAsync<CreateResult>(response, cancellationToken).ConfigureAwait(false);
            await SaveWorkspaceAsync(result.WorkspaceId, result.ShareCode, result.Revision, cancellationToken).ConfigureAwait(false);
            await ClearOutboxAsync(cancellationToken).ConfigureAwait(false);
            SetStatus(new(SyncConnectionState.Synced, result.ShareCode, DateTimeOffset.UtcNow));
            return result.ShareCode;
        }
        catch (Exception exception)
        {
            SetFailure(exception);
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task JoinWorkspaceAsync(string shareCode, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var normalized = NormalizeCode(shareCode);
        if (normalized.Length != 8) throw new ArgumentException("Kod mora sadržavati 8 znakova.", nameof(shareCode));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetStatus(new(SyncConnectionState.Syncing, Message: "Povezivanje…"));
            using var response = await SendRpcAsync("join_recipe_shopper_workspace", new { p_share_code = normalized }, cancellationToken).ConfigureAwait(false);
            var result = await ReadRpcResultAsync<WorkspaceResult>(response, cancellationToken).ConfigureAwait(false);
            await ImportAsync(result.Snapshot, cancellationToken).ConfigureAwait(false);
            await SaveWorkspaceAsync(result.WorkspaceId, FormatCode(normalized), result.Revision, cancellationToken).ConfigureAwait(false);
            await ClearOutboxAsync(cancellationToken).ConfigureAwait(false);
            SetStatus(new(SyncConnectionState.Synced, FormatCode(normalized), DateTimeOffset.UtcNow));
        }
        catch (Exception exception)
        {
            SetFailure(exception);
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var workspace = await GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            if (workspace is null) return;
            SetStatus(status with { State = SyncConnectionState.Syncing, Message = "Sinhronizacija…" });
            var pending = await HasPendingChangesAsync(cancellationToken).ConfigureAwait(false);
            if (pending)
            {
                var snapshot = await ExportAsync(cancellationToken).ConfigureAwait(false);
                using var push = await SendRpcAsync("push_recipe_shopper_workspace", new
                {
                    p_workspace_id = workspace.WorkspaceId,
                    p_share_code = NormalizeCode(workspace.ShareCode),
                    p_expected_revision = workspace.Revision,
                    p_snapshot = snapshot
                }, cancellationToken).ConfigureAwait(false);

                if (push.StatusCode == HttpStatusCode.Conflict)
                {
                    await PullAndApplyAsync(workspace, cancellationToken).ConfigureAwait(false);
                    MarkLocalChange();
                    throw new InvalidOperationException("Podaci su promijenjeni na drugom uređaju. Preuzeta je novija verzija; pokušajte ponovo.");
                }

                var pushed = await ReadRpcResultAsync<WorkspaceResult>(push, cancellationToken).ConfigureAwait(false);
                workspace.Revision = pushed.Revision;
                await SaveWorkspaceRowAsync(workspace, cancellationToken).ConfigureAwait(false);
                await ClearOutboxAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await PullAndApplyAsync(workspace, cancellationToken).ConfigureAwait(false);
            }

            var now = DateTimeOffset.UtcNow;
            workspace.LastSyncedUtc = now.ToString("O");
            await SaveWorkspaceRowAsync(workspace, cancellationToken).ConfigureAwait(false);
            SetStatus(new(SyncConnectionState.Synced, workspace.ShareCode, now));
        }
        catch (HttpRequestException exception)
        {
            SetStatus(status with { State = SyncConnectionState.Offline, Message = exception.Message });
            throw;
        }
        catch (Exception exception)
        {
            SetFailure(exception);
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task LeaveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        await database.WriteAsync(connection =>
        {
            connection.DeleteAll<SyncWorkspaceRow>();
            connection.DeleteAll<SyncOutboxRow>();
        }, cancellationToken).ConfigureAwait(false);
        SetStatus(options.IsConfigured ? new(SyncConnectionState.Disconnected) : new(SyncConnectionState.NotConfigured));
    }

    private async Task PullAndApplyAsync(SyncWorkspaceRow local, CancellationToken cancellationToken)
    {
        using var response = await SendRpcAsync("pull_recipe_shopper_workspace", new
        {
            p_workspace_id = local.WorkspaceId,
            p_share_code = NormalizeCode(local.ShareCode),
            p_after_revision = local.Revision
        }, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent) return;
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json) || json == "null") return;
        var remote = JsonSerializer.Deserialize<WorkspaceResult>(json, Json)
                     ?? throw new InvalidDataException("Supabase je vratio prazan odgovor.");
        if (remote.Revision <= local.Revision) return;
        await ImportAsync(remote.Snapshot, cancellationToken).ConfigureAwait(false);
        local.Revision = remote.Revision;
        await SaveWorkspaceRowAsync(local, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ExportAsync(CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await transfers.ExportBackupAsync(stream, cancellationToken).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task ImportAsync(string snapshot, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(snapshot));
        var preview = await transfers.PreviewAsync(stream, ImportMode.Replace, cancellationToken).ConfigureAwait(false);
        if (!preview.CanCommit) throw new InvalidDataException(string.Join(Environment.NewLine, preview.Errors));
        stream.Position = 0;
        await transfers.ImportAsync(stream, ImportMode.Replace, new Dictionary<string, ImportConflictResolution>(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendRpcAsync(string function, object body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Url.TrimEnd('/')}/rest/v1/rpc/{function}")
        {
            Content = JsonContent.Create(body, options: Json)
        };
        request.Headers.TryAddWithoutValidation("apikey", options.AnonKey);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {options.AnonKey}");
        var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Conflict) response.EnsureSuccessStatusCode();
        return response;
    }

    private static async Task<T> ReadRpcResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
        return value ?? throw new InvalidDataException("Supabase je vratio prazan odgovor.");
    }

    private void RestoreStatus()
    {
        if (!options.IsConfigured) return;
        var workspace = database.ReadAsync(connection => connection.Table<SyncWorkspaceRow>().FirstOrDefault()).GetAwaiter().GetResult();
        if (workspace is null) return;
        var last = DateTimeOffset.TryParse(workspace.LastSyncedUtc, out var parsed) ? parsed : null as DateTimeOffset?;
        status = new(SyncConnectionState.Offline, workspace.ShareCode, last, "Spremno za sinhronizaciju");
    }

    private async Task<SyncWorkspaceRow?> GetWorkspaceAsync(CancellationToken cancellationToken) =>
        await database.ReadAsync(connection => connection.Table<SyncWorkspaceRow>().FirstOrDefault(), cancellationToken).ConfigureAwait(false);

    private Task<bool> HasPendingChangesAsync(CancellationToken cancellationToken) =>
        database.ReadAsync(connection => connection.Table<SyncOutboxRow>().Any(), cancellationToken);

    private Task ClearOutboxAsync(CancellationToken cancellationToken) =>
        database.WriteAsync(connection => connection.DeleteAll<SyncOutboxRow>(), cancellationToken);

    private async Task SaveWorkspaceAsync(string workspaceId, string shareCode, long revision, CancellationToken cancellationToken)
    {
        var existing = await GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        await SaveWorkspaceRowAsync(new SyncWorkspaceRow
        {
            WorkspaceId = workspaceId,
            ShareCode = FormatCode(NormalizeCode(shareCode)),
            DeviceId = existing?.DeviceId ?? Guid.NewGuid().ToString("N"),
            Revision = revision,
            LastSyncedUtc = DateTimeOffset.UtcNow.ToString("O")
        }, cancellationToken).ConfigureAwait(false);
    }

    private Task SaveWorkspaceRowAsync(SyncWorkspaceRow row, CancellationToken cancellationToken) =>
        database.WriteAsync(connection => connection.InsertOrReplace(row), cancellationToken);

    private void EnsureConfigured()
    {
        if (!options.IsConfigured) throw new InvalidOperationException("Postavite RECIPE_SHOPPER_SUPABASE_URL i RECIPE_SHOPPER_SUPABASE_ANON_KEY.");
    }

    private void SetFailure(Exception exception) => SetStatus(status with { State = SyncConnectionState.Failed, Message = exception.Message });
    private void SetStatus(SyncStatus value) { status = value; StatusChanged?.Invoke(this, value); }
    private static string NormalizeCode(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static string FormatCode(string value) => value.Length == 8 ? $"{value[..4]}-{value[4..]}" : value;

    private sealed record CreateResult(string WorkspaceId, string ShareCode, long Revision);
    private sealed record WorkspaceResult(string WorkspaceId, long Revision, string Snapshot);
}
