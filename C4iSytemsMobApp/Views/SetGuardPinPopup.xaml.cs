using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class SetGuardPinPopup : Popup
{
    public SetGuardPinPopup()
    {
        InitializeComponent();
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close("Cancel");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        string newPin = txtNewPinEntry.Text;

        if (string.IsNullOrEmpty(newPin))
        {
            await Application.Current.MainPage.DisplayAlert("Error", "PIN field is required.", "OK");
            return;
        }

        if (newPin.Length < 4 || newPin.Length > 6)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "PIN must be between 4 and 6 digits.", "OK");
            return;
        }

        Close(newPin);
    }
}
