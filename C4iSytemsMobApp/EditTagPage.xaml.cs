using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Services;
using C4iSytemsMobApp.Views;
using Plugin.NFC;
using System.Diagnostics;

namespace C4iSytemsMobApp;

/// <summary>
/// Edits the description of a tag that is already registered.
///
/// NFC only on this branch. On master this page serves beacons too - the two differ only in
/// how the UID is obtained, and everything after that is shared - but Bluetooth is not part of
/// the iOS build (a39cf66), so the beacon half is stripped here and MenuSettingsPage refuses
/// the Edit iBeacon option. The constructor still takes a ScanningType so the two branches
/// stay call-compatible.
///
/// Scanning works offline; saving does not. The lookup and the save both go straight to the
/// API with no offline cache behind them.
/// </summary>
public partial class EditTagPage : ContentPage
{
    private readonly ScanningType _mode;
    private readonly IScannerControlServices _scannerControlServices;
    private readonly ILogBookServices _logBookServices;

    /// <summary>The CrossNFC instance this page's handlers are attached to, or null.</summary>
    private INFC _subscribedNfc;
    private bool _isDeviceiOS;

    private TagEditInfo _tag;

    public EditTagPage(ScanningType mode)
    {
        InitializeComponent();
        BindingContext = this;
        NavigationPage.SetHasNavigationBar(this, false);

        _mode = mode;
        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();

        LabelTitle.Text = "Edit NFC Tag";

        _isDeviceiOS = DeviceInfo.Platform == DevicePlatform.iOS;
        SetTagPanelVisible(false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        /* The stamped logbook follows the scanned site on a PCAR/INSP tour, and that site
           expires after 30 minutes - so the line has to keep up while the guard is typing. */
        App.PcarInspTagResetEvent -= OnPcarInspTagReset;
        App.PcarInspTagResetEvent += OnPcarInspTagReset;

        await ShowLocalSiteNameAsync();
        ShowScanPrompt();

        await StartNfcAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        App.PcarInspTagResetEvent -= OnPcarInspTagReset;

        if (_subscribedNfc != null)
            StopNfc();
    }

    #region Site resolution and logbook

    /// <summary>
    /// The logbook the edit is stamped into. On a standard tour that is the site the guard
    /// logged in at; on a patrol car or inspection tour it is the site they last scanned,
    /// because the car moves between sites within one login. Same rule as GetLocalSiteForPCAR
    /// on the log activity page.
    ///
    /// Null on a PCAR/INSP tour with no live scanned site: the other pages fall back to the
    /// login site, which is right when the fallback only affects what you read. Here it would
    /// file an audit entry against the patrol car's base site, so the save is refused instead.
    /// </summary>
    private static int? GetLocalSiteForPCAR()
    {
        int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out int loginClientSiteId);

        if (App.TourMode != PatrolTouringMode.PCAR && App.TourMode != PatrolTouringMode.INSP)
            return loginClientSiteId > 0 ? loginClientSiteId : (int?)null;

        return App.PcarInspLastScannedSiteId.HasValue && App.PcarInspLastScannedSiteId.Value > 0
            ? App.PcarInspLastScannedSiteId.Value
            : (int?)null;
    }

    private void OnPcarInspTagReset() =>
        MainThread.BeginInvokeOnMainThread(async () => await ShowLocalSiteNameAsync());

    private async Task ShowLocalSiteNameAsync()
    {
        if (App.TourMode != PatrolTouringMode.PCAR && App.TourMode != PatrolTouringMode.INSP)
            return;

        try
        {
            var siteId = GetLocalSiteForPCAR();
            if (!siteId.HasValue)
            {
                LabelSiteName.Text = "No site scanned - scan a site tag before saving";
                LabelSiteName.TextColor = Colors.Red;
                LabelSiteName.IsVisible = true;
                return;
            }

            /* Resolve through the service, which falls back to the server when the site is not
               in the local cache. The cache is only filled at guard login, so a site the patrol
               car has driven to since is usually missing from it - which is what left this line
               showing a bare id. A number means nothing to a guard checking they are about to
               stamp the right logbook. */
            var siteName = _scannerControlServices == null
                ? string.Empty
                : await _scannerControlServices.GetClientSiteNameAsync(siteId.Value);

            LabelSiteName.Text = string.IsNullOrWhiteSpace(siteName)
                ? "LogBook: current site"
                : $"LogBook: {siteName}";
            LabelSiteName.TextColor = Color.FromArgb("#512bd4");
            LabelSiteName.IsVisible = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve edit-tag site name: {ex.Message}");
        }
    }

