using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using C4iSytemsMobApp.Views;
using CommunityToolkit.Maui.Views;
using Plugin.BLE.Abstractions;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.JavaScript;
using System.Windows.Input;

namespace C4iSytemsMobApp;

public partial class iBeaconScannerPage : ContentPage
{
    private IBeaconScanner scanner = new();
    private readonly ILogBookServices _logBookServices;
    private readonly IScannerControlServices _scannerControlServices;
    private int? _clientSiteId;
    private int? _userId;
    private int? _guardId;
    public const string ALERT_TITLE = "iBeacon";
    public ObservableCollection<string> BleLogs { get; set; } = new ObservableCollection<string>();

    public iBeaconScannerPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        LoadSecureData();
        BindingContext = this;
        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
        // Subscribe to messages
        MessageBus.BeaconMessageReceived += OnBeaconMessageReceived;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        //await LoadLogsAsync();
    }

    private async void OnStartScanClicked(object sender, EventArgs e)
    {
        var hasPermission = await PermissionService.CheckAndRequestPermissionsAsync();

        if (!hasPermission)
        {
            await DisplayAlert("Permission Denied", "Bluetooth or Location permission is required to scan for beacons.", "OK");
            return;
        }

        await scanner.StartScanningAsync();
    }

    private async void OnStopScanClicked(object sender, EventArgs e)
    {
        await scanner.StopScanningAsync();
    }

    private async void OnBeaconMessageReceived(string msgType, string msg)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (msgType == "INFO")
                DisplayLog(msg);
            else if (msgType == "DATA")
                ProcessData(msg);
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Optional cleanup
        MessageBus.BeaconMessageReceived -= OnBeaconMessageReceived;
    }

    private void OnClearLogsClicked(object sender, EventArgs e)
    {
        BleLogs.Clear();
    }

    private async void ProcessData(string msgdata)
    {

        var msgDataArray = msgdata.Split("-");
        var serialNumber = msgDataArray[0];
        var deviceName = msgDataArray[1] ?? " ";

        if (!string.IsNullOrEmpty(serialNumber))
        {
            if (_guardId == null || _clientSiteId == null || _userId == null || _guardId <= 0 || _clientSiteId <= 0 || _userId <= 0) return;

            if (!App.IsOnline)
            {
                // Log To Cache                    
                await LogScannedDataToCache(serialNumber, deviceName, ScanningType.BLUETOOTH);
                return;
            }

            DisplayLog($"Device Found:{deviceName}. Logging activity...");
            var scannerSettings = await _scannerControlServices.FetchTagInfoDetailsAsync(_clientSiteId.ToString(), serialNumber, _guardId.ToString(), _userId.ToString(), ScanningType.BLUETOOTH);
            if (scannerSettings != null)
            {
                if (scannerSettings.IsSuccess)
                {
                    // Valid tag - log activity
                    int _scannerType = (int)ScanningType.BLUETOOTH;
                    var _taguid = serialNumber;
                    if (scannerSettings.tagFound)
                    {
                        LogActivityTask(scannerSettings.tagInfoLabel, _scannerType, _taguid);
                    }

                }
                else
                {
                    var newmsg = scannerSettings?.message ?? "Unknown error";
                    DisplayLog($"Error: {deviceName}/{newmsg}");
                    // await DisplayAlert(ALERT_TITLE, newmsg, "OK");
                    return;
                }
            }
            else
            {
                var newmsg = scannerSettings?.message ?? "Unknown error";
                DisplayLog($"Error: {deviceName}/{newmsg}");
                // await DisplayAlert(ALERT_TITLE, newmsg, "OK");
                return;
            }

        }
    }


    private async Task LogScannedDataToCache(string _TagUid, string _deviceName, ScanningType _scannerType)
    {
        DisplayLog($"Device Found:{_deviceName}. Logging activity to Cache...\n");
        var (isSuccess, msg, _ChaceCount) = await _scannerControlServices.SaveScanDataToLocalCache(_TagUid, _scannerType, _clientSiteId.Value, _userId.Value, _guardId.Value);
        if (isSuccess)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SyncState.SyncedCount = _ChaceCount;
            });
            DisplayLog($"{msg}");
        }
        else
        {
            var newmsg = msg ?? "Failed to save tag scan";
            DisplayLog($"Error: {newmsg}");
            //await DisplayAlert("Error", newmsg, "OK");
        }
    }

    private async void LoadSecureData()
    {
        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
        _clientSiteId = clientSiteId;
        _guardId = guardId;
        _userId = userId;
    }

    private async Task LogActivityTask(string activityDescription, int scanningType = 0, string _taguid = "NA")
    {

        var (isSuccess, msg) = await _logBookServices.LogActivityTask(activityDescription, scanningType, _taguid);
        if (isSuccess)
        {
            DisplayLog($"{msg}");
        }
        else
        {
            DisplayLog($"Error: {msg}");
        }
    }

    private void DisplayLog(string msg)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            BleLogs.Add($"{msg}\n");
        });
    }

    private async Task<(int guardId, int clientSiteId, int userId)> GetSecureStorageValues()
    {
        int.TryParse(Preferences.Get("GuardId", ""), out int guardId);
        int.TryParse(Preferences.Get("SelectedClientSiteId", ""), out int clientSiteId);
        int.TryParse(Preferences.Get("UserId", ""), out int userId);

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

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new MainPage(volumeButtonService);
        //Application.Current.MainPage = new MainPage();
    }

}

