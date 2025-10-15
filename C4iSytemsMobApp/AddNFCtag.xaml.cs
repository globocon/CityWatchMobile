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
        await StartNFC();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        _scannedTagUid = string.Empty;
        if (_isNfcEnabledForSite && CrossNFC.IsSupported && CrossNFC.Current.IsAvailable)
        {
            await StopListening();
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

        var scannerSettings = await _scannerControlServices.SaveNFCTagInfoDetailsAsync(clientSiteId.ToString(), _scannedTagUid, guardId.ToString(), userId.ToString(), txtTagLabel.Text);
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