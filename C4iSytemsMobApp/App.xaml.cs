namespace C4iSytemsMobApp
{
    public partial class App : Application
    {
        public App(LoginPage loginPage)
        {
            InitializeComponent();
            // Load saved preference or default to dark
            // Load saved theme preference
            string savedTheme = Preferences.Get("AppTheme", "Dark"); // default is Dark
            Application.Current.UserAppTheme = savedTheme == "Dark" ? AppTheme.Dark : AppTheme.Light;
            // Set MainPage to LoginPage initially
            MainPage = new NavigationPage(loginPage);

        }
    }
}
