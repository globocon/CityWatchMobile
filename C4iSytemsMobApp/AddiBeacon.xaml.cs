using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace C4iSytemsMobApp;

/// <summary>
/// Registers a new iBeacon against a site: find the beacons in range, pick one, describe it,
/// save.
///
/// Android only. The BLE scan loop in IBeaconScanner is only wired up and permissioned on
/// Android, so the menu hides the entry point on other platforms; the guard below is the
/// backstop for anything that reaches the page another way.
/// </summary>
public partial class AddiBeacon : ContentPage
{
    public const string ALERT_TITLE = "iBeacon";

    /// <summary>
    /// How long a Scan runs before it stops itself.
    ///
    /// The scan loop would otherwise run until the guard pressed Stop or left the page, holding
    /// the radio on in a patrol car for the rest of a shift. Thirty seconds is three passes at
    /// Plugin.BLE's ten-second default, which is enough for a beacon in the room to report and
    /// short enough that a forgotten scan costs nothing.
    /// </summary>
    private const int ScanWindowSeconds = 30;

    private readonly IScannerControlServices _scannerControlServices;
    private readonly IBeaconScanner _bleScanner = new();
    private bool _bleEventsSubscribed;

    private CancellationTokenSource _scanWindowCts;
    private CancellationTokenSource _pulseCts;

    private DeviceFound _selectedDevice;

    /// <summary>Beacons seen in this scan, de-duplicated by MAC - the loop re-reports them.</summary>
    public ObservableCollection<DeviceFound> Devices { get; } = new();

    public AddiBeacon()
    {
        InitializeComponent();
        BindingContext = this;
        NavigationPage.SetHasNavigationBar(this, false);

        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            BleControls.IsVisible = false;
            BleDeviceList.IsVisible = false;
            UpdateInfoLabel("Adding an iBeacon is supported on Android devices only.", true);
            return;
        }

        /* The site a beacon is registered to follows the scanned site on a PCAR/INSP tour, and
           that expires after 30 minutes - so the line has to keep up while the guard is typing
           a description. */
        App.PcarInspTagResetEvent -= OnPcarInspTagReset;
        App.PcarInspTagResetEvent += OnPcarInspTagReset;

        _ = ShowLocalSiteNameAsync();   // resolves the name in the background; may need the server
        UpdateInfoLabel("Scan for devices, then pick the beacon to add.", false);

        if (!_bleEventsSubscribed)
        {
            /* Devices arrive on this callback, not on MessageBus: IBeaconScanner's
               StartScanningAsync/StopScanningAsync and its MessageBus "DATA" send are all
               commented out, so the only live path is Start() -> ScanLoop -> OnDeviceFoundAsync.
               Same wiring MainPage uses. */
            _bleScanner.OnDeviceFoundAsync += OnDevicesFoundAsync;
            _bleScanner.OnScanningInProgress += OnScanningInProgress;
            _bleEventsSubscribed = true;
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
    }

    #region Site resolution

