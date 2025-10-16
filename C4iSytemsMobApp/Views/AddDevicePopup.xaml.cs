
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class AddDevicePopup : Popup
{
    public AddDevicePopup()
    {
        InitializeComponent();
    }

    private void OnAddNfcClicked(object sender, EventArgs e)
    {
        Close("NFC");
    }

    private void OnAddIBeaconClicked(object sender, EventArgs e)
    {
        Close("IBeacon");
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close("Cancel");
    }
}
