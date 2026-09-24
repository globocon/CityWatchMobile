using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
//using UIKit;


namespace C4iSytemsMobApp;

public partial class SOPPage : ContentPage
{
    public ObservableCollection<StaffDocument> StaffDocuments { get; set; } = new ObservableCollection<StaffDocument>();

    /// <summary>The site the guard logged in at - the fallback when no tag has been scanned.</summary>
    private int? _loginClientSiteId;

    public SOPPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Kick off async logic
        _ = InitializePageAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        /* The scanned site has a 30-minute life (App.StartOrResetPcarExpiryTimer). When it
           runs out the guard is no longer considered to be at that site, so the procedures on
           screen are no longer the ones that apply - this page has to follow that, not keep
           showing a site the app has already let go of.

           Subscribed here and released in OnDisappearing. The home page subscribes to this
           same event and never releases it; not copying that, because this page is discarded
           and rebuilt on every visit and a handler left behind would fire against a dead
           page, once per visit ever made. */
        App.PcarInspTagResetEvent -= OnPcarInspTagReset;
        App.PcarInspTagResetEvent += OnPcarInspTagReset;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        App.PcarInspTagResetEvent -= OnPcarInspTagReset;
    }

    private void OnPcarInspTagReset()
    {
        // App raises this from a timer callback, so hop to the UI thread before touching
        // the label or the bound collection.
        MainThread.BeginInvokeOnMainThread(async () => await ShowSopsForCurrentSiteAsync(tagExpired: true));
    }

    private async Task InitializePageAsync()
    {
        var clientSiteId = await TryGetSecureId("SelectedClientSiteId", "Please select a valid Client Site.");
        if (!clientSiteId.HasValue)
            return;

        _loginClientSiteId = clientSiteId.Value;

        await ShowSopsForCurrentSiteAsync(tagExpired: false);
    }

    /// <summary>
    /// Resolves the site the guard is currently at and shows its SOPs. Used both when the page
    /// opens and when the scanned site expires underneath it, so the two cannot drift apart.
    /// </summary>
    /// <param name="tagExpired">
    /// True when this refresh was caused by the 30-minute expiry rather than by opening the
    /// page. The reason then goes in the site line instead of a dialog: the guard may be
    /// mid-way through reading, and a modal thrown over the top of that is worse than a
    /// heading that explains itself.
    /// </param>
    private async Task ShowSopsForCurrentSiteAsync(bool tagExpired)
    {
        if (!_loginClientSiteId.HasValue)
            return;

        var sopClientSiteId = GetLocalSiteForPCAR(_loginClientSiteId.Value);

        ShowLocalSiteName(sopClientSiteId, tagExpired);

        await LoadStaffDocuments(sopClientSiteId, tagExpiredRefresh: tagExpired);
    }

    /// <summary>
    /// The site whose SOPs should be shown.
    ///
    /// SOP documents are configured per client site. On a standard tour that is the site the
    /// guard logged in at, which is why this page worked there. On a patrol car or inspection
    /// tour the guard logs in against the car's base site and then moves between sites, so the
    /// login site is not where they are - and its SOPs are usually empty, which is why the page
    /// appeared to have nothing in it. The site they last scanned is where they actually are.
    ///
    /// Same rule as GetLocalSiteForPCAR on the log activity page and the tag-status lookups on
    /// the home page, including the fallback: no scan yet (or the 30-minute expiry has cleared
    /// it) falls back to the login site rather than showing nothing.
    /// </summary>
    private static int GetLocalSiteForPCAR(int loginClientSiteId)
    {
        //If PCAR then change local client site to latest scanned site
        if (App.TourMode != PatrolTouringMode.PCAR && App.TourMode != PatrolTouringMode.INSP)
            return loginClientSiteId;

        return App.PcarInspLastScannedSiteId.HasValue && App.PcarInspLastScannedSiteId.Value > 0
            ? App.PcarInspLastScannedSiteId.Value
            : loginClientSiteId;
    }

    /// <summary>
    /// On a patrol car or inspection tour the SOPs on screen change as the guard scans, so the
    /// page has to say which site they belong to. Silent on a standard tour, where it is always
    /// the login site.
    /// </summary>
    private void ShowLocalSiteName(int clientSiteId, bool tagExpired)
    {
        if (App.TourMode != PatrolTouringMode.PCAR && App.TourMode != PatrolTouringMode.INSP)
            return;

        try
        {
            /* Expiry has already cleared the scanned site, so clientSiteId is back to the
               login site here - naming it would tell the guard they are somewhere they are
               not. Say what happened instead. */
            if (tagExpired && !(App.PcarInspLastScannedSiteId.HasValue && App.PcarInspLastScannedSiteId.Value > 0))
            {
                SopSiteName.Text = "SOP: site tag expired - scan a site tag";
                SopSiteName.IsVisible = true;
                return;
            }

            var scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
            var siteName = scannerControlServices?.GetClientSiteNameFromLocalDbNonAsync(clientSiteId);

            SopSiteName.Text = string.IsNullOrWhiteSpace(siteName) ? "SOP: current site" : $"SOP: {siteName}";
            SopSiteName.IsVisible = true;
        }
        catch (Exception ex)
        {
            // A missing site name must not stop the documents loading.
            Console.WriteLine($"Failed to resolve SOP site name: {ex.Message}");
        }
    }

    private async Task<int?> TryGetSecureId(string key, string errorMessage)
    {
        string idString = Preferences.Get(key,"");

        if (string.IsNullOrWhiteSpace(idString) || !int.TryParse(idString, out int id) || id <= 0)
        {
            await DisplayAlert("Validation Error", errorMessage, "OK");
            return null;
        }

        return id;
    }

    private async Task LoadStaffDocuments(int clientSiteId, bool tagExpiredRefresh = false)
    {
        /* Refreshing because the scanned site expired: drop the old site's documents before
           fetching, not after. If the fetch then fails - no signal in a car park is the normal
           case here - the list would otherwise keep showing procedures for a site the guard has
           left, under a heading saying the tag expired. Showing none is the safer of the two. */
        if (tagExpiredRefresh)
            StaffDocuments.Clear();

        try
        {
            var httpClient = new HttpClient();
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetStaffDocumentSOP?clientSiteId={clientSiteId}";
            var documents = await httpClient.GetFromJsonAsync<List<StaffDocument>>(url);

            StaffDocuments.Clear();

            if (documents == null || documents.Count == 0)
            {
                // The expiry refresh has already said its piece in the site line above.
                if (tagExpiredRefresh)
                    return;

                /* On a patrol car or inspection tour with nothing scanned yet, the site queried
                   is the car's base site, which normally holds no SOPs - so "no documents for
                   this site" is true but tells the guard the wrong thing. Say what would
                   actually get them the documents. */
                var isPcarWithoutScan =
                    (App.TourMode == PatrolTouringMode.PCAR || App.TourMode == PatrolTouringMode.INSP)
                    && !(App.PcarInspLastScannedSiteId.HasValue && App.PcarInspLastScannedSiteId.Value > 0);

                var message = isPcarWithoutScan
                    ? "No SOP documents found. Scan the site tag first - on a patrol car or inspection tour the SOPs shown are the ones for the site you last scanned."
                    : "No SOP documents found for this site.";

                await Application.Current.MainPage.DisplayAlert("Info", message, "OK");
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


                Uri uri = new Uri(fileUrl);
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);

                //var httpClient = new HttpClient();
                //var bytes = await httpClient.GetByteArrayAsync(new Uri(fileUrl));
                //var fileName = Path.GetFileName(fileUrl);
                //var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                //await File.WriteAllBytesAsync(filePath, bytes);

                //await DisplayAlert("Downloaded", $"File saved to: {filePath}", "OK");

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