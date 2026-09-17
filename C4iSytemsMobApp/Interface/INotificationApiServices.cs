using C4iSytemsMobApp.Models;

namespace C4iSytemsMobApp.Interface
{
    public interface INotificationApiServices
    {
        Task<(bool isSuccess, string message, List<GuardNotification> notifications)> GetNotificationsAsync();

        Task<int> GetUnreadCountAsync();

        Task<(bool isSuccess, string message)> SetReadStatusAsync(int notificationId, bool isRead);

        Task<(bool isSuccess, string message)> MarkAllAsReadAsync();
    }
}
