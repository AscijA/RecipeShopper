namespace RecipeShopper.Application.Abstractions.Platform;

public interface IImageService
{
    Task<string?> PickAndStoreAsync(CancellationToken cancellationToken = default);
    Task<string?> CaptureAndStoreAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

public interface IReminderService
{
    Task<bool> RequestPermissionAsync(CancellationToken cancellationToken = default);
    Task ScheduleDailyAsync(TimeOnly time, string title, string message, CancellationToken cancellationToken = default);
    Task CancelDailyAsync(CancellationToken cancellationToken = default);
}

public interface INavigationService
{
    Task GoToAsync(string route, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);
    Task GoBackAsync(CancellationToken cancellationToken = default);
}

public interface IShareService
{
    Task ShareTextAsync(string title, string text, CancellationToken cancellationToken = default);
    Task ShareFileAsync(string title, string path, CancellationToken cancellationToken = default);
}

public interface IFilePickerService
{
    Task<Stream?> PickJsonAsync(CancellationToken cancellationToken = default);
    Task<string> SaveJsonAsync(string suggestedFileName, Stream content, CancellationToken cancellationToken = default);
}
