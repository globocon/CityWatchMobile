
using C4iSytemsMobApp.Interface;
using Plugin.NFC;
using System.Text;

namespace C4iSytemsMobApp;

public partial class NfcScannerTestPage : ContentPage
{
    public const string ALERT_TITLE = "NFC";
    public const string MIME_TYPE = "application/com.companyname.nfcsample";

    private NFCNdefTypeFormat _type;
    private bool _makeReadOnly = false;
    private bool _isDeviceiOS = false;
    private readonly INfcService _nfcService;
    public NfcScannerTestPage(INfcService nfcService)
	{
		InitializeComponent();
        _nfcService = nfcService;
        BindingContext = this;
    }

    /// <summary>
    /// Property that tracks whether the Android device is still listening
    /// </summary>
    public bool DeviceIsListening
    {
        get => _deviceIsListening;
        set
        {
            _deviceIsListening = value;
            lblServiceStatus.Text = $"Is Listening: {DeviceIsListening}";
            button_Start_Stop.Text = DeviceIsListening ? "Stop NFC Scanner" : "Start NFC Scanner";
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
            lblIsEnabled.Text = $"Is Enabled: {NfcIsEnabled}";
            OnPropertyChanged(nameof(NfcIsEnabled));
            OnPropertyChanged(nameof(NfcIsDisabled));
        }
    }

