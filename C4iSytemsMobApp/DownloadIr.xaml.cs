using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;

namespace C4iSytemsMobApp;

public partial class DownloadIr : ContentPage
{
    private readonly string _downloadUrl;    
    private string _thirdPartyDomain;
    private bool _isOffLineIr;
    public DownloadIr(string downloadUrl,string thirdPartyDomin, bool isOffLineIr)
	{
        NavigationPage.SetHasNavigationBar(this, false);
        InitializeComponent();
        _downloadUrl = downloadUrl;
        _thirdPartyDomain = thirdPartyDomin;
        _isOffLineIr = isOffLineIr;

        string hqName = !string.IsNullOrWhiteSpace(_thirdPartyDomain) ? _thirdPartyDomain : "CityWatch HQ";

        ThankYouLabel.Text = $"Thank you! Your report has been emailed to {hqName} and, if needed, to the client as well.";

        if(_isOffLineIr)
        {
            DownloadButton.IsVisible = false;
            DownloadButton.IsEnabled = false;
            ThankYouLabel.Text = "Thank you! Your report has been saved locally and will be uploaded when connectivity is restored.";
            ResultInfoLabel.Text = "Incident Report Saved In Cache";
        }

    }
    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_downloadUrl))
        {
            try
            {

                Uri uri = new Uri(_downloadUrl);
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);

                //var httpClient = new HttpClient();
                //var bytes = await httpClient.GetByteArrayAsync(new Uri(_downloadUrl));
                //var fileName = Path.GetFileName(_downloadUrl);
                //var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                //await File.WriteAllBytesAsync(filePath, bytes);

                //await DisplayAlert("Downloaded", $"File saved to: {filePath}", "OK");

                //// Launch the file with the default viewer
                //await Launcher.Default.OpenAsync(new OpenFileRequest
                //{
                //    File = new ReadOnlyFile(filePath)
                //});
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to download: {ex.Message}", "OK");
            }
        }
        else
        {
            await DisplayAlert("Error", "Invalid file URL", "OK");
        }
    }



    private async void OnBackClicked(object sender, EventArgs e)
    {

        Application.Current.MainPage = new WebIncidentReport();

    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);

        //Application.Current.MainPage = new MainPage();
    }


    private async void OnReuseClicked(object sender, EventArgs e)
    {
        var lastRequest = IRPreferenceHelper.Load();

        if (lastRequest == null)
        {
            await DisplayAlert("No Data", "No previous IR found to reuse.", "OK");
            return;
        }

        // Reset values that must be new each time
        lastRequest.Id = 0;
        lastRequest.ReportReference = Guid.NewGuid().ToString();
        lastRequest.ReportedBy = string.Empty;

        // Navigate to form page with prefilled data
        Application.Current.MainPage = new NavigationPage(new WebIncidentReport(lastRequest));
    }


}