using RecipeShopper.Application.Abstractions.Platform;

#if IOS
using UserNotifications;
#endif

namespace RecipeShopper.App.Presentation.Services;

public sealed class MauiNavigationService : INavigationService
{
    public Task GoToAsync(string route, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (parameters is null)
        {
            return Shell.Current.GoToAsync(route);
        }

        var safeParameters = parameters
            .Where(x => x.Value is not null)
            .ToDictionary(x => x.Key, x => x.Value!);
        return Shell.Current.GoToAsync(route, safeParameters);
    }

    public Task GoBackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Shell.Current.GoToAsync("..");
    }
}

public sealed class MauiImageService : IImageService
{
    private const string ImageDirectoryName = "recipe-images";

    public async Task<string?> PickAndStoreAsync(CancellationToken cancellationToken = default)
    {
        var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }).WaitAsync(cancellationToken);
        var result = results.FirstOrDefault();
        return result is null ? null : await StoreAsync(result, cancellationToken);
    }

    public async Task<string?> CaptureAndStoreAsync(CancellationToken cancellationToken = default)
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            return null;
        }
        var result = await MediaPicker.Default.CapturePhotoAsync().WaitAsync(cancellationToken);
        return result is null ? null : await StoreAsync(result, cancellationToken);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.GetFullPath(Path.Combine(FileSystem.AppDataDirectory, ImageDirectoryName));
        var path = Path.GetFullPath(Path.Combine(FileSystem.AppDataDirectory, relativePath));
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private static async Task<string> StoreAsync(FileResult result, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, ImageDirectoryName);
        Directory.CreateDirectory(directory);
        var extension = Path.GetExtension(result.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var destination = Path.Combine(directory, fileName);
        await using var input = await result.OpenReadAsync();
        await using var output = File.Create(destination);
        await input.CopyToAsync(output, cancellationToken);
        return Path.Combine(ImageDirectoryName, fileName).Replace('\\', '/');
    }
}

public sealed class MauiShareService : IShareService
{
    public Task ShareTextAsync(string title, string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Share.Default.RequestAsync(new ShareTextRequest(text, title));
    }

    public Task ShareFileAsync(string title, string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Share.Default.RequestAsync(new ShareFileRequest(title, new ShareFile(path)));
    }
}

public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<Stream?> PickJsonAsync(CancellationToken cancellationToken = default)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Odaberite JSON datoteku",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = ["application/json", "text/json", "text/plain"],
                [DevicePlatform.iOS] = ["public.json", "public.text"]
            })
        }).WaitAsync(cancellationToken);
        return result is null ? null : await result.OpenReadAsync();
    }

    public async Task<string> SaveJsonAsync(string suggestedFileName, Stream content, CancellationToken cancellationToken = default)
    {
        var safeName = string.Concat(suggestedFileName.Select(x => Path.GetInvalidFileNameChars().Contains(x) ? '_' : x));
        if (!safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".json";
        }
        var directory = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, safeName);
        await using var output = File.Create(path);
        await content.CopyToAsync(output, cancellationToken);
        return path;
    }
}

public sealed class MauiReminderService : IReminderService
{
    private const string ReminderRequestCode = "recipe-shopper-daily";

    public async Task<bool> RequestPermissionAsync(CancellationToken cancellationToken = default)
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>().WaitAsync(cancellationToken);
            return status == PermissionStatus.Granted;
        }
        return true;
#elif IOS
        var result = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Item1;
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    public Task ScheduleDailyAsync(TimeOnly time, string title, string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if ANDROID
        var context = Android.App.Application.Context;
        var intent = new Android.Content.Intent(context, typeof(Platforms.Android.DailyReminderReceiver));
        intent.PutExtra("title", title);
        intent.PutExtra("message", message);
        var pendingIntent = Android.App.PendingIntent.GetBroadcast(
            context,
            ReminderRequestCode.GetHashCode(StringComparison.Ordinal),
            intent,
            Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable);
        var next = DateTime.Today.Add(time.ToTimeSpan());
        if (next <= DateTime.Now)
        {
            next = next.AddDays(1);
        }
        var trigger = new DateTimeOffset(next).ToUnixTimeMilliseconds();
        var alarm = (Android.App.AlarmManager?)context.GetSystemService(Android.Content.Context.AlarmService);
        if (pendingIntent is not null)
        {
            alarm?.SetInexactRepeating(Android.App.AlarmType.RtcWakeup, trigger, Android.App.AlarmManager.IntervalDay, pendingIntent);
        }
#elif IOS
        var content = new UNMutableNotificationContent { Title = title, Body = message, Sound = UNNotificationSound.Default };
        var components = new NSDateComponents { Hour = time.Hour, Minute = time.Minute };
        var trigger = UNCalendarNotificationTrigger.CreateTrigger(components, true);
        var request = UNNotificationRequest.FromIdentifier(ReminderRequestCode, content, trigger);
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests([ReminderRequestCode]);
        UNUserNotificationCenter.Current.AddNotificationRequest(request, null);
#endif
        Preferences.Default.Set("daily-reminder-time", time.ToString("HH:mm"));
        return Task.CompletedTask;
    }

    public Task CancelDailyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if ANDROID
        var context = Android.App.Application.Context;
        var intent = new Android.Content.Intent(context, typeof(Platforms.Android.DailyReminderReceiver));
        var pendingIntent = Android.App.PendingIntent.GetBroadcast(
            context,
            ReminderRequestCode.GetHashCode(StringComparison.Ordinal),
            intent,
            Android.App.PendingIntentFlags.NoCreate | Android.App.PendingIntentFlags.Immutable);
        if (pendingIntent is not null)
        {
            var alarm = (Android.App.AlarmManager?)context.GetSystemService(Android.Content.Context.AlarmService);
            alarm?.Cancel(pendingIntent);
            pendingIntent.Cancel();
        }
#elif IOS
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests([ReminderRequestCode]);
#endif
        Preferences.Default.Remove("daily-reminder-time");
        return Task.CompletedTask;
    }
}

public interface IDialogService
{
    Task AlertAsync(string title, string message, string cancel = "U redu");
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel = "Odustani");
    Task<string?> PromptAsync(string title, string message, string accept = "Sačuvaj", string cancel = "Odustani", string? initialValue = null, Keyboard? keyboard = null);
    Task<string?> ActionSheetAsync(string title, string cancel, params string[] actions);
}

public sealed class MauiDialogService : IDialogService
{
    private static Page? CurrentPage => Shell.Current?.CurrentPage ?? Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;

    public Task AlertAsync(string title, string message, string cancel = "U redu") =>
        CurrentPage?.DisplayAlertAsync(title, message, cancel) ?? Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel = "Odustani") =>
        CurrentPage?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);

    public Task<string?> PromptAsync(string title, string message, string accept = "Sačuvaj", string cancel = "Odustani", string? initialValue = null, Keyboard? keyboard = null) =>
        CurrentPage?.DisplayPromptAsync(title, message, accept, cancel, initialValue: initialValue, keyboard: keyboard ?? Keyboard.Default)
        ?? Task.FromResult<string?>(null);

    public Task<string?> ActionSheetAsync(string title, string cancel, params string[] actions) =>
        CurrentPage?.DisplayActionSheetAsync(title, cancel, null, actions) ?? Task.FromResult<string?>(null);
}
