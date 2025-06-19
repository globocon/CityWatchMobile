using C4iSytemsMobApp.Interface;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
//using UIKit;


namespace C4iSytemsMobApp;

public partial class SOPPage : ContentPage
{
    public ObservableCollection<StaffDocument> StaffDocuments { get; set; } = new ObservableCollection<StaffDocument>();

    public SOPPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Kick off async logic
        _ = InitializePageAsync();
    }

    private async Task InitializePageAsync()
    {
        var clientSiteId = await TryGetSecureId("SelectedClientSiteId", "Please select a valid Client Site.");
        if (clientSiteId.HasValue)
        {
            await LoadStaffDocuments(clientSiteId.Value);
        }
    }

    private async Task<int?> TryGetSecureId(string key, string errorMessage)
    {
        string idString = await SecureStorage.GetAsync(key);

        if (string.IsNullOrWhiteSpace(idString) || !int.TryParse(idString, out int id) || id <= 0)
        {
            await DisplayAlert("Validation Error", errorMessage, "OK");
            return null;
        }

        return id;
    }

    private async Task LoadStaffDocuments(int clientSiteId)
    {
        try
        {
            var httpClient = new HttpClient();
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetStaffDocumentSOP?clientSiteId={clientSiteId}";
            var documents = await httpClient.GetFromJsonAsync<List<StaffDocument>>(url);

            StaffDocuments.Clear();

            if (documents == null || documents.Count == 0)
            {
                await Application.Current.MainPage.DisplayAlert("Info", "No SOP documents found for this site.", "OK");
                return;
            }

            foreach (var doc in documents)
            {
                doc.FilePath = $"https://cws-ir.com/StaffDocs/{Uri.EscapeDataString(doc.FileName)}";
                StaffDocuments.Add(doc);
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
                var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(new Uri(fileUrl));
                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                await File.WriteAllBytesAsync(filePath, bytes);

                await DisplayAlert("Downloaded", $"File saved to: {filePath}", "OK");

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

    private void OnBackClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService,true);
    }

    private void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService, true);
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