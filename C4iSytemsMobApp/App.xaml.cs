using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
//using CommunityToolkit.Mvvm.Messaging;

namespace C4iSytemsMobApp
{
    public partial class App : Application
    {
        public static bool IsVolumeControlEnabledForCounter { get; set; } = false;
        private readonly IAppUpdateService _appUpdateService;
        public static PatrolTouringMode TourMode { get; set; } = PatrolTouringMode.STND;
        public static bool IsOnline { get; private set; }
        // Global event that any page/VM can subscribe to
        public static event Action<bool>? ConnectivityChangedEvent;
               

        public App(LoginPage loginPage, IAppUpdateService appUpdateService)
        {
            InitializeComponent();

            // Initialize current status
            IsOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

            // Listen for changes
            Connectivity.ConnectivityChanged += (s, e) =>
            {
                IsOnline = e.NetworkAccess == NetworkAccess.Internet;
                // Notify UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ConnectivityChangedEvent?.Invoke(IsOnline);
                });
            };

            // Load saved theme preference or default to dark
            string savedTheme = Preferences.Get("AppTheme", "Dark"); // default is Dark
            Application.Current.UserAppTheme = savedTheme == "Dark" ? AppTheme.Dark : AppTheme.Light;
            _appUpdateService = appUpdateService;
            MainPage = new NavigationPage(loginPage);
#if !DEBUG
            // Check for updates after UI is ready
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(1500); // give a moment for UI to load
                bool updateAvailable = await _appUpdateService.CheckForUpdateAsync();
                if (!updateAvailable)
                {
                    // Redirect to login page
                    MainPage = new NavigationPage(loginPage);
                }
            });
#endif

        }
    }

    public class ConnectivityMessage
    {
        public bool IsOnline { get; }

        public ConnectivityMessage(bool isOnline)
        {
            IsOnline = isOnline;
        }
    }

    public static class SyncState
    {
        private static int _syncedCount;
        private static string? _syncingStatus = " ";

        public static int SyncedCount
        {
            get => _syncedCount;
            set
            {
                if (_syncedCount != value)
                {
                    _syncedCount = value;
                    SyncedCountChanged?.Invoke(null, value);
                }
            }
        }

        // Event raised when synced count changes
        public static event EventHandler<int> SyncedCountChanged;

        public static string SyncingStatus
        {
            get => _syncingStatus;
            set
            {
                if (_syncingStatus != value)
                {
                    _syncingStatus = value;
                    SyncingStatusChanged?.Invoke(null, value);
                }
            }
        }

        // Event raised when Syncing Status changes
        public static event EventHandler<string> SyncingStatusChanged;
    }

}
