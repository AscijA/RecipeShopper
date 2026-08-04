using Microsoft.Extensions.DependencyInjection;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.Application.Abstractions.Sync;

namespace RecipeShopper.App;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell shell;
    private readonly IWorkspaceSyncService sync;
    private readonly IAppDataStore store;

    public App(AppShell shell, IWorkspaceSyncService sync, IAppDataStore store)
    {
        InitializeComponent();
        this.shell = shell;
        this.sync = sync;
        this.store = store;
        UserAppTheme = store.Settings.Appearance switch
        {
            Presentation.Models.AppAppearance.Light => AppTheme.Light,
            Presentation.Models.AppAppearance.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(shell);
        window.Activated += async (_, _) =>
        {
            if (!sync.Status.IsConnected) return;
            try
            {
                await sync.SyncAsync();
                store.ReloadPersistentState();
            }
            catch
            {
                // Offline and server errors are exposed through SyncStatus in Settings.
            }
        };
        return window;
    }
}
