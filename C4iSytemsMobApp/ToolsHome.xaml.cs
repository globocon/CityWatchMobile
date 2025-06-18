using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

public partial class ToolsHome : ContentPage
{
	public ToolsHome()
	{
		InitializeComponent();
	}

    private async void OnButtonClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {

            string buttonText = btn.Text;
            Application.Current.MainPage = new ToolsPage(buttonText);
          
           
        }
    }


    private void OnBackClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService, true);
        //Application.Current.MainPage = new MainPage(true);
    }

    private void OnHomeClicked(object sender, EventArgs e)
    {
        // Navigate to your app's home page
        // Example:
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService, true);
        //Application.Current.MainPage = new MainPage();
    }
}