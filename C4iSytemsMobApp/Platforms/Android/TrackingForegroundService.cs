using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace C4iSytemsMobApp.Services.Tracking.Platforms
{
    /// <summary>
    /// Android keep-alive for tracking (ADD §6.1). The sampling loop itself lives in
    /// TrackingService; this service exists so the OS (a) allows location off-foreground and
    /// (b) shows the mandatory persistent notification — which doubles as the always-visible
    /// "you are being tracked" indicator the privacy design requires (§13.5).
    /// </summary>
    [Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
    public class TrackingForegroundService : Service
    {
        public const int NotificationId = 94021;
        private const string ChannelId = "citywatch_tracking";

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            CreateChannel();

            /* #153 Part 6: Android will not run a location foreground service without a
               visible notification, so this cannot be removed — but the wording is the
               client's (R&D diary): duress accuracy and lost-device recovery are WHY the
               location is shared, and consent was already given at login. */
            var notification = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("CityWatch duress & safety active")
                .SetContentText("Shared with control room for duress accuracy or lost device.")
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetOngoing(true)
                .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
                .Build();

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                StartForeground(NotificationId, notification, ForegroundService.TypeLocation);
            else
                StartForeground(NotificationId, notification);

            return StartCommandResult.Sticky;
        }

        private void CreateChannel()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                return;
            var channel = new NotificationChannel(ChannelId, "Patrol tracking", NotificationImportance.Low)
            {
                Description = "Shown while your patrol session is sharing location."
            };
            (GetSystemService(NotificationService) as NotificationManager)?.CreateNotificationChannel(channel);
        }
    }

    /// <summary>Cross-platform seam used by TrackingService (#if ANDROID).</summary>
    public static class TrackingForegroundServiceHelper
    {
        public static void Start()
        {
            var context = Android.App.Application.Context;
            var intent = new Intent(context, typeof(TrackingForegroundService));
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
        }

        public static void Stop()
        {
            var context = Android.App.Application.Context;
            context.StopService(new Intent(context, typeof(TrackingForegroundService)));
        }
    }
}
