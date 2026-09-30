using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Enums;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Plugin.NFC;
using System.ComponentModel;
using System.Diagnostics;

namespace C4iSytemsMobApp;

public partial class AddNFCtag : ContentPage, INotifyPropertyChanged
{
    //public event PropertyChangedEventHandler PropertyChanged;
    public const string ALERT_TITLE = "NFC";
    bool _eventsAlreadySubscribed = false;
    private readonly IScannerControlServices _scannerControlServices;
    private bool _isNfcEnabledForSite = false;
    bool _isDeviceiOS = false;
    string _scannedTagUid = string.Empty;
    public bool DeviceIsListening
    {
        get => _deviceIsListening;
        set
        {
            _deviceIsListening = value;
            OnPropertyChanged(nameof(DeviceIsListening));
        }
    }
    private bool _deviceIsListening;
    private bool _nfcIsEnabled;
    public bool NfcIsEnabled
    {
        get => _nfcIsEnabled;
        set
        {
            _nfcIsEnabled = value;
            OnPropertyChanged(nameof(NfcIsEnabled));
            OnPropertyChanged(nameof(NfcIsDisabled));
        }
    }

    public bool NfcIsDisabled => !NfcIsEnabled;
    //protected override void OnPropertyChanged(string propertyName)
    //{
    //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    //}
    public AddNFCtag()
    {
        InitializeComponent();
        BindingContext = this;
        NavigationPage.SetHasNavigationBar(this, false);
        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        /* The scanned site expires after 30 minutes (App.StartOrResetPcarExpiryTimer). A guard
           can easily still be on this page when that happens - scan a tag, get interrupted,
           come back and press Save - so the site line has to keep up, otherwise it would be
           telling them the tag goes somewhere it no longer would. Released in OnDisappearing;
           the home page leaks this subscription, not copying that. */
        App.PcarInspTagResetEvent -= OnPcarInspTagReset;
        App.PcarInspTagResetEvent += OnPcarInspTagReset;

        await ShowLocalSiteNameAsync();

        await StartNFC();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        App.PcarInspTagResetEvent -= OnPcarInspTagReset;

        _scannedTagUid = string.Empty;
        if (_isNfcEnabledForSite && CrossNFC.IsSupported && CrossNFC.Current.IsAvailable)
        {
            await StopListening();
        }
    }

    private void OnPcarInspTagReset()
    {
        // Raised from a timer callback in App, so hop to the UI thread before touching labels.
        MainThread.BeginInvokeOnMainThread(async () => await ShowLocalSiteNameAsync());
    }

    /// <summary>
    /// The site a tag registered here belongs to.
    ///
    /// On a standard tour that is the site the guard logged in at. On a patrol car or
    /// inspection tour the guard logs in against the car's base site and then moves between
    /// sites, so the login site is not where they are - the site they last scanned is. Same
    /// rule as GetLocalSiteForPCAR on the log activity page, the tag-status lookups on the
    /// home page, and the SOP page.
    ///
    /// Returns null on a PCAR/INSP tour with no live scanned site, where the other pages fall
    /// back to the login site. That fallback is right for reading and wrong here: this writes,
    /// and the row it writes is permanent. Silently registering a tag against the patrol car's
    /// base site would put it on a site it does not belong to, and the guard would have no way
    /// of knowing. The caller refuses to save instead.
    /// </summary>
    private static int? GetLocalSiteForPCAR(int loginClientSiteId)
    {
        //If PCAR then change local client site to latest scanned site
        if (App.TourMode != PatrolTouringMode.PCAR && App.TourMode != PatrolTouringMode.INSP)
            return loginClientSiteId;

        return App.PcarInspLastScannedSiteId.HasValue && App.PcarInspLastScannedSiteId.Value > 0
            ? App.PcarInspLastScannedSiteId.Value
            : (int?)null;
    }

