using Microsoft.Extensions.Logging;
using RecipeShopper.App.Presentation.Services;
using RecipeShopper.App.Presentation.ViewModels;
using RecipeShopper.App.Presentation.Views;
using RecipeShopper.Application.Abstractions.Platform;
using RecipeShopper.Infrastructure;
using RecipeShopper.Infrastructure.Sync;
using System.Reflection;
#if IOS
using UIKit;
#endif
#if ANDROID
using Android.Graphics.Drawables;
#endif

namespace RecipeShopper.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if IOS
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, _) =>
            handler.PlatformView.BorderStyle = UITextBorderStyle.None);
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("BorderlessEditor", (handler, _) =>
        {
            handler.PlatformView.Layer.BorderWidth = 0;
            handler.PlatformView.BackgroundColor = UIColor.Clear;
        });
#endif
#if ANDROID
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, _) =>
        {
            handler.PlatformView.Background = new ColorDrawable(Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, handler.PlatformView.PaddingTop, 0, handler.PlatformView.PaddingBottom);
        });
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("BorderlessEditor", (handler, _) =>
            handler.PlatformView.Background = new ColorDrawable(Android.Graphics.Color.Transparent));
#endif

        builder.Services.AddSingleton<IAppDataStore, DemoAppDataStore>();
        builder.Services.AddSingleton<INavigationService, MauiNavigationService>();
        builder.Services.AddSingleton<IImageService, MauiImageService>();
        builder.Services.AddSingleton<IShareService, MauiShareService>();
        builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();
        builder.Services.AddSingleton<IReminderService, MauiReminderService>();
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddRecipeShopperInfrastructure(
            Path.Combine(FileSystem.AppDataDirectory, "recipe-shopper.db3"),
            Path.Combine(FileSystem.AppDataDirectory, "images"),
            new SupabaseSyncOptions(
                ConfigurationValue("RecipeShopperSupabaseUrl", "RECIPE_SHOPPER_SUPABASE_URL"),
                ConfigurationValue("RecipeShopperSupabaseAnonKey", "RECIPE_SHOPPER_SUPABASE_ANON_KEY")));

        builder.Services.AddSingleton<RecipesViewModel>();
        builder.Services.AddSingleton<ShoppingViewModel>();
        builder.Services.AddSingleton<IngredientsViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<RecipesPage>();
        builder.Services.AddSingleton<ShoppingPage>();
        builder.Services.AddSingleton<IngredientsPage>();
        builder.Services.AddSingleton<SettingsPage>();

        builder.Services.AddTransient<RecipeDetailViewModel>();
        builder.Services.AddTransient<RecipeEditViewModel>();
        builder.Services.AddTransient<MealPlannerViewModel>();
        builder.Services.AddTransient<ShoppingListsViewModel>();
        builder.Services.AddTransient<IngredientDetailViewModel>();
        builder.Services.AddTransient<IngredientEditViewModel>();
        builder.Services.AddTransient<RecipeDetailPage>();
        builder.Services.AddTransient<RecipeEditPage>();
        builder.Services.AddTransient<MealPlannerPage>();
        builder.Services.AddTransient<ShoppingListsPage>();
        builder.Services.AddTransient<IngredientDetailPage>();
        builder.Services.AddTransient<IngredientEditPage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }

    private static string ConfigurationValue(string metadataKey, string environmentKey) =>
        Environment.GetEnvironmentVariable(environmentKey)
        ?? typeof(MauiProgram).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(value => value.Key == metadataKey)?.Value
        ?? string.Empty;
}
