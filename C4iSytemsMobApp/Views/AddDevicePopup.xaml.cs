
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class AddDevicePopup : Popup
{
    public AddDevicePopup()
    {
        InitializeComponent();

        /* iBeacon work is Android only. The BLE scanning it depends on runs through
           IBeaconScanner's foreground scan loop, which the app only wires up and permissions
           on Android; on iOS the options would open pages that can never find a device. Hidden
           rather than disabled - a permanently greyed button invites the question "why". */
        var isAndroid = DeviceInfo.Platform == DevicePlatform.Android;
        ButtonAddIBeacon.IsVisible = isAndroid;
        ButtonEditIBeacon.IsVisible = isAndroid;
    }

    private void OnAddNfcClicked(object sender, EventArgs e)
    {
        Close("NFC");
    }

    private void OnAddIBeaconClicked(object sender, EventArgs e)
    {
        Close("IBeacon");
    }

    private void OnEditNfcClicked(object sender, EventArgs e)
    {
        Close("EditNFC");
    }

    private void OnEditIBeaconClicked(object sender, EventArgs e)
    {
        Close("EditIBeacon");
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close("Cancel");
    }
}