    /// <summary>
    /// The logbook wording for a description change. Names the tag by what it used to say, its
    /// HEX code, and what it says now, so the entry can be read years later without the tag in
    /// hand. Who made the change is on the entry already - the guard id is part of every
    /// logbook row - but it is repeated in the text because the printed report shows initials
    /// only.
    ///
    /// The guard's details come from the Guards table rather than from Preferences: this is an
    /// audit record, and Preferences hold whatever was cached at some earlier login, so a
    /// renamed or re-badged guard would be recorded under stale details permanently. A lookup
    /// that fails degrades the wording rather than blocking the edit - the entry is worth more
    /// than the name on it.
    /// </summary>
    private async Task<string> BuildLogbookEntryAsync(string oldDescription, string tagUid, string newDescription)
    {
        var who = "unknown user";

        try
        {
            var guard = await _scannerControlServices.GetGuardNameDetailsAsync();
            if (guard != null && guard.IsSuccess)
            {
                var name = guard.Name?.Trim();
                var initial = guard.Initial?.Trim();
                var securityNo = guard.SecurityNo?.Trim();

                // "Sam Sing [S.S 4]" - initials and security number identify the guard on the
                // printed report, where the full name is not shown.
                var badge = $"[{initial}]";

                who = $"{(string.IsNullOrWhiteSpace(name) ? "unknown user" : name)} {badge}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read guard details for logbook entry: {ex.Message}");
        }

        var before = string.IsNullOrWhiteSpace(oldDescription) ? "(no description)" : oldDescription.Trim();

        return $"Admin User Edited Database, {before} with HEX CODE {tagUid} is now " +
               $"{newDescription.Trim()}, user {who}.";
    }

    #endregion

    #region Lookup

    private void ShowScanPrompt()
    {
        UpdateInfoLabel("Tap the NFC tag you want to edit.", false);
    }

