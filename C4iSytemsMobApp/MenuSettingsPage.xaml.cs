using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

public partial class MenuSettingsPage : ContentPage
{
	public MenuSettingsPage()
	{
		InitializeComponent();
	}
           
    private void OnBackClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService, true);
        //Application.Current.MainPage = new MainPage(true);
    }


    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        var np = new MainPage(volumeButtonService, true);
        np._shouldOpenDrawerOnReturn = false;
        Application.Current.MainPage = np;                
    }

    private void OnCheckForUpdatesClicked(object sender, EventArgs e)
    {
        // Your update check logic here
        DisplayAlert("Check for Updates", "You are running the latest version.", "OK");
    }

    private async void OnAddTagsClicked(object sender, EventArgs e)
    {
        //Application.Current.MainPage = new DownloadsPage(3);
        DisplayAlert("Add Tags", "New feature coming soon...", "OK");

    }

    private async void OnAboutC4iClicked(object sender, EventArgs e)
    {
        try
        {
            Uri uri = new Uri("https://www.c4isystem.com");
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Exception", $"Could not launch URL: {ex.Message}", "OK");
            // An unexpected error occurred. No browser may be installed on the device.
        }
    }
}