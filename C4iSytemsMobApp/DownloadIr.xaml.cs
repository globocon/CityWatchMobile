namespace C4iSytemsMobApp;

public partial class DownloadIr : ContentPage
{
    private readonly string _downloadUrl;
    public DownloadIr(string downloadUrl)
	{
        InitializeComponent();
        _downloadUrl = downloadUrl;

        
    }

    private void OnDownloadClicked(object sender, EventArgs e)
    {
        Browser.OpenAsync(_downloadUrl, BrowserLaunchMode.External);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {

        Application.Current.MainPage = new WebIncidentReport();

    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MainPage();
    }

}