    /// <summary>
    /// Looks the scanned UID up and either opens it for editing or reports that it is unknown.
    /// Works offline only as far as the network allows - the lookup is a server call, so with
    /// no connection the guard is told to come back online rather than shown a blank form.
    /// </summary>
    private async Task LookupTagAsync(string tagUid)
    {
        if (string.IsNullOrWhiteSpace(tagUid))
            return;

        SetBusy(true);
        try
        {
            UpdateInfoLabel($"Checking {tagUid}...", false);

            var result = await _scannerControlServices.GetTagForEditAsync(tagUid, _mode);

            if (result == null || !result.IsSuccess)
            {
                UpdateInfoLabel(result?.message ?? "Unable to check the tag. Please try again.", true);
                return;
            }

            if (!result.tagFound)
            {
                // The warning the spec asks for, and the only outcome for an unregistered tag:
                // this page edits existing tags, it does not create them.
                _tag = null;
                SetTagPanelVisible(false);
                UpdateInfoLabel($"Tag not found in database ({tagUid}).", true);
                await DisplayAlert(LabelTitle.Text, "Tag not found in database.", "OK");
                return;
            }

            _tag = result;
            ShowTagForEditing();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowTagForEditing()
    {
        LabelTagSite.Text = string.IsNullOrWhiteSpace(_tag.ClientSiteName)
            ? $"Site {_tag.ClientSiteId}"
            : _tag.ClientSiteName;
        LabelTagUid.Text = _tag.UId;
        LabelCurrentDescription.Text = string.IsNullOrWhiteSpace(_tag.LabelDescription)
            ? "(no description)"
            : _tag.LabelDescription;
        txtNewDescription.Text = _tag.LabelDescription;

        SetTagPanelVisible(true);

        UpdateInfoLabel("Tag found. Edit the description and save.", false);
    }

    private async void OnScanAnotherClicked(object sender, EventArgs e)
    {
        _tag = null;
        SetTagPanelVisible(false);
        txtNewDescription.Text = string.Empty;
        ShowScanPrompt();

        await StartNfcAsync();
    }

    #endregion

    #region Save

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_tag == null)
        {
            await DisplayAlert(LabelTitle.Text, "Scan a tag first.", "OK");
            return;
        }

        /* Checked before the rest: the update posts straight to the API and there is no offline
           cache behind it, so there is nothing to be gained by validating the description first
           and then telling the guard it was never going to save. */
        if (!App.IsOnline)
        {
            await DisplayAlert(LabelTitle.Text, "Changes cannot be saved in offline mode.", "OK");
            return;
        }

        var newDescription = txtNewDescription.Text?.Trim();
        if (string.IsNullOrWhiteSpace(newDescription))
        {
            await DisplayAlert(LabelTitle.Text, "Description is required.", "OK");
            return;
        }

        var oldDescription = _tag.LabelDescription?.Trim() ?? string.Empty;
        if (string.Equals(oldDescription, newDescription, StringComparison.Ordinal))
        {
            await DisplayAlert(LabelTitle.Text, "The description has not changed.", "OK");
            return;
        }

        /* The logbook stamp is the point of this feature, so the site it goes to is settled
           before anything is written. Refusing here leaves the tag untouched; letting the save
           through and failing the stamp afterwards would change the database with no record. */
        var logBookClientSiteId = GetLocalSiteForPCAR();
        if (!logBookClientSiteId.HasValue)
        {
            await ShowLocalSiteNameAsync();
            await DisplayAlert(LabelTitle.Text,
                "No site scanned. On a patrol car or inspection tour the change is recorded against the site you last scanned - scan the site tag first, then save.",
                "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var saveResult = await _scannerControlServices.UpdateTagDescriptionAsync(_tag, newDescription);

            if (saveResult == null || !saveResult.IsSuccess)
            {
                await DisplayAlert(LabelTitle.Text, saveResult?.message ?? "Failed to save the change.", "OK");
                return;
            }

            var (logged, logMessage) = await StampLogBookAsync(oldDescription, newDescription, logBookClientSiteId.Value);

            // The tag now reads as saved whether or not the stamp landed; keep the page honest.
            _tag.LabelDescription = newDescription;
            LabelCurrentDescription.Text = newDescription;

            await DisplayAlert(LabelTitle.Text,
                logged
                    ? "Description updated and recorded in the logbook."
                    : $"Description updated, but the logbook entry failed: {logMessage}",
                "OK");

            UpdateInfoLabel("Saved.", false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Writes the change to the logbook as a system entry.
    ///
    /// Two sites, following the scan path in LogActivity/MainPage:
    ///   NFCScannedFromSiteId - the site the TAG belongs to, which becomes the entry's site
    ///   localClientSiteId    - the logbook it is filed in, which on a PCAR/INSP tour is the
    ///                          site the guard last scanned
    /// On a standard tour both are usually the same site; on a patrol car they are not, and a
    /// tag edited at one site must still appear in the logbook the guard is working.
    /// </summary>
    private async Task<(bool logged, string message)> StampLogBookAsync(string oldDescription, string newDescription, int logBookClientSiteId)
    {
        try
        {
            var entry = await BuildLogbookEntryAsync(oldDescription, _tag.UId, newDescription);

            var (isSuccess, message) = await _logBookServices.LogActivityTask(
                entry,
                logBookClientSiteId,
                (int)_mode,
                _tag.UId,
                IsSystemEntry: true,
                NFCScannedFromSiteId: _tag.ClientSiteId,
                RowIdInServer: 0);

            return (isSuccess, message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    #endregion

    #region NFC

    private async Task StartNfcAsync()
    {
        if (!CrossNFC.IsSupported || !CrossNFC.Current.IsAvailable)
        {
            UpdateInfoLabel("NFC scanning is not supported on this device.", true);
            return;
        }

        if (!CrossNFC.Current.IsEnabled)
        {
            UpdateInfoLabel("NFC is disabled. Please enable NFC scanning.", true);
            return;
        }

        /* Plugin.NFC 0.1.26 rebuilds CrossNFC.Current on EVERY Legacy assignment, even when the
           value does not change. Setting it on each scan left this page's handlers on the old
           instance while StartListening ran on a new one: the iPhone read the tag and showed
           its tick, and the page never heard about it. Only assign when it actually differs. */
        if (CrossNFC.Legacy)
            CrossNFC.Legacy = false;
        _isDeviceiOS = DeviceInfo.Platform == DevicePlatform.iOS;

        // Same delay as the add-tag page: Android throws "Foreground dispatch can only be
        // enabled when your activity is resumed" without it.
        await Task.Delay(500);

        /* Same as the add-tag page: start listening on iOS too. On iOS StartListening is what
           opens the system NFC scan sheet - without it nothing can be read. The sheet closes
           after one tag (or when the guard cancels it), so ButtonScanTag reopens it. */
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SubscribeNfc();
            CrossNFC.Current.StartListening();
        });
    }

    /// <summary>
    /// Attaches the handlers to the CURRENT CrossNFC instance. Another page can still replace
    /// that instance (MainPage and the add-tag page set CrossNFC.Legacy when they start
    /// listening), so this follows it rather than trusting an earlier subscription.
    /// </summary>
    private void SubscribeNfc()
    {
        var current = CrossNFC.Current;
        if (ReferenceEquals(_subscribedNfc, current))
            return;

        UnsubscribeNfc();

        current.OnMessageReceived += Current_OnMessageReceived;
        if (_isDeviceiOS)
            current.OniOSReadingSessionCancelled += Current_OniOSReadingSessionCancelled;
        _subscribedNfc = current;
    }

    private void UnsubscribeNfc()
    {
        if (_subscribedNfc == null)
            return;

        _subscribedNfc.OnMessageReceived -= Current_OnMessageReceived;
        if (_isDeviceiOS)
            _subscribedNfc.OniOSReadingSessionCancelled -= Current_OniOSReadingSessionCancelled;
        _subscribedNfc = null;
    }

    private void StopNfc()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UnsubscribeNfc();
                CrossNFC.Current.StopListening();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to stop NFC listening: {ex.Message}");
        }
    }

    private async void Current_OnMessageReceived(ITagInfo tagInfo)
    {
        if (tagInfo == null)
        {
            UpdateInfoLabel("No tag found.", true);
            return;
        }

        var serialNumber = NFCUtils.ByteArrayToHexString(tagInfo.Identifier, "");
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            UpdateInfoLabel("Tag UID not found.", true);
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () => await LookupTagAsync(serialNumber));
    }

    private void Current_OniOSReadingSessionCancelled(object sender, EventArgs e)
    {
        Debug.WriteLine("iOS NFC Session has been cancelled");
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_tag == null)
                UpdateInfoLabel("Scan cancelled. Tap 'Scan NFC tag' to try again.", true);
        });
    }

    private async void OnScanTagClicked(object sender, EventArgs e)
    {
        ShowScanPrompt();
        await StartNfcAsync();
    }

    #endregion


    #region Page chrome

    private void UpdateInfoLabel(string message, bool isError)
    {
        LabelInfo.Text = message;
        LabelInfo.TextColor = isError ? Colors.Red : Colors.Green;
    }

    /// <summary>
    /// The tag panel and the iOS scan button take turns: an iOS NFC sheet closes after one tag
    /// or a cancel, so while no tag is open the guard needs a way to open it again. Android
    /// keeps listening for as long as the page is open and has no need for the button.
    /// </summary>
    private void SetTagPanelVisible(bool visible)
    {
        TagDetailPanel.IsVisible = visible;
        ButtonScanTag.IsVisible = _isDeviceiOS && !visible;
    }

    private void SetBusy(bool busy)
    {
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
        ButtonSave.IsEnabled = !busy;
    }

    private void OnCloseClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new MenuSettingsPage();
    }

    private void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService, true);
    }

    protected override bool OnBackButtonPressed()
    {
        Application.Current.MainPage = new MenuSettingsPage();
        return true;
    }

    #endregion
}
