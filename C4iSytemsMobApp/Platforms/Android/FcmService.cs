using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using AndroidX.Core.App;
using Firebase.Messaging;

namespace C4iSytemsMobApp.Services.Tracking.Platforms
{
    /// <summary>
    /// FCM receiver for the tracking nudge (§Push). ORCHESTRATION ONLY: validate the
    /// message, hand off to TrackingService — the single tracking engine. Unknown message
    /// types are logged and ignored so future push features can share the channel without
    /// touching this class.
    ///
    /// Why this works in Doze: the server sends HIGH-PRIORITY DATA messages, which buy
    /// roughly ten seconds of execution even while the device sleeps — enough for exactly
    /// one fresh fix and one upload, which is all NudgeAsync does. Honest limits: a
    /// force-stopped app receives nothing, and a dead process has no session in memory, so
    /// the nudge revives a SUSPENDED app, never a closed one.
    /// </summary>
    [Service(Exported = false)]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class FcmService : FirebaseMessagingService
    {
        public override void OnNewToken(string token)
        {
            base.OnNewToken(token);
            Console.WriteLine("[Tracking] FCM token refreshed by Firebase");
            _ = TrackingService.Instance.OnFcmTokenRefreshedAsync(token);
        }

        public override void OnMessageReceived(RemoteMessage message)
        {
            base.OnMessageReceived(message);
            try
            {
                var data = message.Data;
                if (data == null || !data.TryGetValue("type", out var type))
                    return;
                if (type == "TrackingMessage")
                {
                    /* Foreground half of operator messages: Android only auto-displays the
                       FCM Notification block while the app is backgrounded — with the app
                       open, this handler is the display. */
                    ShowOperatorMessage(message);
                    return;
                }
                if (type != "TrackingNudge")
                {
                    Console.WriteLine($"[Tracking] FCM message type '{type}' ignored");
                    return;
                }

                data.TryGetValue("unitId", out var unitId);
                data.TryGetValue("reason", out var reason);
                data.TryGetValue("requestId", out var requestId);
                Console.WriteLine($"[Tracking] TrackingNudgeReceived request {requestId} reason {reason}");

                /* Fire-and-forget with its own try/catch inside: OnMessageReceived must
                   return quickly, and a nudge fault must never crash the app. */
                _ = TrackingService.Instance.NudgeAsync(unitId, reason, requestId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tracking] FCM handler fault: {ex.GetType().Name} {ex.Message}");
            }
        }

        /// <summary>Operator text (type=TrackingMessage) received while the app is OPEN:
        /// show it as a local notification. Display faults must never crash the app —
        /// the message also lives in the server's audit trail either way.</summary>
        private void ShowOperatorMessage(RemoteMessage message)
        {
            try
            {
                var title = message.GetNotification()?.Title;
                if (string.IsNullOrWhiteSpace(title))
                    title = "CityWatch Control Room";

                message.Data.TryGetValue("message", out var body);
                if (string.IsNullOrWhiteSpace(body))
                    body = message.GetNotification()?.Body;
                if (string.IsNullOrWhiteSpace(body))
                {
                    Console.WriteLine("[Tracking] operator message ignored — no text in payload");
                    return;
                }

                message.Data.TryGetValue("requestId", out var requestId);
                Console.WriteLine($"[Tracking] TrackingMessageReceived request {requestId}");
                MessageNotifier.Show(this, title, body, requestId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tracking] operator message display fault: {ex.GetType().Name} {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Local-notification display for operator messages — the FOREGROUND half of the
    /// custom-message feature. Backgrounded/killed apps never reach this code: Android's
    /// system tray renders the FCM Notification block itself (routed into the same channel
    /// by the manifest's default_notification_channel_id meta-data).
    /// </summary>
    public static class MessageNotifier
    {
        public const string ChannelId = "citywatch_messages";

        /// <summary>Idempotent (CreateNotificationChannel is a no-op for an existing id).
        /// Called from MainActivity.OnCreate so the channel exists before the FIRST
        /// background message arrives, and again before every foreground display.</summary>
        public static void EnsureChannel()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                return;
            var context = Android.App.Application.Context;
            var channel = new NotificationChannel(ChannelId, "Control room messages",
                NotificationImportance.High)     // heads-up: an operator message is urgent
            {
                Description = "Messages sent to this phone by the control room."
            };
            (context.GetSystemService(Context.NotificationService) as NotificationManager)
                ?.CreateNotificationChannel(channel);
        }

        public static void Show(Context context, string title, string body, string? requestId)
        {
            EnsureChannel();

            /* Android 13+: without POST_NOTIFICATIONS the notify() would be silently
               dropped — say so in the log instead. A Service cannot prompt; the prompt
               lives in PermissionService and runs at session start. */
            if (OperatingSystem.IsAndroidVersionAtLeast(33)
                && !NotificationManagerCompat.From(context).AreNotificationsEnabled())
            {
                Console.WriteLine("[Tracking] operator message suppressed — notifications disabled");
                return;
            }

            /* Tap brings the app forward; MainActivity is SingleTop so the running
               instance is reused, never a second one stacked. */
            var launch = new Intent(context, typeof(MainActivity));
            launch.SetFlags(ActivityFlags.SingleTop);
            var tap = PendingIntent.GetActivity(context, 0, launch,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var notification = new NotificationCompat.Builder(context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))   // up to 240 chars
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetAutoCancel(true)
                .SetContentIntent(tap)
                .Build();

            /* Distinct id per message — consecutive messages must not overwrite each other.
               requestId is the server's correlation id, so retries of ONE message coalesce. */
            var id = string.IsNullOrWhiteSpace(requestId)
                ? Environment.TickCount & 0x7FFFFFFF
                : requestId.GetHashCode() & 0x7FFFFFFF;
            NotificationManagerCompat.From(context).Notify(id, notification);
        }
    }

    /// <summary>Token retrieval, failure-tolerant: no google-services.json or no Play
    /// services simply means the device cannot be nudged — tracking itself is unaffected.</summary>
    public static class FcmTokenHelper
    {
        public static async Task<string?> GetTokenAsync()
        {
            try
            {
                var token = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
                return token?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tracking] FCM token unavailable: {ex.GetType().Name} {ex.Message}");
                return null;
            }
        }
    }
}
