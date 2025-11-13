using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp
{
    public partial class App : Application
    {
        public static bool IsVolumeControlEnabledForCounter { get; set; } = false;
        private readonly IAppUpdateService _appUpdateService;
        
        
        //public App(LoginPage loginPage)
        //{
        //    InitializeComponent();
        //    // Load saved preference or default to dark
        //    // Load saved theme preference
        //    string savedTheme = Preferences.Get("AppTheme", "Dark"); // default is Dark
        //    Application.Current.UserAppTheme = savedTheme == "Dark" ? AppTheme.Dark : AppTheme.Light;
        //    // Set MainPage to LoginPage initially
        //    MainPage = new NavigationPage(loginPage);

        //}

        public App(LoginPage loginPage, IAppUpdateService appUpdateService)
        {
            InitializeComponent();
          
            // Load saved theme preference or default to dark
            string savedTheme = Preferences.Get("AppTheme", "Dark"); // default is Dark
            Application.Current.UserAppTheme = savedTheme == "Dark" ? AppTheme.Dark : AppTheme.Light;
            _appUpdateService = appUpdateService;
            MainPage = new NavigationPage(loginPage);
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


        }
    }
}