    public bool NfcIsDisabled => !NfcIsEnabled;
       
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_nfcService.IsSupported)
        {
            if (!_nfcService.IsAvailable)
            {
                await ShowAlert("NFC is not available");
                lblIsAvailable.Text = "Is Available: False";
            }
            else
            {
                lblIsAvailable.Text = "Is Available: True";
            }

                NfcIsEnabled = _nfcService.IsEnabled;
            if (!NfcIsEnabled)
                await ShowAlert("NFC is disabled");

            if (DeviceInfo.Platform == DevicePlatform.iOS)
                _isDeviceiOS = true;

            await InitializeNFCAsync();
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await StopListening();
    }

    private async Task InitializeNFCAsync()
    {
        try
        {
            await _nfcService.InitializeAsync();
            SubscribeToNFCEvents();

            // Some delay to prevent Java.Lang.IllegalStateException on Android
            await Task.Delay(500);
            await StartListeningIfNotiOS();
        }
        catch (Exception ex)
        {
            await ShowAlert($"Failed to initialize NFC: {ex.Message}");
        }
    }

    private void SubscribeToNFCEvents()
    {
        _nfcService.OnMessageReceived += OnMessageReceived;
        //_nfcService.OnMessagePublished += OnMessagePublished;
        _nfcService.OnTagDiscovered += OnTagDiscovered;
        _nfcService.OnNfcStatusChanged += OnNfcStatusChanged;
        _nfcService.OnTagListeningStatusChanged += OnTagListeningStatusChanged;

        if (_isDeviceiOS)
            _nfcService.OniOSReadingSessionCancelled += OniOSReadingSessionCancelled;
    }

    private void UnsubscribeFromNFCEvents()
    {
        _nfcService.OnMessageReceived -= OnMessageReceived;
        //_nfcService.OnMessagePublished -= OnMessagePublished;
        _nfcService.OnTagDiscovered -= OnTagDiscovered;
        _nfcService.OnNfcStatusChanged -= OnNfcStatusChanged;
        _nfcService.OnTagListeningStatusChanged -= OnTagListeningStatusChanged;

        if (_isDeviceiOS)
            _nfcService.OniOSReadingSessionCancelled -= OniOSReadingSessionCancelled;
    }

    private void OnTagListeningStatusChanged(object sender, bool isListening) =>
        DeviceIsListening = isListening;

    private async void OnNfcStatusChanged(object sender, bool isEnabled)
    {
        NfcIsEnabled = isEnabled;
        await ShowAlert($"NFC has been {(isEnabled ? "enabled" : "disabled")}");
    }

    private async void OnMessageReceived(object sender, ITagInfo tagInfo)
    {
        if (tagInfo == null)
        {
            await ShowAlert("No tag found");
            return;
        }

        var identifier = tagInfo.Identifier;
        var serialNumber = _nfcService.ByteArrayToHexString(identifier, ":");
        var title = !tagInfo.IsEmpty ? $"Tag Info: {tagInfo}" : "Tag Info";

        if (!tagInfo.IsSupported)
        {
            await ShowAlert("Unsupported tag (app)", title);
        }
        else if (tagInfo.IsEmpty)
        {
            var msg = $"Empty tag. serialNumber: {serialNumber}";
            txtnfcTagID.Text += $"TagDetails:\n {msg}\n\n";
            await ShowAlert(msg, title);
        }
        else
        {
            var first = tagInfo.Records[0];
            await ShowAlert(GetMessage(first), title);
        }
    }

    private void OniOSReadingSessionCancelled(object sender, EventArgs e) =>
        Debug("iOS NFC Session has been cancelled");

    //private async void OnMessagePublished(object sender, ITagInfo tagInfo)
    //{
    //    try
    //    {
    //        ChkReadOnly.IsChecked = false;
    //        await _nfcService.StopPublishingAsync();
    //        if (tagInfo.IsEmpty)
    //            await ShowAlert("Formatting tag operation successful");
    //        else
    //            await ShowAlert("Writing tag operation successful");
    //    }
    //    catch (Exception ex)
    //    {
    //        await ShowAlert(ex.Message);
    //    }
    //}

    private async void OnTagDiscovered(object sender, (ITagInfo TagInfo, bool Format) args)
    {
        if (!_nfcService.IsWritingTagSupported)
        {
            await ShowAlert("Writing tag is not supported on this device");
            return;
        }

        try
        {
            NFCNdefRecord record = null;
            switch (_type)
            {
                case NFCNdefTypeFormat.WellKnown:
                    record = new NFCNdefRecord
                    {
                        TypeFormat = NFCNdefTypeFormat.WellKnown,
                        MimeType = MIME_TYPE,
                        Payload = _nfcService.EncodeToByteArray("Plugin.NFC is awesome!"),
                        LanguageCode = "en"
                    };
                    break;
                case NFCNdefTypeFormat.Uri:
                    record = new NFCNdefRecord
                    {
                        TypeFormat = NFCNdefTypeFormat.Uri,
                        Payload = _nfcService.EncodeToByteArray("https://github.com/franckbour/Plugin.NFC")
                    };
                    break;
                case NFCNdefTypeFormat.Mime:
                    record = new NFCNdefRecord
                    {
                        TypeFormat = NFCNdefTypeFormat.Mime,
                        MimeType = MIME_TYPE,
                        Payload = _nfcService.EncodeToByteArray("Plugin.NFC is awesome!")
                    };
                    break;
                default:
                    break;
            }

            if (!args.Format && record == null)
                throw new Exception("Record can't be null.");

            args.TagInfo.Records = new[] { record };

            if (args.Format)
                await _nfcService.ClearMessageAsync(args.TagInfo);
            else
            {
                await _nfcService.PublishMessageAsync(args.TagInfo, _makeReadOnly);
            }
        }
        catch (Exception ex)
        {
            await ShowAlert(ex.Message);
        }
    }

    // Button click handlers remain mostly the same, but now use the service
    async void Button_Clicked_StartListening(object sender, System.EventArgs e) => await BeginListening();
    async void Button_Clicked_StopListening(object sender, System.EventArgs e) => await StopListening();
    //async void Button_Clicked_StartWriting(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.WellKnown);
    //async void Button_Clicked_StartWriting_Uri(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.Uri);
    //async void Button_Clicked_StartWriting_Custom(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.Mime);
    //async void Button_Clicked_FormatTag(object sender, System.EventArgs e) => await Publish();

    //private async Task Publish(NFCNdefTypeFormat? type = null)
    //{
    //    await StartListeningIfNotiOS();
    //    try
    //    {
    //        _type = NFCNdefTypeFormat.Empty;
    //        if (ChkReadOnly.IsChecked)
    //        {
    //            if (!await DisplayAlert("Warning", "Make a Tag read-only operation is permanent and can't be undone. Are you sure you wish to continue?", "Yes", "No"))
    //            {
    //                ChkReadOnly.IsChecked = false;
    //                return;
    //            }
    //            _makeReadOnly = true;
    //        }
    //        else
    //            _makeReadOnly = false;

    //        if (type.HasValue) _type = type.Value;
    //        await _nfcService.StartPublishingAsync(!type.HasValue);
    //    }
    //    catch (Exception ex)
    //    {
    //        await ShowAlert(ex.Message);
    //    }
    //}

    private string GetMessage(NFCNdefRecord record)
    {
        var message = $"Message: {record.Message}";
        message += Environment.NewLine;
        message += $"RawMessage: {Encoding.UTF8.GetString(record.Payload)}";
        message += Environment.NewLine;
        message += $"Type: {record.TypeFormat}";

        if (!string.IsNullOrWhiteSpace(record.MimeType))
        {
            message += Environment.NewLine;
            message += $"MimeType: {record.MimeType}";
        }

        return message;
    }

    private void Debug(string message) => System.Diagnostics.Debug.WriteLine(message);

    private Task ShowAlert(string message, string title = null) =>
        DisplayAlert(string.IsNullOrWhiteSpace(title) ? ALERT_TITLE : title, message, "OK");

    private async Task StartListeningIfNotiOS()
    {
        if (_isDeviceiOS) return;
        await BeginListening();
    }

    private async Task BeginListening()
    {
        try
        {
            await _nfcService.StartListeningAsync();
            //lblServiceStatus.Text = "NFC Service Status: Listening";
        }
        catch (Exception ex)
        {
            await ShowAlert(ex.Message);
        }
    }

    private async Task StopListening()
    {
        try
        {
            await _nfcService.StopListeningAsync();
           //lblServiceStatus.Text = "NFC Service Status: Stopped";
        }
        catch (Exception ex)
        {
            await ShowAlert(ex.Message);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        UnsubscribeFromNFCEvents();
        Task.Run(async () => await _nfcService.StopListeningAsync());
        return base.OnBackButtonPressed();
    }


    // Helper method to show error and close scanner
    private async void ShowMessageOnScreen(string msgtype, string message)
    {
        await DisplayAlert(msgtype, message, "OK");

    }

    private void OnStartNfcScannerClicked(object sender, EventArgs e)
    {

        if (DeviceIsListening)
        {
            Button_Clicked_StopListening(sender, e);
        }
        else
        {

            Button_Clicked_StartListening(sender, e);         
        }
    }
}