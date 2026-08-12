using Android.App;
using Android.Gms.Extensions;
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
