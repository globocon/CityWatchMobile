using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Views;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace C4iSytemsMobApp;

public partial class AppUpgradePage : ContentPage
{
    private readonly IAppUpdateService _appUpdateService;

    public AppUpgradePage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        BindingContext = this;
        _appUpdateService = IPlatformApplication.Current.Services.GetService<IAppUpdateService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        lblAppVersion.Text = $"Current Version ({App.CurrentAppVersion})";
        AnimateDots();
        await CheckForUpdatesAsync();
    }

    private async void AnimateDots()
    {
        string[] frames = { ".", "..", "...", "" };

        while (true)
        {
            foreach (var f in frames)
            {
                lblDots.Text = f;
                await Task.Delay(400);
            }
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {

#if !DEBUG

                        // Show loading overlay
                        LoadingOverlay.IsVisible = true;
                        // Check for updates after UI is ready
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await Task.Delay(1500); // give a moment for UI to load
                            bool updateAvailable = await _appUpdateService.CheckForUpdateAsync();
                            if (!updateAvailable)
                            {
                                // Hide loading overlay
                                LoadingOverlay.IsVisible = false;
                                // Redirect to login page
                                //MainPage = new NavigationPage(loginPage);
                                Application.Current.MainPage = new LoginPage();
                            }
                        });

                        // Hide loading overlay
                        LoadingOverlay.IsVisible = false;

#else
            Application.Current.MainPage = new LoginPage();
#endif


        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to check for updates: {ex.Message}", "OK");
        }
        finally
        {
            // Hide loading overlay
            LoadingOverlay.IsVisible = false;
        }

    }



}
