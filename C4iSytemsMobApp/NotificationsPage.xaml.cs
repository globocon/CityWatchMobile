using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System.Collections.ObjectModel;

namespace C4iSytemsMobApp;

/// <summary>
/// The Notifications tab. Cards are served by the API for the logged-in guard only — the
/// guard id is read from Preferences inside <see cref="INotificationApiServices"/> on every
/// call, so nothing here can outlive a logout.
///
/// Read/unread is stored server-side rather than locally: the guard may pick up a different
/// phone mid-shift, and what they have already read has to follow them.
/// </summary>
public partial class NotificationsPage : ContentPage
{
    private const string NoNotificationsMessage = "You have no current notifications";

    private readonly INotificationApiServices _notificationApiServices;

    /* Not Page.IsBusy: that one drives the platform's own activity indicator and is set by
       other things. This is the RefreshView's spinner and the re-entrancy guard. */
    private bool _isLoading;

    public ObservableCollection<GuardNotification> Notifications { get; } = new();

    public NotificationsPage(INotificationApiServices notificationApiServices)
    {
        InitializeComponent();
        _notificationApiServices = notificationApiServices;
        BindingContext = this;
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value)
                return;

            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    public bool HasNotifications => Notifications.Count > 0;

    public bool HasUnread => Notifications.Any(x => !x.IsRead);

    public string SummaryText
    {
        get
        {
            var unread = Notifications.Count(x => !x.IsRead);
            return unread == 0
                ? $"{Notifications.Count} notification(s), all read"
                : $"{unread} unread of {Notifications.Count}";
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadNotificationsAsync();
    }

    private async Task LoadNotificationsAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var (isSuccess, message, notifications) = await _notificationApiServices.GetNotificationsAsync();

            Notifications.Clear();
            foreach (var notification in notifications)
                Notifications.Add(notification);

            /* The empty view carries the reason when there is one. A guard who is offline or
               not logged in is looking at the same blank list as a guard with nothing due, and
               only one of those three is worth saying "you're all clear" about. */
            EmptyMessageLabel.Text = isSuccess || string.IsNullOrWhiteSpace(message)
                ? NoNotificationsMessage
                : message;

            RefreshHeader();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Card-level toggle. The server is the record, so the card only flips once the call has
    /// succeeded — an optimistic flip would leave the app disagreeing with the web portal
    /// after a failed request.
    /// </summary>
    private async void OnToggleReadClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not GuardNotification notification)
            return;

        var newState = !notification.IsRead;

        button.IsEnabled = false;
        try
        {
            var (isSuccess, message) = await _notificationApiServices.SetReadStatusAsync(notification.Id, newState);

            if (!isSuccess)
            {
                await DisplayAlert("Notifications", string.IsNullOrWhiteSpace(message)
                    ? "Unable to update the notification. Please try again."
                    : message, "OK");
                return;
            }

            notification.IsRead = newState;
            notification.ReadOn = newState ? DateTime.Now : null;
            RefreshHeader();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void OnMarkAllReadClicked(object sender, EventArgs e)
    {
        if (!HasUnread)
            return;

        var (isSuccess, message) = await _notificationApiServices.MarkAllAsReadAsync();

        if (!isSuccess)
        {
            await DisplayAlert("Notifications", string.IsNullOrWhiteSpace(message)
                ? "Unable to update the notifications. Please try again."
                : message, "OK");
            return;
        }

        foreach (var notification in Notifications)
        {
            notification.IsRead = true;
            notification.ReadOn ??= DateTime.Now;
        }

        RefreshHeader();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadNotificationsAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadNotificationsAsync();
    }

    private void RefreshHeader()
    {
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        GoHome();
    }

    private void OnHomeClicked(object sender, EventArgs e)
    {
        GoHome();
    }

    /* The home screen is a root page swap everywhere else in this app rather than a
       navigation stack pop, and the drawer this page is opened from is part of it — so Back
       and Home land in the same place. */
    private static void GoHome()
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
    }

    protected override bool OnBackButtonPressed()
    {
        GoHome();
        return true;
    }
}
