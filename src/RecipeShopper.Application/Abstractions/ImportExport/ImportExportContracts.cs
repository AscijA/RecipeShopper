using RecipeShopper.Application.Models.ImportExport;
using RecipeShopper.Domain.Enums;

namespace RecipeShopper.Application.Abstractions.ImportExport;

public interface IImportExportService
{
    Task<ImportPreview> PreviewAsync(Stream json, ImportMode mode, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportAsync(Stream json, ImportMode mode, IReadOnlyDictionary<string, ImportConflictResolution> resolutions, CancellationToken cancellationToken = default);
    Task ExportCatalogAsync(Stream destination, CancellationToken cancellationToken = default);
    Task ExportBackupAsync(Stream destination, CancellationToken cancellationToken = default);
}

public sealed record ImportConflict(string Key, string EntityType, string DisplayName, string Reason);
public sealed record ImportPreview(int Additions, int Merges, IReadOnlyList<ImportConflict> Conflicts, IReadOnlyList<string> Errors)
{
    public bool CanCommit => Errors.Count == 0;
}

public sealed record ImportResult(int Added, int Merged, int Replaced, int KeptBoth);