    /// <summary>
    /// The site the beacon is registered against.
    ///
    /// On a standard tour that is the site the guard logged in at. On a patrol car or
    /// inspection tour the guard logs in against the car's base site and then moves between
    /// sites, so the login site is not where they are - the site they last scanned is. Same
    /// rule as GetLocalSiteForPCAR on the log activity page.
    ///
    /// Null on a PCAR/INSP tour with no live scanned site. The reading pages fall back to the
    /// login site; that is wrong here, because this writes a permanent row and a beacon filed
    /// against the patrol car's base site would sit on a site it does not belong to with
    /// nothing to flag it. The caller refuses to save instead.
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
               register a beacon to the right site. */
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
            Debug.WriteLine($"Failed to resolve add-beacon site name: {ex.Message}");
        }
    }

    #endregion

    #region Bluetooth

    private async void OnStartBleScanClicked(object sender, EventArgs e)
    {
        if (!_bleScanner.IsBluetoothSupported)
        {
            await DisplayAlert(ALERT_TITLE, "Bluetooth is not supported on this device.", "OK");
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
            await DisplayAlert(ALERT_TITLE, "Please switch on Bluetooth to scan for beacons.", "OK");
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
            : $"Scan finished - {Devices.Count} device(s) found. Pick the beacon to add.", Devices.Count == 0);
    }

    /// <summary>
    /// Fired by the scanner around each pass of its loop. A pass finds nothing for seconds at a
    /// time, so the pulse is what tells the guard the radio is still working.
    /// </summary>
    private void OnScanningInProgress(bool scanning)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_selectedDevice != null)
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

    private Task OnDevicesFoundAsync(List<DeviceFound> devices)
    {
        // A late callback after the guard has picked a device must not disturb the form.
        if (_selectedDevice != null || devices == null || devices.Count == 0)
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
    /// is wrapped in a Frame, which swallows the touch on Android, so selection never changed
    /// and the description form never appeared. RosterPage picks its items the same way.
    /// </summary>
    private async void OnBleDeviceTapped(object sender, EventArgs e)
    {
        if ((e as TappedEventArgs)?.Parameter is not DeviceFound device)
            return;

        // Ignore a second tap while the form for the first is already open.
        if (_selectedDevice != null)
            return;

        /* Swap to the description form BEFORE stopping the scan, not after. Stop() is awaited
           and can sit for a moment on the adapter, and until it returned the guard was left
           looking at the device list with nothing to type into. _selectedDevice is set first so
           the scan callback, which may still fire once or twice on the way down, leaves the
           form alone. */
        _selectedDevice = device;
        LabelDeviceName.Text = device.DeviceName;
        LabelDeviceUid.Text = device.MacID;
        txtTagLabel.Text = string.Empty;

        BleControls.IsVisible = false;
        BleDeviceList.IsVisible = false;
        SelectedDevicePanel.IsVisible = true;

        CancelScanWindow();
        StopPulse();
        ScanningIndicator.IsVisible = false;
        ScanSpinner.IsRunning = false;

        UpdateInfoLabel("Enter a description and save.", false);

        try { await _bleScanner.Stop(); } catch (Exception ex) { Debug.WriteLine(ex.Message); }
        ButtonStartScan.IsEnabled = true;
        ButtonStopScan.IsEnabled = false;
    }

    private void OnPickAnotherClicked(object sender, EventArgs e)
    {
        _selectedDevice = null;
        SelectedDevicePanel.IsVisible = false;
        BleControls.IsVisible = true;
        BleDeviceList.IsVisible = true;
        UpdateInfoLabel("Scan for devices, then pick the beacon to add.", false);
    }

    #endregion

    #region Save

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_selectedDevice == null)
        {
            await DisplayAlert(ALERT_TITLE, "Please select a device.", "OK");
            return;
        }

        /* Checked before the rest: registering a beacon posts straight to the API with no
           offline cache behind it, so validating the description first and then failing anyway
           helps nobody. Same rule as the add-NFC page. */
        if (!App.IsOnline)
        {
            await DisplayAlert(ALERT_TITLE, "Beacon cannot be registered in offline mode.", "OK");
            return;
        }

        var description = txtTagLabel.Text?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            await DisplayAlert(ALERT_TITLE, "Description is required.", "OK");
            return;
        }

        var clientSiteId = GetLocalSiteForPCAR();
        if (!clientSiteId.HasValue)
        {
            await ShowLocalSiteNameAsync();
            await DisplayAlert(ALERT_TITLE,
                "No site scanned. On a patrol car or inspection tour a beacon is registered to the site you last scanned - scan the site tag first, then save.",
                "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _scannerControlServices.SaveTagInfoDetailsAsync(
                clientSiteId.Value.ToString(), _selectedDevice.MacID, description, ScanningType.BLUETOOTH);

            if (result == null || !result.IsSuccess)
            {
                // The server rejects a UID that is already registered; that message comes back here.
                await DisplayAlert(ALERT_TITLE, result?.message ?? "Failed to save the beacon.", "OK");
                return;
            }

            await DisplayAlert(ALERT_TITLE, result.message ?? "Beacon saved successfully.", "OK");

            _selectedDevice = null;
            txtTagLabel.Text = string.Empty;
            SelectedDevicePanel.IsVisible = false;
            BleControls.IsVisible = true;
            BleDeviceList.IsVisible = true;
            UpdateInfoLabel("Saved. Scan again to add another beacon.", false);
        }
        finally
        {
            SetBusy(false);
        }
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
