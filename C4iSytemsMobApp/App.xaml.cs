namespace C4iSytemsMobApp
{
    public partial class App : Application
    {
        public App(LoginPage loginPage)
        {
            InitializeComponent();

            // Set MainPage to LoginPage initially
            MainPage = new NavigationPage(loginPage);

        }
    }
}
