using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using System.Reflection;
//using CommunityToolkit.Mvvm.Messaging;

namespace C4iSytemsMobApp
{
    public partial class App : Application
    {
        public static bool IsVolumeControlEnabledForCounter { get; set; } = false;        
        public static PatrolTouringMode TourMode { get; set; } = PatrolTouringMode.STND;
        public static bool IsOnline { get; private set; }
        // Global event that any page/VM can subscribe to
        public static event Action<bool>? ConnectivityChangedEvent;
               
        public static string CurrentAppVersion { get; private set; }

        public App(AppUpgradePage upgradePage)
        {
            InitializeComponent();

            // Initialize current status
            IsOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

            CurrentAppVersion = GetAppVersion();

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
            MainPage = new NavigationPage(upgradePage);
        }

        private string GetAppVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "1.28.1";
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
