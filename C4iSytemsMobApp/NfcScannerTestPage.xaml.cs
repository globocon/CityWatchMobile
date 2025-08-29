
using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

public partial class NfcScannerTestPage : ContentPage
{
    private readonly INfcService _nfcService;
    public NfcScannerTestPage(INfcService nfcService)
	{
		InitializeComponent();
        _nfcService = nfcService;

       
        
    }

    private void OnNfcTagRead(string tagId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            txtnfcTagID.Text += $"TagDetails:\n {tagId}\n\n";

        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_nfcService.IsNfcAvailable && _nfcService.IsNfcEnabled)
            _nfcService.StopListening();
    }

    //OnStartNfcScannerClicked
    private void OnStartNfcScannerClicked(object sender, EventArgs e)
    {
        // NFC status check
        if (!_nfcService.IsNfcAvailable)
        {
            ShowMessageOnScreen("Error", "NFC is not available on this device.");
            return;
        }
        else if (!_nfcService.IsNfcEnabled)
        {
            ShowMessageOnScreen("Error", "NFC is not enabled. Please enable NFC and try again.");
            return;
        }
        
        // Start listening for NFC tags
        _nfcService.StartListening(OnNfcTagRead);
        ShowMessageOnScreen("Info", "NFC is now listening for tags...");
    }

    
    // Helper method to show error and close scanner
    private async void ShowMessageOnScreen(string msgtype, string message)
    {
        await DisplayAlert(msgtype, message, "OK");

    }


    protected override bool OnBackButtonPressed()
    {
        if (_nfcService.IsNfcAvailable && _nfcService.IsNfcEnabled)
        {
            Task.Run(() => _nfcService.StopListening());
        }
            
        return base.OnBackButtonPressed();
    }

   
}