using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System.Net.Http.Json;

namespace C4iSytemsMobApp.Services
{
    /// <summary>
    /// Notifications tab API client.
    ///
    /// The guard id AND the selected site are read from Preferences on EVERY call rather than
    /// cached in the constructor. This service is a singleton, and both can change without the
    /// app restarting — a guard logs out and another logs in, or the same guard resets their
    /// site mid-shift. Cached values would show the previous guard's notifications, or the
    /// previous site's, which is exactly what this tab must never do.
    /// </summary>
    public class NotificationApiServices : INotificationApiServices
    {
        private readonly HttpClient _httpClient;

        public NotificationApiServices(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private static int CurrentGuardId =>
            int.TryParse(Preferences.Get("GuardId", "0"), out var guardId) ? guardId : 0;

        /// <summary>
        /// The site the guard is signed in at. Zero is valid — it simply means "no site
        /// notifications", which is the right answer before a site has been selected.
        /// </summary>
        private static int CurrentClientSiteId =>
            int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out var siteId) ? siteId : 0;

        public async Task<(bool isSuccess, string message, List<GuardNotification> notifications)> GetNotificationsAsync()
        {
            var empty = new List<GuardNotification>();

            var guardId = CurrentGuardId;
            if (guardId <= 0)
                return (false, "Guard ID not found. Please log in again.", empty);

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return (false, "You are offline. Notifications will appear once you are back online.", empty);

            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetGuardNotifications" +
                             $"?guardId={guardId}&clientSiteId={CurrentClientSiteId}";

                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                    return (false, "Unable to load notifications. Please try again.", empty);

                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<GuardNotification>>>();
                if (result == null || !result.isSuccess)
                    return (false, result?.message ?? "Unable to load notifications.", empty);

                return (true, result.message, result.data ?? empty);
            }
            catch (Exception ex)
            {
                return (false, $"Unable to load notifications. {ex.Message}", empty);
            }
        }

        public async Task<int> GetUnreadCountAsync()
        {
            var guardId = CurrentGuardId;
            if (guardId <= 0 || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return 0;

            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetGuardNotificationCount" +
                             $"?guardId={guardId}&clientSiteId={CurrentClientSiteId}";

                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                    return 0;

                var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
                return result is { isSuccess: true } ? result.data : 0;
            }
            catch (Exception ex)
            {
                // The badge is decoration: a failure here must never interrupt the home screen.
                Console.WriteLine($"Failed to read notification count: {ex.Message}");
                return 0;
            }
        }

        public async Task<(bool isSuccess, string message)> SetReadStatusAsync(int notificationId, bool isRead)
        {
            var guardId = CurrentGuardId;
            if (guardId <= 0)
                return (false, "Guard ID not found. Please log in again.");

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return (false, "You are offline. Please try again once you are back online.");

            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SetGuardNotificationReadStatus";
                var request = new
                {
                    Id = notificationId,
                    GuardId = guardId,
                    ClientSiteId = CurrentClientSiteId,
                    IsRead = isRead
                };

                var response = await _httpClient.PostAsJsonAsync(apiUrl, request);
                if (!response.IsSuccessStatusCode)
                    return (false, "Unable to update the notification. Please try again.");

                var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
                return (result?.isSuccess ?? false, result?.message ?? string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Unable to update the notification. {ex.Message}");
            }
        }

        public async Task<(bool isSuccess, string message)> MarkAllAsReadAsync()
        {
            var guardId = CurrentGuardId;
            if (guardId <= 0)
                return (false, "Guard ID not found. Please log in again.");

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return (false, "You are offline. Please try again once you are back online.");

            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/MarkAllGuardNotificationsRead";
                var request = new { GuardId = guardId, ClientSiteId = CurrentClientSiteId };

                var response = await _httpClient.PostAsJsonAsync(apiUrl, request);
                if (!response.IsSuccessStatusCode)
                    return (false, "Unable to update the notifications. Please try again.");

                var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
                return (result?.isSuccess ?? false, result?.message ?? string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Unable to update the notifications. {ex.Message}");
            }
        }
    }
}
