using C4iSytemsMobApp.Interface;
using CommunityToolkit.Maui.Views;
using C4iSytemsMobApp.Views;

namespace C4iSytemsMobApp;

public partial class MenuSettingsPage : ContentPage
{

    private readonly IScannerControlServices _scannerControlServices;
    public MenuSettingsPage()
	{
		InitializeComponent();
        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
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

        //Check if guard have access to add tag
        var guardId = await TryGetSecureId("GuardId", "Guard ID not found. Please validate the License Number first.") ?? 0;
        var guardHasAccess = await _scannerControlServices.CheckIfGuardHasTagAddAccess(guardId.ToString());
        if (!guardHasAccess)
        {
            await DisplayAlert("Security", "Access Denied. Please contact administrator.", "OK");
            return;
        }

        //DisplayAlert("Add Tags", "New feature coming soon...", "OK");

        var popup = new AddDevicePopup();
        var result = await this.ShowPopupAsync(popup);

        if (result is string action)
        {
            //if (!(Application.Current.MainPage is NavigationPage))
            //    Application.Current.MainPage = new NavigationPage(Application.Current.MainPage);

            switch (action)
            {
                case "NFC":
                    Application.Current.MainPage = new AddNFCtag();      
                    //await Application.Current.MainPage.Navigation.PushAsync(new AddNFCtag());
                    break;
                case "IBeacon":
                    Application.Current.MainPage = new AddiBeacon();
                    //await Application.Current.MainPage.Navigation.PushAsync(new AddiBeacon());
                    break;
                case "Cancel":
                    // Just close silently
                    break;
            }
        }

    }

    private async void OnSelectSmartWandClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new SelectSmartWand();
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

    private async Task<int?> TryGetSecureId(string key, string errorMessage)
    {
        string idString = Preferences.Get(key, "");

        if (string.IsNullOrWhiteSpace(idString) || !int.TryParse(idString, out int id) || id <= 0)
        {
            await DisplayAlert("Validation Error", errorMessage, "OK");
            return 0;
        }

        return id;
    }
}