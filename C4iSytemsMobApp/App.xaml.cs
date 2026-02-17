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
        public static bool IsOnline { get; private set; }
        // Global event that any page/VM can subscribe to
        public static event Action<bool>? ConnectivityChangedEvent;
        public static string CurrentAppVersion { get; private set; }
        public static int? PcarInspLastScannedSiteId { get; set; } = null;

        private static DateTime? _pcarInspLastScannedTime;
        public static DateTime? PcarInspLastScannedTime
        {
            get => _pcarInspLastScannedTime;
            set
            {
                var previous = _pcarInspLastScannedTime;
                _pcarInspLastScannedTime = value;

                // If changed from null → value OR value changed → reset timer
                if (value != null)
                {
                    StartOrResetPcarExpiryTimer();
                }
                else
                {
                    StopPcarExpiryTimer();
                }
            }
        }
        private static System.Threading.Timer? _pcarExpiryTimer;

        public static event Action? PcarInspTagResetEvent;


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

        private static void StartOrResetPcarExpiryTimer()
        {
            StopPcarExpiryTimer();

            if (_pcarInspLastScannedTime == null)
                return;

            if (TourMode == PatrolTouringMode.STND)
                return;

            var elapsed = DateTime.UtcNow - _pcarInspLastScannedTime.Value.ToUniversalTime();
            var remaining = TimeSpan.FromMinutes(30) - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                TriggerPcarExpiry();
                return;
            }

            _pcarExpiryTimer = new System.Threading.Timer(_ =>
            {
                TriggerPcarExpiry();
            },
            null,
            remaining,
            Timeout.InfiniteTimeSpan);
        }

        private static void StopPcarExpiryTimer()
        {
            _pcarExpiryTimer?.Dispose();
            _pcarExpiryTimer = null;
        }
        private static void TriggerPcarExpiry()
        {
            if (TourMode == PatrolTouringMode.STND)
                return;

            if (_pcarInspLastScannedTime == null)
                return;

            PcarInspLastScannedSiteId = null;
            _pcarInspLastScannedTime = null;

            StopPcarExpiryTimer();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PcarInspTagResetEvent?.Invoke();
            });
        }

        protected override void OnSleep()
        {
            StopPcarExpiryTimer();
            base.OnSleep();
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
