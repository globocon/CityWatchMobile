
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class AddDevicePopup : Popup
{
    public AddDevicePopup()
    {
        InitializeComponent();

        /* Both iBeacon options are hidden on this branch, on every target - not just off
           Android as on master. Bluetooth is not part of this build at all: IBeaconScanner and
           the Plugin.BLE reference are both absent (a39cf66), so there is nothing behind either
           button anywhere. Hidden rather than disabled - a permanently greyed button invites
           the question "why". MenuSettingsPage still refuses both actions as a backstop. */
        ButtonAddIBeacon.IsVisible = false;
        ButtonEditIBeacon.IsVisible = false;
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
