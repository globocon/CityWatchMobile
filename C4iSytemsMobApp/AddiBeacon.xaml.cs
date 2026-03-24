using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

public partial class AddiBeacon : ContentPage
{
    public AddiBeacon()
    {
        InitializeComponent();
    }

    private void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService, true);
        //Application.Current.MainPage = new MainPage(true);
    }
}