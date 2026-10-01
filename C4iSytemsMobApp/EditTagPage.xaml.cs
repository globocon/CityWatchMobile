using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Services;
using C4iSytemsMobApp.Views;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.NFC;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace C4iSytemsMobApp;

/// <summary>
/// Edits the description of a tag that is already registered - NFC or iBeacon.
///
/// One page for both, because the two differ only in how the UID is obtained: an NFC tag
/// announces itself when tapped, a beacon has to be hunted for and picked off a list.
/// Everything after that - look the UID up, refuse if it is unknown, edit the description,
/// save, stamp the logbook - is identical, and having it once means the logbook wording and
/// the site rules cannot drift between the two.
///
/// Scanning works offline; saving does not. The lookup and the save both go straight to the
/// API with no offline cache behind them.
/// </summary>
public partial class EditTagPage : ContentPage
{
    private readonly ScanningType _mode;
    private readonly IScannerControlServices _scannerControlServices;
    private readonly ILogBookServices _logBookServices;

    /// <summary>
    /// How long a Scan runs before it stops itself. Three passes at Plugin.BLE's ten-second
    /// default - enough for a beacon in the room to report, short enough that a scan left
    /// running in a patrol car does not hold the radio on for the shift.
    /// </summary>
    private const int ScanWindowSeconds = 30;

    private readonly IBeaconScanner _bleScanner = new();
    private bool _bleEventsSubscribed;
    private bool _nfcEventsSubscribed;
    private bool _isDeviceiOS;

    private CancellationTokenSource _scanWindowCts;
    private CancellationTokenSource _pulseCts;

    private TagEditInfo _tag;

    /// <summary>Beacons seen in this scan, de-duplicated by MAC - the scanner re-reports them.</summary>
    public ObservableCollection<DeviceFound> Devices { get; } = new();

