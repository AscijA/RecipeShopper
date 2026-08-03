using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.Application.Abstractions.ImportExport;
using RecipeShopper.Application.Abstractions.Platform;
using RecipeShopper.Application.Abstractions.Sync;
using ImportConflictResolution = RecipeShopper.Domain.Enums.ImportConflictResolution;
using ImportMode = RecipeShopper.Domain.Enums.ImportMode;

namespace RecipeShopper.App.Presentation.ViewModels;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly IAppDataStore store;
    private readonly IReminderService reminders;
    private readonly IFilePickerService files;
    private readonly IShareService share;
    private readonly IImportExportService importExport;
    private readonly IDialogService dialogs;
    private readonly IWorkspaceSyncService sync;
    private bool loading;

    [ObservableProperty] private string selectedAppearance = "Sistem";
    [ObservableProperty] private string selectedAccent = "Zelena";
    [ObservableProperty] private string defaultGrouping = "Bez grupiranja";
    [ObservableProperty] private bool moveCheckedToBottom;
    [ObservableProperty] private bool hideChecked;
    [ObservableProperty] private bool confirmDestructiveActions;
    [ObservableProperty] private DateTime referenceMonday;
    [ObservableProperty] private bool reminderEnabled;
    [ObservableProperty] private TimeSpan reminderTime;
    [ObservableProperty] private string currency = "BAM";
    [ObservableProperty] private string shareCodeInput = string.Empty;
    [ObservableProperty] private string shareCode = string.Empty;
    [ObservableProperty] private string syncStatusText = "Nije povezano";
    [ObservableProperty] private bool isWorkspaceConnected;
    [ObservableProperty] private bool isSyncConfigured;

    public IReadOnlyList<string> AppearanceOptions { get; } = ["Sistem", "Svijetla", "Tamna"];
    public IReadOnlyList<string> AccentOptions { get; } = ["Zelena", "Narandžasta", "Plava"];
    public IReadOnlyList<string> GroupingOptions { get; } = ["Bez grupiranja", "Po receptu", "Po trgovini"];
    public IReadOnlyList<string> CurrencyOptions { get; } = ["BAM", "EUR"];
    public string VersionText => $"Moji Recepti {AppInfo.Current.VersionString}";

    public SettingsViewModel(
        IAppDataStore store,
        IReminderService reminders,
        IFilePickerService files,
        IShareService share,
        IImportExportService importExport,
        IDialogService dialogs,
        IWorkspaceSyncService sync)
    {
        this.store = store;
        this.reminders = reminders;
        this.files = files;
        this.share = share;
        this.importExport = importExport;
        this.dialogs = dialogs;
        this.sync = sync;
        sync.StatusChanged += OnSyncStatusChanged;
        Title = "Postavke";
        Load();
    }

    [RelayCommand]
    private async Task CreateWorkspaceAsync()
    {
        try
        {
            var code = await sync.CreateWorkspaceAsync();
            UpdateSyncStatus(sync.Status);
            await share.ShareTextAsync("Kod zajedničkog prostora", $"Pridruži se mom prostoru u aplikaciji Moji Recepti. Kod: {code}");
        }
        catch (Exception exception) { await dialogs.AlertAsync("Povezivanje nije uspjelo", exception.Message); }
    }

    [RelayCommand]
    private async Task JoinWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(ShareCodeInput)) return;
        try
        {
            await sync.JoinWorkspaceAsync(ShareCodeInput);
            store.ReloadPersistentState();
            Load();
            UpdateSyncStatus(sync.Status);
        }
        catch (Exception exception) { await dialogs.AlertAsync("Kod nije prihvaćen", exception.Message); }
    }

    [RelayCommand]
    private Task ShareWorkspaceCodeAsync() => share.ShareTextAsync(
        "Kod zajedničkog prostora",
        $"Pridruži se mom prostoru u aplikaciji Moji Recepti. Kod: {ShareCode}");

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        try
        {
            await sync.SyncAsync();
            store.ReloadPersistentState();
            Load();
            UpdateSyncStatus(sync.Status);
        }
        catch (Exception exception) { await dialogs.AlertAsync("Sinhronizacija nije uspjela", exception.Message); }
    }

    [RelayCommand]
    private async Task LeaveWorkspaceAsync()
    {
        if (!await dialogs.ConfirmAsync("Napustiti zajednički prostor?", "Lokalni podaci ostaju na ovom uređaju.", "Napusti")) return;
        await sync.LeaveWorkspaceAsync();
        UpdateSyncStatus(sync.Status);
    }

    partial void OnSelectedAppearanceChanged(string value)
    {
        if (loading) return;
        store.Settings.Appearance = value switch { "Svijetla" => AppAppearance.Light, "Tamna" => AppAppearance.Dark, _ => AppAppearance.System };
        ApplyAppearance();
    }

    partial void OnSelectedAccentChanged(string value)
    {
        if (loading) return;
        store.Settings.Accent = value;
        ApplyAccent();
    }

    partial void OnDefaultGroupingChanged(string value)
    {
        if (loading) return;
        store.Settings.DefaultGrouping = value switch { "Po receptu" => ShoppingGrouping.Recipe, "Po trgovini" => ShoppingGrouping.Store, _ => ShoppingGrouping.None };
    }

    partial void OnMoveCheckedToBottomChanged(bool value) { if (!loading) store.Settings.MoveCheckedToBottom = value; }
    partial void OnHideCheckedChanged(bool value) { if (!loading) store.Settings.HideChecked = value; }
    partial void OnConfirmDestructiveActionsChanged(bool value) { if (!loading) store.Settings.ConfirmDestructiveActions = value; }
    partial void OnReferenceMondayChanged(DateTime value)
    {
        if (loading) return;
        var offset = ((int)value.DayOfWeek + 6) % 7;
        store.Settings.ReferenceMonday = value.Date.AddDays(-offset);
        if (ReferenceMonday != store.Settings.ReferenceMonday) ReferenceMonday = store.Settings.ReferenceMonday;
    }
    partial void OnReminderTimeChanged(TimeSpan value)
    {
        if (!loading && ReminderEnabled) _ = ScheduleReminderAsync();
    }
    partial void OnCurrencyChanged(string value) { if (!loading) store.Settings.Currency = value; }

    partial void OnReminderEnabledChanged(bool value)
    {
        if (loading) return;
        _ = SetReminderAsync(value);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        await using var stream = new MemoryStream();
        await importExport.ExportBackupAsync(stream);
        stream.Position = 0;
        var path = await files.SaveJsonAsync($"moji-recepti-{DateTime.Today:yyyy-MM-dd}.json", stream);
        await share.ShareFileAsync("Izvoz podataka", path);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        await using var selected = await files.PickJsonAsync();
        if (selected is null) return;
        await using var buffer = new MemoryStream();
        await selected.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        var modeChoice = await dialogs.ActionSheetAsync(
            "Način uvoza",
            "Odustani",
            "Spoji sa postojećim podacima",
            "Zamijeni sve podatke");
        if (modeChoice is null) return;
        var mode = modeChoice.StartsWith("Zamijeni", StringComparison.Ordinal)
            ? ImportMode.Replace
            : ImportMode.Merge;

        await using var previewStream = new MemoryStream(bytes, writable: false);
        var preview = await importExport.PreviewAsync(previewStream, mode);
        if (!preview.CanCommit)
        {
            await dialogs.AlertAsync("Uvoz nije moguć", string.Join(Environment.NewLine, preview.Errors));
            return;
        }

        var resolutions = new Dictionary<string, ImportConflictResolution>();
        foreach (var conflict in preview.Conflicts)
        {
            var resolution = await dialogs.ActionSheetAsync(
                $"Konflikt: {conflict.DisplayName}",
                "Odustani",
                "Zadrži lokalno",
                "Koristi uvezeno",
                "Zadrži oba");
            if (resolution is null) return;
            resolutions[conflict.Key] = resolution switch
            {
                "Koristi uvezeno" => ImportConflictResolution.UseImported,
                "Zadrži oba" => ImportConflictResolution.KeepBoth,
                _ => ImportConflictResolution.KeepLocal
            };
        }

        var confirmed = await dialogs.ConfirmAsync(
            "Potvrditi uvoz?",
            $"Novo: {preview.Additions}, spajanja: {preview.Merges}, konflikti: {preview.Conflicts.Count}.",
            "Uvezi");
        if (!confirmed) return;

        await using var importStream = new MemoryStream(bytes, writable: false);
        var result = await importExport.ImportAsync(importStream, mode, resolutions);
        store.ReloadPersistentState();
        Load();
        await dialogs.AlertAsync(
            "Uvoz završen",
            $"Dodano: {result.Added}, spojeno: {result.Merged}, zamijenjeno: {result.Replaced}, zadržano oba: {result.KeptBoth}.");
    }

    [RelayCommand]
    private async Task ManageCategoriesAsync()
    {
        var choice = await dialogs.ActionSheetAsync("Kategorije", "Zatvori", store.Categories.Select(x => $"{x.Icon} {x.Name}").ToArray());
        if (choice is not null) await dialogs.AlertAsync("Kategorije", "U v1 se kategorije uređuju kroz import kataloga; ugrađene kategorije ostaju dostupne.");
    }

    [RelayCommand]
    private async Task ManageStoresAsync()
    {
        var stores = store.Stores.ToList();
        var labels = stores.Select(x => $"{(x.IsEnabled ? "✓" : "○")} {x.Name}").ToArray();
        var choice = await dialogs.ActionSheetAsync("Dostupne trgovine", "Zatvori", labels);
        var index = Array.IndexOf(labels, choice);
        if (index >= 0)
        {
            stores[index].IsEnabled = !stores[index].IsEnabled;
            store.SaveStore(stores[index]);
        }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!await dialogs.ConfirmAsync("Vratiti početne podatke?", "Svi trenutni lokalni podaci biće zamijenjeni početnim sadržajem.", "Vrati podatke")) return;
        store.ResetDemoData();
        Load();
        await dialogs.AlertAsync("Podaci vraćeni", "Početni recepti, namirnice i liste su ponovo učitani.");
    }

    private void Load()
    {
        loading = true;
        SelectedAppearance = store.Settings.Appearance switch { AppAppearance.Light => "Svijetla", AppAppearance.Dark => "Tamna", _ => "Sistem" };
        SelectedAccent = store.Settings.Accent;
        DefaultGrouping = store.Settings.DefaultGrouping switch { ShoppingGrouping.Recipe => "Po receptu", ShoppingGrouping.Store => "Po trgovini", _ => "Bez grupiranja" };
        MoveCheckedToBottom = store.Settings.MoveCheckedToBottom;
        HideChecked = store.Settings.HideChecked;
        ConfirmDestructiveActions = store.Settings.ConfirmDestructiveActions;
        ReferenceMonday = store.Settings.ReferenceMonday;
        ReminderEnabled = store.Settings.ReminderEnabled;
        ReminderTime = store.Settings.ReminderTime;
        Currency = store.Settings.Currency;
        UpdateSyncStatus(sync.Status);
        loading = false;
        ApplyAppearance();
        ApplyAccent();
    }

    private void OnSyncStatusChanged(object? sender, SyncStatus value) =>
        MainThread.BeginInvokeOnMainThread(() => UpdateSyncStatus(value));

    private void UpdateSyncStatus(SyncStatus value)
    {
        ShareCode = value.ShareCode ?? string.Empty;
        IsWorkspaceConnected = value.IsConnected;
        IsSyncConfigured = value.State != SyncConnectionState.NotConfigured;
        SyncStatusText = value.State switch
        {
            SyncConnectionState.NotConfigured => "Supabase nije konfigurisan",
            SyncConnectionState.Disconnected => "Nije povezano",
            SyncConnectionState.Syncing => "Sinhronizacija…",
            SyncConnectionState.Synced => value.LastSyncedAt is null ? "Sinhronizovano" : $"Sinhronizovano {value.LastSyncedAt.Value.ToLocalTime():g}",
            SyncConnectionState.Offline => value.Message ?? "Offline — promjene su sačuvane lokalno",
            _ => value.Message ?? "Sinhronizacija nije uspjela"
        };
    }

    private void ApplyAppearance()
    {
        if (Microsoft.Maui.Controls.Application.Current is null) return;
        Microsoft.Maui.Controls.Application.Current.UserAppTheme = store.Settings.Appearance switch
        {
            AppAppearance.Light => AppTheme.Light,
            AppAppearance.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    private void ApplyAccent()
    {
        if (Microsoft.Maui.Controls.Application.Current is null) return;
        var color = SelectedAccent switch { "Narandžasta" => Color.FromArgb("#C86522"), "Plava" => Color.FromArgb("#386A8D"), _ => Color.FromArgb("#2F6B4F") };
        Microsoft.Maui.Controls.Application.Current.Resources["Primary"] = color;
        Microsoft.Maui.Controls.Application.Current.Resources["PrimaryBrush"] = new SolidColorBrush(color);
    }

    private async Task SetReminderAsync(bool enabled)
    {
        if (enabled)
        {
            if (!await reminders.RequestPermissionAsync())
            {
                loading = true;
                ReminderEnabled = false;
                loading = false;
                await dialogs.AlertAsync("Podsjetnici nisu dozvoljeni", "Omogućite obavijesti u postavkama uređaja.");
                return;
            }
            await ScheduleReminderAsync();
        }
        else
        {
            await reminders.CancelDailyAsync();
        }
        store.Settings.ReminderEnabled = enabled;
    }

    private Task ScheduleReminderAsync()
    {
        store.Settings.ReminderTime = ReminderTime;
        return reminders.ScheduleDailyAsync(TimeOnly.FromTimeSpan(ReminderTime), "Današnji plan obroka", "Pogledajte šta je danas na meniju.");
    }
}
