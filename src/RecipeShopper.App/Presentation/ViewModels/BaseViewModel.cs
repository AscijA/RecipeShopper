using CommunityToolkit.Mvvm.ComponentModel;

namespace RecipeShopper.App.Presentation.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string title = string.Empty;

    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    protected async Task RunBusyAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }
        try
        {
            IsBusy = true;
            await operation();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
