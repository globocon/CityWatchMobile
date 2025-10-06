
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace C4iSytemsMobApp;

public partial class QrScannerPage : ContentPage
{
    private bool _isScanned = false;

    public QrScannerPage()
    {
        InitializeComponent();

        cameraView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            TryHarder = true
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Permission Denied", "Camera access is required to scan QR codes.", "OK");
            await Navigation.PopAsync();
            return;
        }

        _isScanned = false;
        cameraView.IsDetecting = true;
        cameraView.CameraLocation = CameraLocation.Rear;

        // Automatically turn ON torch when scanner starts
        cameraView.IsTorchOn = true;
    }

    protected override void OnDisappearing()
    {
        // Turn OFF torch when scanner stops / page closes
        cameraView.IsTorchOn = false;
        cameraView.IsDetecting = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_isScanned || e?.Results == null || !e.Results.Any())
            return;

        _isScanned = true;
        string scannedData = e.Results[0].Value;

        Dispatcher.Dispatch(async () =>
        {
            if (string.IsNullOrEmpty(scannedData) || !scannedData.StartsWith("c4i2025|"))
            {
                await ShowErrorAndCloseScanner("Invalid QR Code Format");
                return;
            }

            var parts = scannedData.Split('|');
            if (parts.Length != 4 ||
                !int.TryParse(parts[1], out int userId) ||
                !int.TryParse(parts[2], out int clientId) ||
                string.IsNullOrWhiteSpace(parts[3]))
            {
                await ShowErrorAndCloseScanner("QR Code Format is Invalid");
                return;
            }

            // Save extracted values
            Preferences.Set("UserId", userId.ToString());
            Preferences.Set("ClientSiteId", clientId.ToString());
            Preferences.Set("SelectedClientSiteId", clientId.ToString());
            Preferences.Set("UserName", parts[3]);

            // Turn OFF torch once scanning is finished
            cameraView.IsTorchOn = false;

            // Navigate
            Application.Current.MainPage = new GuardLoginQRCode();
        });
    }

    private async Task ShowErrorAndCloseScanner(string message)
    {
        await DisplayAlert("Error", message, "OK");
        await Navigation.PopAsync();
    }

    private async void OnCloseScannerClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}

