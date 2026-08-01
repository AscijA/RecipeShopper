using Microsoft.Extensions.DependencyInjection;

namespace RecipeShopper.App;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        this.shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(shell);
    }
}
