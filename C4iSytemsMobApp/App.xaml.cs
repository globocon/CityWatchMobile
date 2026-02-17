using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using System.Reflection;
//using CommunityToolkit.Mvvm.Messaging;

namespace C4iSytemsMobApp
{
    public partial class App : Application
    {
        public static bool IsVolumeControlEnabledForCounter { get; set; } = false;        
        public static PatrolTouringMode TourMode { get; set; } = PatrolTouringMode.STND;
        public static int? PcarInspLastScannedSiteId { get; set; } = null;
        public static DateTime? PcarInspLastScannedTime { get; set; } = null;
        public static bool IsOnline { get; private set; }
        // Global event that any page/VM can subscribe to
        public static event Action<bool>? ConnectivityChangedEvent;
               
        public static string CurrentAppVersion { get; private set; }

        public App(AppUpgradePage upgradePage, ConnectivityListener connectivityListener)
        {
            InitializeComponent();

            // Initialize current status
            IsOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

            CurrentAppVersion = GetAppVersion();

            // Listen for changes
            Connectivity.ConnectivityChanged += (s, e) =>
            {
                IsOnline = e.NetworkAccess == NetworkAccess.Internet;
                //var service = IPlatformApplication.Current.Services.GetService<ISyncApiService>();
                //(service as SyncApiService)?.ResetClient();
                //// Notify UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ConnectivityChangedEvent?.Invoke(IsOnline);
                });
            };

            // Load saved theme preference or default to dark
            string savedTheme = Preferences.Get("AppTheme", "Dark"); // default is Dark
            Application.Current.UserAppTheme = savedTheme == "Dark" ? AppTheme.Dark : AppTheme.Light;
            MainPage = new NavigationPage(upgradePage);

            // RUN STARTUP SYNC AFTER APP LOADS
            Task.Run(async () =>
            {
                try
                {
                    await connectivityListener.CheckAndSyncOnStartupAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Startup sync failed: " + ex.Message);
                }
            });
        }

        private string GetAppVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "1.28.1";
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
