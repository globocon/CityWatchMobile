
using ZXing.Net.Maui;

namespace C4iSytemsMobApp;

public partial class QrScannerPage : ContentPage
{
	public QrScannerPage()
	{
		InitializeComponent();
	}


    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (e?.Results == null || !e.Results.Any())
            return; // Exit if no QR code is detected


        string scannedData = e.Results[0].Value; // Get first QR code detected

        // Process QR code data
        if (string.IsNullOrEmpty(scannedData) || !scannedData.StartsWith("c4i2025|"))
        {
            ShowErrorAndCloseScanner("Invalid QR Code Format");
            return;
        }

        var parts = scannedData.Split('|');
        if (parts.Length != 4 ||
            !int.TryParse(parts[1], out int userId) ||
            !int.TryParse(parts[2], out int clientId) ||
            string.IsNullOrWhiteSpace(parts[3]))
        {
            ShowErrorAndCloseScanner("QR Code Format is Invalid");
            return;
        }

        // Save extracted values in SecureStorage
        SecureStorage.SetAsync("UserId", userId.ToString());
        SecureStorage.SetAsync("ClientSiteId", clientId.ToString());
        SecureStorage.SetAsync("SelectedClientSiteId", clientId.ToString());
        SecureStorage.SetAsync("UserName", parts[3]);

        // Navigate to the next page efficiently
        Dispatcher.Dispatch(() =>
        {
            Application.Current.MainPage = new GuardLoginQRCode();
        });
    }

    // Helper method to show error and close scanner
    private async void ShowErrorAndCloseScanner(string message)
    {
        await DisplayAlert("Error", message, "OK");
        await Navigation.PopAsync();
    }




    private async void OnCloseScannerClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync(); // Close the scanner page
    }
}