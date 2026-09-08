
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class AddDevicePopup : Popup<string>
{
    public AddDevicePopup()
    {
        InitializeComponent();
    }

    private async void OnAddNfcClicked(object sender, EventArgs e)
    {
        await CloseAsync("NFC");
    }

    private async void OnAddIBeaconClicked(object sender, EventArgs e)
    {
        await CloseAsync("IBeacon");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync("Cancel");
    }
}
