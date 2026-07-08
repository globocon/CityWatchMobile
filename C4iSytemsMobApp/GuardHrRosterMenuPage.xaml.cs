using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp;

public partial class GuardHrRosterMenuPage : ContentPage
{
    public GuardHrRosterMenuPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        var guardName = Preferences.Get("GuardName", string.Empty);
        lblGuardName.Text = string.IsNullOrWhiteSpace(guardName) ? string.Empty : $"Welcome, {guardName}";
        lblGuardName.IsVisible = !string.IsNullOrWhiteSpace(guardName);
    }

    private async void OnNotificationsClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Notification", "New feature coming soon. Stay tuned!", "OK");
    }

    private void OnHrRecordsClicked(object sender, EventArgs e)
    {
        // PIN was already verified before entering this menu
        Application.Current.MainPage = new HrRecordsPage();
    }

    private void OnGuardRosterClicked(object sender, EventArgs e)
    {
        // RosterPage detects HrRosterOnlyMode and loads the guard's roster across all sites
        Application.Current.MainPage = new RosterPage();
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        Preferences.Remove("HrRosterOnlyMode");
        Preferences.Remove("GuardId");
        Preferences.Remove("GuardName");
        Preferences.Remove("LicenseNumber");

        var crowdControlSettingsService = IPlatformApplication.Current.Services.GetService<ICrowdControlServices>();
        var scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
        Application.Current.MainPage = new GuardLoginPage(crowdControlSettingsService, scannerControlServices);
    }
}
