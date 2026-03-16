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
        string confirmPin = txtConfirmPinEntry.Text;

        if (string.IsNullOrEmpty(newPin) || string.IsNullOrEmpty(confirmPin))
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Both PIN fields are required.", "OK");
            return;
        }

        if (newPin.Length < 4 || newPin.Length > 6)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "PIN must be between 4 and 6 digits.", "OK");
            return;
        }

        if (newPin != confirmPin)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "PINs do not match.", "OK");
            return;
        }

        Close(newPin);
    }
}
