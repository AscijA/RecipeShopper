using Foundation;

namespace RecipeShopper.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary? launchOptions)
    {
        var launched = base.FinishedLaunching(application, launchOptions);
        Microsoft.Maui.Platform.KeyboardAutoManagerScroll.Connect();
        return launched;
    }
}