    /// <summary>
    /// Names the site a tag saved now would be registered against. Silent on a standard tour,
    /// where it is always the login site and saying so adds nothing.
    /// </summary>
    private async Task ShowLocalSiteNameAsync()
    {
        if (App.TourMode != PatrolTouringMode.PCAR && App.TourMode != PatrolTouringMode.INSP)
            return;

        try
        {
            int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out int loginClientSiteId);
            var siteId = GetLocalSiteForPCAR(loginClientSiteId);

            if (!siteId.HasValue)
            {
                LabelSiteName.Text = "No site scanned - scan a site tag before saving";
                LabelSiteName.TextColor = Colors.Red;
                LabelSiteName.IsVisible = true;
                return;
            }

            /* Resolve through the service, which falls back to the server when the site is not
               in the local cache. ClientSitesLocal is filled once at guard login, so a site the
               patrol car has driven to since is usually missing from it - which is what left
               this line showing a bare id. A number means nothing to a guard checking they are
               about to register a tag to the right site. */
            var siteName = _scannerControlServices == null
                ? string.Empty
                : await _scannerControlServices.GetClientSiteNameAsync(siteId.Value);

            LabelSiteName.Text = string.IsNullOrWhiteSpace(siteName)
                ? "Registering to: current site"
                : $"Registering to: {siteName}";
            LabelSiteName.TextColor = Color.FromArgb("#512bd4");
            LabelSiteName.IsVisible = true;
        }
        catch (Exception ex)
        {
            // A missing site name must not stop the page working.
            Debug.WriteLine($"Failed to resolve NFC tag site name: {ex.Message}");
        }
    }

    #region "NFC Methods"

    private async Task StartNFC()
    {
        // Check NFC status
        ////string isNfcEnabledForSiteLocalStored = Preferences.Get("NfcOnboarded", "");

        //if (!string.IsNullOrEmpty(isNfcEnabledForSiteLocalStored) && bool.TryParse(isNfcEnabledForSiteLocalStored, out _isNfcEnabledForSite))
        //{
        // In order to support Mifare Classic 1K tags (read/write), you must set legacy mode to true.
        CrossNFC.Legacy = false;

        if (CrossNFC.IsSupported)
        {
            if (CrossNFC.Current.IsAvailable)
            {
                NfcIsEnabled = CrossNFC.Current.IsEnabled;
                if (!NfcIsEnabled)
                {
                    await DisplayAlert(ALERT_TITLE, "NFC is disabled. Please enable NFC scanning.", "OK");
                    UpdateInfoLabel("Please enable NFC scanning...", true);
                }
                else
                {
                    UpdateInfoLabel("Tap an NFC tag to scan.", false);
                }

                if (DeviceInfo.Platform == DevicePlatform.iOS)
                    _isDeviceiOS = true;

                //await InitializeNFCAsync();
                await AutoStartAsync().ConfigureAwait(false);
            }
            else
            {
                UpdateInfoLabel("NFC scanning is not supported in your device...", true);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LabelTagUID.TextColor = Colors.Gray;
                    LabelTagInfo.TextColor = Colors.Gray;
                    txtTagLabel.IsEnabled = false; // Entry disables normally
                    frame_ButtonSave.Opacity = 0.5; // Dim the frame
                    frame_ButtonSave.InputTransparent = true; // Prevent tap
                    //frame_ButtonCancel.IsEnabled = false;
                });
               
            }
        }
        else
        {
            UpdateInfoLabel("NFC scanning is not supported in your device...", true);
        }
        //}

    }

    async Task AutoStartAsync()
    {
        // Some delay to prevent Java.Lang.IllegalStateException "Foreground dispatch can only be enabled when your activity is resumed" on Android
        await Task.Delay(500);
        await StartListeningIfNotiOS();
    }

    void SubscribeEvents()
    {
        if (_eventsAlreadySubscribed)
            UnsubscribeEvents();

        _eventsAlreadySubscribed = true;

        CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
        CrossNFC.Current.OnNfcStatusChanged += Current_OnNfcStatusChanged;
        CrossNFC.Current.OnTagListeningStatusChanged += Current_OnTagListeningStatusChanged;

        if (_isDeviceiOS)
            CrossNFC.Current.OniOSReadingSessionCancelled += Current_OniOSReadingSessionCancelled;
    }

    void UnsubscribeEvents()
    {
        CrossNFC.Current.OnMessageReceived -= Current_OnMessageReceived;
        CrossNFC.Current.OnNfcStatusChanged -= Current_OnNfcStatusChanged;
        CrossNFC.Current.OnTagListeningStatusChanged -= Current_OnTagListeningStatusChanged;

        if (_isDeviceiOS)
            CrossNFC.Current.OniOSReadingSessionCancelled -= Current_OniOSReadingSessionCancelled;

        _eventsAlreadySubscribed = false;
    }
    void Current_OnTagListeningStatusChanged(bool isListening) => DeviceIsListening = isListening;

    async void Current_OnNfcStatusChanged(bool isEnabled)
    {
        NfcIsEnabled = isEnabled;
        await DisplayAlert(ALERT_TITLE, $"NFC has been {(isEnabled ? "enabled" : "disabled")}.", "OK");
        if (isEnabled)
            UpdateInfoLabel("Tap an NFC tag to scan...", false);
        else
            UpdateInfoLabel("Please enable NFC scanning...", true);
    }

    async void Current_OnMessageReceived(ITagInfo tagInfo)
    {
        if (tagInfo == null)
        {
            await DisplayAlert(ALERT_TITLE, "No tag found", "OK");
            return;
        }

        var identifier = tagInfo.Identifier;
        var serialNumber = NFCUtils.ByteArrayToHexString(identifier, "");
        var title = !tagInfo.IsEmpty ? $"Tag Info: {tagInfo}" : "Tag Info";

        if (!tagInfo.IsSupported)
        {
            await DisplayAlert(ALERT_TITLE, "Unsupported NFC tag", "OK");
        }
        else if (!string.IsNullOrEmpty(serialNumber))
        {
            //await ShowToastMessage($"Tag scanned. Logging activity...");
            _scannedTagUid = serialNumber;
            UpdateInfoLabel($"Tag received - {serialNumber}",false);
            LabelTagUID.Text = $"UID: {_scannedTagUid}";
            await DisplayAlert(ALERT_TITLE, $"Tag received - {serialNumber}", "OK");
        }
        else
        {
            //var first = tagInfo.Records[0];
            //await DisplayAlert(ALERT_TITLE, GetMessage(first), "OK");
            await DisplayAlert(ALERT_TITLE, "Tag UID not found", "OK");
            return;
        }
    }

    void Current_OniOSReadingSessionCancelled(object sender, EventArgs e) => Debug.WriteLine("iOS NFC Session has been cancelled");

    async Task StartListeningIfNotiOS()
    {
        if (_isDeviceiOS)
        {
            SubscribeEvents();
            return;
        }
        await BeginListening();
    }

    async Task BeginListening()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SubscribeEvents();
                CrossNFC.Current.StartListening();
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert(ALERT_TITLE, ex.Message, "OK");
        }
    }

    async Task StopListening()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CrossNFC.Current.StopListening();
                UnsubscribeEvents();
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert(ALERT_TITLE, ex.Message, "OK");
        }
    }


    #endregion "NFC Methods"

    private async void OnSaveTagClicked(object sender, EventArgs e)
    {
        /* Checked before anything else: registering a tag goes straight to the API and has no
           offline cache behind it, unlike scans and logbook entries. There is nothing to be
           gained by validating the rest first and then telling the guard it was never going to
           save. */
        if (!App.IsOnline)
        {
            await DisplayAlert(ALERT_TITLE, "Tag cannot be registered in offline mode.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_scannedTagUid))
        {
            await DisplayAlert(ALERT_TITLE, "Please scan a tag.", "OK");
            return;
        }

        if (txtTagLabel.Text.Trim().Length <= 0)
        {
            await DisplayAlert(ALERT_TITLE, "Tag Label is required.", "OK");
            return;
        }

        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return;

        /* Register the tag against the site the guard is actually at, not the one they logged
           in at - on a patrol car or inspection tour those are different. Re-resolved here
           rather than reused from OnAppearing, because the 30-minute expiry may have fired
           while the guard was filling in the label. */
        var tagClientSiteId = GetLocalSiteForPCAR(clientSiteId);
        if (!tagClientSiteId.HasValue)
        {
            /* PCAR/INSP with no live scanned site. Saving anyway would register the tag to the
               patrol car's base site permanently, and nothing downstream would flag it as
               wrong, so refuse rather than guess. */
            await ShowLocalSiteNameAsync();
            await DisplayAlert(ALERT_TITLE,
                "No site scanned. On a patrol car or inspection tour a tag is registered to the site you last scanned - scan the site tag first, then save.",
                "OK");
            return;
        }

        var scannerSettings = await _scannerControlServices.SaveNFCTagInfoDetailsAsync(tagClientSiteId.Value.ToString(), _scannedTagUid, guardId.ToString(), userId.ToString(), txtTagLabel.Text);
        if (scannerSettings != null)
        {
            if (scannerSettings.IsSuccess)
            {
                await DisplayAlert(ALERT_TITLE, scannerSettings.message, "OK");
                //await ShowToastMessage(scannerSettings.message);
                _scannedTagUid = string.Empty;
                UpdateInfoLabel("Tap an NFC tag to scan...",false);
                LabelTagUID.Text = $"UID: {_scannedTagUid}";
                txtTagLabel.Text = "";
            }
            else
            {
                await DisplayAlert(ALERT_TITLE, scannerSettings.message, "OK");
            }
        }
        else
        {
            await DisplayAlert(ALERT_TITLE, scannerSettings?.message ?? "Unknown error", "OK");
        }        
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MenuSettingsPage();
    }

    private void UpdateInfoLabel(string message,bool IsError)
    {
        LabelInfo.Text = message;
        if(IsError)
            LabelInfo.TextColor = Colors.Red;
       else
            LabelInfo.TextColor = Colors.Green;
    }

    private async Task ShowToastMessage(string message)
    {
        await Toast.Make(message, ToastDuration.Long).Show();

    }
    private async Task<(int guardId, int clientSiteId, int userId)> GetSecureStorageValues()
    {
        int.TryParse(Preferences.Get("GuardId", "0"), out int guardId);
        int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out int clientSiteId);
        int.TryParse(Preferences.Get("UserId", "0"), out int userId);

        if (guardId <= 0)
        {
            await DisplayAlert("Error", "Guard ID not found. Please validate the License Number first.", "OK");
            return (-1, -1, -1);
        }
        if (clientSiteId <= 0)
        {
            await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
            return (-1, -1, -1);
        }
        if (userId <= 0)
        {
            await DisplayAlert("Validation Error", "User ID is invalid. Please log in again.", "OK");
            return (-1, -1, -1);
        }

        return (guardId, clientSiteId, userId);
    }
}