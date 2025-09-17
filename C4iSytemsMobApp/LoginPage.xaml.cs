using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using Microsoft.Maui.Devices.Sensors;

namespace C4iSytemsMobApp;

public partial class LoginPage : ContentPage
{
    private readonly HttpClient _httpClient;

    public LoginPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        lblAppVersion.Text = $"Version {GetAppVersion()}";       
        LoadSavedCredentials();
       


    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var savedUsername = await SecureStorage.GetAsync("SavedUsername");
            var savedPassword = await SecureStorage.GetAsync("SavedPassword");

            if (!string.IsNullOrEmpty(savedUsername) && !string.IsNullOrEmpty(savedPassword))
            {
                usernameEntry.Text = savedUsername;
                passwordEntry.Text = savedPassword;
                rememberMeCheckBox.IsChecked = true;
            }
        }
        catch (Exception ex)
        {
            // Handle case where secure storage is unavailable
            await DisplayAlert("Error", "Unable to load saved credentials.", "OK");
        }
    }




    private async void OnPageLoaded(object sender, EventArgs e)
    {
        await GetAndShowLocationAsync();
    }
    private async void LoadSavedCredentials()
    {
        try
        {
            string savedUsername = await SecureStorage.GetAsync("SavedUsername");
            string savedPassword = await SecureStorage.GetAsync("SavedPassword");

            if (AppConfig.ApiBaseUrl.Contains("test") || AppConfig.ApiBaseUrl.Contains("localhost") || AppConfig.ApiBaseUrl.Contains("192.168.1."))
            {
                // Apply default test credentials if the API base URL is for testing
                usernameEntry.Text = savedUsername ?? "martha_cove";
                passwordEntry.Text = savedPassword ?? "Qwerty123$";
            }
            else
            {
                // Apply saved credentials if available
                usernameEntry.Text = savedUsername;
                passwordEntry.Text = savedPassword;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading saved credentials: {ex.Message}");
        }
    }


    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string username = usernameEntry.Text?.Trim();
        string password = passwordEntry.Text?.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            await DisplayAlert("Error", "Please enter a username and password.", "OK");
            return;
        }

        // Prevent admin users from logging in
        if (username.Equals("cwsadmin", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Error", "Admin login not allowed.", "OK");
            return;
        }

        // Show loading indicator
        loadingIndicator.IsVisible = true;
        loadingIndicator.IsRunning = true;

        var loginResponse = await ValidateLoginAsync(username, password);

        loadingIndicator.IsVisible = false;
        loadingIndicator.IsRunning = false;

        if (loginResponse.IsSuccess)
        {
            var crowdControlSettingsService = IPlatformApplication.Current.Services.GetService<ICrowdControlServices>();
            var scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
            Application.Current.MainPage = new GuardLoginPage(crowdControlSettingsService, scannerControlServices);
        }
        else
        {
            await DisplayAlert("Error", loginResponse.ErrorMessage, "OK");
        }
    }


    private async Task<LoginResponse> ValidateLoginAsync(string username, string password)
    {
        try
        {
            var apiUrl = $"{AppConfig.ApiBaseUrl}Auth/login";
            
            var loginData = new { UserName = username, Password = password };

            using (HttpClient client = new HttpClient()) // Ensures HttpClient is always available
            {
                HttpResponseMessage response = await client.PostAsJsonAsync(apiUrl, loginData);
                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadFromJsonAsync<LoginResponse>();

                    if (responseData != null)
                    {
                        await SecureStorage.SetAsync("UserId", responseData.UserId.ToString());
                        await SecureStorage.SetAsync("UserName", responseData.Name);
                        await SecureStorage.SetAsync("UserRole", responseData.Role);
                    }


                    if (rememberMeCheckBox.IsChecked)
                    {
                        await SecureStorage.SetAsync("SavedUsername", username);
                        await SecureStorage.SetAsync("SavedPassword", password);
                    }
                    else
                    {
                        SecureStorage.Remove("SavedUsername");
                        SecureStorage.Remove("SavedPassword");
                    }

                    return new LoginResponse { IsSuccess = true };
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    return new LoginResponse { IsSuccess = false, ErrorMessage = errorMessage };
                }
            }
        }
        catch (Exception ex)
        {
            return new LoginResponse { IsSuccess = false, ErrorMessage = "Network error: " + ex.Message };
        }
    }


    private async void OnQRCodeLoginClicked(object sender, EventArgs e)
    {
       await Navigation.PushAsync(new QrScannerPage());
    }

    //private async void OnNfcTestClicked(object sender, EventArgs e)
    //{
    //    var nfcScannerTestPage = IPlatformApplication.Current.Services.GetService<INfcService>();
    //    Application.Current.MainPage = new NfcScannerTestPage(nfcScannerTestPage);
    //}

    private string GetAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "1.20.0";
    }


    protected override bool OnBackButtonPressed()
    {
        bool isConfirmed = false;

        Device.BeginInvokeOnMainThread(async () =>
        {
            isConfirmed = await DisplayAlert("Exit", "Do you want to close the app?", "Yes", "No");

            if (isConfirmed)
            {
                SecureStorage.RemoveAll(); // Clear SecureStorage (logout)
                System.Diagnostics.Process.GetCurrentProcess().Kill(); // Close the app
            }
        });

        return true; // Prevent default back button behavior
    }



    private async Task GetAndShowLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                // Inform the user why the permission is needed
                bool answer = await DisplayAlert("Permission Required",
                    "This app needs your location to continue. Please allow location access.", "OK", "Cancel");

                if (!answer)
                {
                    //locationLabel.Text = "Location permission denied by user.";
                    return;
                }

                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                if (AppConfig.ApiBaseUrl.Contains("test") || AppConfig.ApiBaseUrl.Contains("localhost") || AppConfig.ApiBaseUrl.Contains("192.168.1."))
                {
                    await SecureStorage.SetAsync("GpsCoordinates", "40.748440,-73.984559");

                }
                //locationLabel.Text = "Location permission not granted. Please enable it in app settings.";
                return;
            }

           

            var location = await Geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Medium,
                Timeout = TimeSpan.FromSeconds(10)
            });

            if (location != null)
            {

                await SecureStorage.SetAsync("GpsCoordinates", location.Latitude.ToString() +','+location.Longitude.ToString());
                //locationLabel.Text = $"Latitude: {location.Latitude}, Longitude: {location.Longitude}";
            }
            else
            {
                
                //locationLabel.Text = "Unable to retrieve location.";
            }
        }
        catch (Exception ex)
        {
            //locationLabel.Text = $"Error: {ex.Message}";
        }
    }









}

public class LoginResponse
{
    public int UserId { get; set; }
    public string Name { get; set; }
    public string Role { get; set; }
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
}