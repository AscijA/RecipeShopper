#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;

namespace RecipeShopper.App.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class DailyReminderReceiver : BroadcastReceiver
{
    private const string ChannelId = "meal-plan-reminders";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        Notification.Builder builder;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            manager?.CreateNotificationChannel(new NotificationChannel(ChannelId, "Plan obroka", NotificationImportance.Default)
            {
                Description = "Dnevni podsjetnik za plan obroka"
            });
            builder = new Notification.Builder(context, ChannelId);
        }
        else
        {
#pragma warning disable CA1422
            builder = new Notification.Builder(context);
#pragma warning restore CA1422
        }
        var notification = builder
            .SetContentTitle(intent?.GetStringExtra("title") ?? "Današnji plan")
            .SetContentText(intent?.GetStringExtra("message") ?? "Pogledajte planirane obroke.")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetAutoCancel(true)
            .Build();
        manager?.Notify(2107, notification);
    }
}
#endif
