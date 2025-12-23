
using C4iSytemsMobApp.Interface;
using CommunityToolkit.Maui.Views;

namespace C4iSytemsMobApp.Views;

public partial class CheckGuardPinPopup : Popup
{
    private readonly IGuardApiServices _guardApiServices;
    public CheckGuardPinPopup()
    {
        InitializeComponent();
        _guardApiServices = IPlatformApplication.Current.Services.GetService<IGuardApiServices>();
    }


    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close("Cancel");
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        // Check for PIN validity
        if (string.IsNullOrEmpty(txtPinEntry.Text))
        {
            await Application.Current.MainPage.DisplayAlert("Error", "PIN is required", "OK");
            return;
        }

        bool PinValidated = false;
        string errorMessage = string.Empty;
        (PinValidated, errorMessage) = await _guardApiServices.ValidateGuardDocumentAccessPin(txtPinEntry.Text);
        if(PinValidated == false)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"PIN validation failed: {errorMessage}", "OK");
            return;
        }

        Close("OK");
    }

    private void OnForgotPasswordClicked(object sender, EventArgs e)
    {
        // Close("ForgotPassword");
    }
}
