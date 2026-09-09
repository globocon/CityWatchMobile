using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class SetGuardPinPopup : Popup<string>
{
    public SetGuardPinPopup()
    {
        InitializeComponent();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync("Cancel");
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

        if (!newPin.All(char.IsDigit))
        {
            await Application.Current.MainPage.DisplayAlert("Error", "PIN must contain only numbers.", "OK");
            return;
        }

        await CloseAsync(newPin);
    }
}