    public EditTagPage(ScanningType mode)
    {
        InitializeComponent();
        BindingContext = this;
        NavigationPage.SetHasNavigationBar(this, false);

        _mode = mode;
        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();

        LabelTitle.Text = _mode == ScanningType.BLUETOOTH ? "Edit iBeacon" : "Edit NFC Tag";
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

        if (_mode == ScanningType.BLUETOOTH)
        {
            BleControls.IsVisible = true;
            BleDeviceList.IsVisible = true;

            /* Devices arrive on this callback, not on MessageBus: IBeaconScanner's
               StartScanningAsync/StopScanningAsync and its MessageBus "DATA" send are all
               commented out, so the only live path is Start() -> ScanLoop -> OnDeviceFoundAsync.
               This is the same wiring MainPage uses. */
            if (!_bleEventsSubscribed)
            {
                _bleScanner.OnDeviceFoundAsync += OnDevicesFoundAsync;
                _bleScanner.OnScanningInProgress += OnScanningInProgress;
                _bleEventsSubscribed = true;
            }
        }
        else
        {
            await StartNfcAsync();
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        App.PcarInspTagResetEvent -= OnPcarInspTagReset;

        if (_bleEventsSubscribed)
        {
            _bleScanner.OnDeviceFoundAsync -= OnDevicesFoundAsync;
            _bleScanner.OnScanningInProgress -= OnScanningInProgress;
            _bleEventsSubscribed = false;
        }

        CancelScanWindow();
        StopPulse();

        try { await _bleScanner.Stop(); } catch (Exception ex) { Debug.WriteLine(ex.Message); }

        if (_nfcEventsSubscribed)
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
        UpdateInfoLabel(_mode == ScanningType.BLUETOOTH
            ? "Scan for devices, then pick the beacon to edit."
            : "Tap the NFC tag you want to edit.", false);
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
                TagDetailPanel.IsVisible = false;
                ShowDeviceListIfBeacon(true);
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

        ShowDeviceListIfBeacon(false);
        TagDetailPanel.IsVisible = true;

        UpdateInfoLabel("Tag found. Edit the description and save.", false);
    }

    private void ShowDeviceListIfBeacon(bool visible)
    {
        if (_mode != ScanningType.BLUETOOTH)
            return;

        BleControls.IsVisible = visible;
        BleDeviceList.IsVisible = visible;
    }

    private async void OnScanAnotherClicked(object sender, EventArgs e)
    {
        _tag = null;
        TagDetailPanel.IsVisible = false;
        txtNewDescription.Text = string.Empty;
        ShowDeviceListIfBeacon(true);
        ShowScanPrompt();

        if (_mode == ScanningType.NFC)
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

        CrossNFC.Legacy = false;
        _isDeviceiOS = DeviceInfo.Platform == DevicePlatform.iOS;

        // Same delay as the add-tag page: Android throws "Foreground dispatch can only be
        // enabled when your activity is resumed" without it.
        await Task.Delay(500);

        SubscribeNfc();

        if (!_isDeviceiOS)
            MainThread.BeginInvokeOnMainThread(() => CrossNFC.Current.StartListening());
    }

    private void SubscribeNfc()
    {
        if (_nfcEventsSubscribed)
            return;

        CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
        _nfcEventsSubscribed = true;
    }

    private void StopNfc()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CrossNFC.Current.OnMessageReceived -= Current_OnMessageReceived;
                if (!_isDeviceiOS)
                    CrossNFC.Current.StopListening();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to stop NFC listening: {ex.Message}");
        }
        finally
        {
            _nfcEventsSubscribed = false;
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

    #endregion

    #region Bluetooth

    private async void OnStartBleScanClicked(object sender, EventArgs e)
    {
        if (!_bleScanner.IsBluetoothSupported)
        {
            await DisplayAlert(LabelTitle.Text, "Bluetooth is not supported on this device.", "OK");
            return;
        }

        var hasPermission = await PermissionService.CheckAndRequestPermissionsAsync();
        if (!hasPermission)
        {
            await DisplayAlert("Permission Denied", "Bluetooth or Location permission is required to scan for beacons.", "OK");
            return;
        }

        if (_bleScanner.GetCurrentState() == BluetoothState.Off)
        {
            await DisplayAlert(LabelTitle.Text, "Please switch on Bluetooth to scan for beacons.", "OK");
            return;
        }

        Devices.Clear();
        ButtonStartScan.IsEnabled = false;
        ButtonStopScan.IsEnabled = true;
        ScanningIndicator.IsVisible = true;
        ScanSpinner.IsRunning = true;

        _bleScanner.Start();

        _ = RunScanWindowAsync();
    }

    /// <summary>
    /// Runs the scan for <see cref="ScanWindowSeconds"/> and then stops it, counting down in the
    /// status line so the guard can see it is going to end rather than wondering why it did.
    /// Cancelled by Stop, by picking a device, or by leaving the page.
    /// </summary>
    private async Task RunScanWindowAsync()
    {
        CancelScanWindow();
        _scanWindowCts = new CancellationTokenSource();
        var token = _scanWindowCts.Token;

        try
        {
            for (var remaining = ScanWindowSeconds; remaining > 0; remaining--)
            {
                UpdateInfoLabel($"Scanning for beacons... {remaining}s", false);
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped early - whoever cancelled owns the status line from here.
            return;
        }

        await StopBleScanAsync();
    }

    private void CancelScanWindow()
    {
        try
        {
            _scanWindowCts?.Cancel();
            _scanWindowCts?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
        finally
        {
            _scanWindowCts = null;
        }
    }

    private async void OnStopBleScanClicked(object sender, EventArgs e)
    {
        await StopBleScanAsync();
    }

    private async Task StopBleScanAsync()
    {
        CancelScanWindow();

        try { await _bleScanner.Stop(); } catch (Exception ex) { Debug.WriteLine(ex.Message); }

        StopPulse();
        ScanningIndicator.IsVisible = false;
        ScanSpinner.IsRunning = false;
        ButtonStartScan.IsEnabled = true;
        ButtonStopScan.IsEnabled = false;

        UpdateInfoLabel(Devices.Count == 0
            ? "No beacons found. Move closer and scan again."
            : $"Scan finished - {Devices.Count} device(s) found. Pick the beacon to edit.", Devices.Count == 0);
    }

    /// <summary>
    /// Fired by the scanner around each pass of its loop. A pass finds nothing for seconds at a
    /// time, so the pulse is what tells the guard the radio is still working.
    /// </summary>
    private void OnScanningInProgress(bool scanning)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_tag != null)
                return;

            ScanSpinner.IsRunning = scanning;

            if (scanning)
                StartPulse();
            else
                StopPulse();
        });
    }

    private void StartPulse()
    {
        _pulseCts?.Cancel();
        _pulseCts = new CancellationTokenSource();
        _ = RunPulseLoopAsync(_pulseCts.Token);
    }

    private void StopPulse()
    {
        _pulseCts?.Cancel();
        _pulseCts = null;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ScanPulseLabel.Opacity = 1;
            ScanPulseLabel.Scale = 1;
        });
    }

    private async Task RunPulseLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ScanPulseLabel.FadeTo(0.3, 500, Easing.CubicInOut);
                    await ScanPulseLabel.FadeTo(1.0, 500, Easing.CubicInOut);
                });
            }
        }
        catch (Exception ex)
        {
            // The page can go away mid-animation; that is not worth surfacing.
            Debug.WriteLine($"Scan pulse stopped: {ex.Message}");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => ScanPulseLabel.Opacity = 1);
        }
    }

    /// <summary>
    /// The scan loop re-reports the same beacons on every pass, so the list is keyed on MAC -
    /// a guard picking from forty copies of three beacons is no use.
    /// </summary>
    private Task OnDevicesFoundAsync(List<DeviceFound> devices)
    {
        // A late callback after the guard has opened a tag must not disturb the form.
        if (_tag != null || devices == null || devices.Count == 0)
            return Task.CompletedTask;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var device in devices)
            {
                if (string.IsNullOrWhiteSpace(device?.MacID))
                    continue;

                if (Devices.Any(d => string.Equals(d.MacID, device.MacID, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Devices.Add(new DeviceFound
                {
                    MacID = device.MacID,
                    DeviceName = string.IsNullOrWhiteSpace(device.DeviceName) ? "Unknown device" : device.DeviceName
                });
            }

            /* Deliberately silent: the status line is showing the scan countdown, and the list
               filling in is its own feedback. Writing here would fight RunScanWindowAsync. */
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// A beacon card was tapped.
    ///
    /// Driven by a TapGestureRecognizer rather than CollectionView.SelectionChanged: each item
    /// is wrapped in a Frame, which swallows the touch on Android, so selection never changed.
    /// RosterPage picks its items the same way.
    /// </summary>
    private async void OnBleDeviceTapped(object sender, EventArgs e)
    {
        if ((e as TappedEventArgs)?.Parameter is not DeviceFound device)
            return;

        // Ignore a second tap while a tag is already open for editing.
        if (_tag != null)
            return;

        /* Stop the loop before looking up, so the list is not still growing under the guard
           while the server is asked about the device they picked. */
        CancelScanWindow();
        StopPulse();
        ScanningIndicator.IsVisible = false;
        ScanSpinner.IsRunning = false;

        try { await _bleScanner.Stop(); } catch (Exception ex) { Debug.WriteLine(ex.Message); }
        ButtonStartScan.IsEnabled = true;
        ButtonStopScan.IsEnabled = false;

        await LookupTagAsync(device.MacID);
    }

    #endregion

    #region Page chrome

    private void UpdateInfoLabel(string message, bool isError)
    {
        LabelInfo.Text = message;
        LabelInfo.TextColor = isError ? Colors.Red : Colors.Green;
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
