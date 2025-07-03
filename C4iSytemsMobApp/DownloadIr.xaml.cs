using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

public partial class DownloadIr : ContentPage
{
    private readonly string _downloadUrl;
    public DownloadIr(string downloadUrl)
	{
        InitializeComponent();
        _downloadUrl = downloadUrl;

        
    }
    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_downloadUrl))
        {
            try
            {
                var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(new Uri(_downloadUrl));
                var fileName = Path.GetFileName(_downloadUrl);
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                await File.WriteAllBytesAsync(filePath, bytes);

                await DisplayAlert("Downloaded", $"File saved to: {filePath}", "OK");

                // Launch the file with the default viewer
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
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

}