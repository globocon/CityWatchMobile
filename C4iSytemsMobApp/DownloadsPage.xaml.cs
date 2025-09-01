using C4iSytemsMobApp.Interface;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Windows.Input;

namespace C4iSytemsMobApp;


public partial class DownloadsPage : ContentPage
{
    public ObservableCollection<StaffDocument> StaffDocuments { get; set; } = new();
    public DownloadsPage(int type)
    {
        InitializeComponent();
        BindingContext = this;
        // Set the heading
        TitleLabel.Text = GetHeadingFromType(type);

        LoadStaffDocuments(type);
    }

    private string GetHeadingFromType(int type)
    {
        return type switch
        {
            1 => "Company SOP's",
            2 => "C4i Training",
            3 => "Forms & Templates",
            _ => "Documents"
        };
    }

    private async void LoadStaffDocuments(int type)
    {
        try
        {
            var httpClient = new HttpClient();
            var query = "";
            var userId = await SecureStorage.GetAsync("UserId");
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetStaffDocuments?type={type}&UserId={userId}&query={Uri.EscapeDataString(query)}";

            var documents = await httpClient.GetFromJsonAsync<List<StaffDocument>>(url);

            if (documents != null)
            {
                StaffDocuments.Clear();
                foreach (var doc in documents)
                {
                    doc.FilePath = $"https://cws-ir.com/StaffDocs/{Uri.EscapeDataString(doc.FileName)}";
                    StaffDocuments.Add(doc);
                }
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load documents: {ex.Message}", "OK");
        }
    }


    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        if (sender is ImageButton button && button.CommandParameter is string fileUrl && !string.IsNullOrWhiteSpace(fileUrl))
        {
            try
            {


                Uri uri = new Uri(fileUrl);
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
                //var httpClient = new HttpClient();
                //var bytes = await httpClient.GetByteArrayAsync(new Uri(fileUrl));
                //var fileName = Path.GetFileName(fileUrl);
                //var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                //await File.WriteAllBytesAsync(filePath, bytes);

                //await DisplayAlert("Downloaded", $"File saved to: {filePath}", "OK");

                //// Launch the file with default viewer
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

        Application.Current.MainPage = new DownloadsHome();
       
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
        //Application.Current.MainPage = new MainPage();
    }



    public class StaffDocument
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ClientSiteName { get; set; }
        public string ClientTypeName { get; set; }
        public string FormattedLastUpdated { get; set; }
    }

}
