using C4iSytemsMobApp.Interface;
using Microsoft.Maui.Controls;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace C4iSytemsMobApp;

public partial class GuardLoginQRCode : ContentPage
{
    private readonly HttpClient _httpClient;
    public GuardLoginQRCode()
    {
        InitializeComponent();
        _httpClient = new HttpClient(); // Temporary fix
        LoadLoggedInUser();
    }


    private async void LoadLoggedInUser()
    {
        try
        {
          
            string userName = await SecureStorage.GetAsync("UserName");
            if (!string.IsNullOrEmpty(userName))
            {
                lblLoggedInUser.Text = $"Welcome, {userName}";
            }
            else
            {
                lblLoggedInUser.Text = "Welcome, Guest";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading user: {ex.Message}");
        }
    }
    // Handle the login click
    private async void OnReadClicked(object sender, EventArgs e)
    {
       
        string licenseNumber = txtLicenseNumber.Text;

        // Helper method to display error messages consistently
        void SetErrorMessage(string message)
        {
            lblGuardName.Text = message;
            lblGuardName.TextColor = Colors.Red;
            lblGuardName.IsVisible = true;
        }

        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            SetErrorMessage("Please enter a license number.");
            return;
        }

        try
        {
            activityIndicator.IsRunning = true;  // Show loading indicator
            activityIndicator.IsVisible = true;
            // Validate License Number
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetGuardDetails/{Uri.EscapeDataString(licenseNumber)}";
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                SetErrorMessage($"Invalid License Number. {errorResponse}");
                return;
            }

            var guardData = await response.Content.ReadFromJsonAsync<GuardResponse>();
            if (guardData == null || guardData.GuardId <= 0)
            {
                SetErrorMessage("Invalid License Number.");
                return;
            }

            // Store Guard Details
            await SecureStorage.SetAsync("GuardId", guardData.GuardId.ToString());
            await SecureStorage.SetAsync("GuardName", guardData.Name);

            lblGuardName.Text = $"Hello {guardData.Name}. Please verify your details and click Enter Log Book";
            lblGuardName.TextColor = Colors.Green;
            lblGuardName.IsVisible = true;

            // Validate Client Site ID
            string clientSiteIdString = await SecureStorage.GetAsync("ClientSiteId");
            if (string.IsNullOrWhiteSpace(clientSiteIdString) || !int.TryParse(clientSiteIdString, out int clientSiteId) || clientSiteId <= 0)
            {
                await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
                return;
            }

            // Validate User ID
            string userIdString = await SecureStorage.GetAsync("UserId");
            if (string.IsNullOrWhiteSpace(userIdString) || !int.TryParse(userIdString, out int userId) || userId <= 0)
            {
                await DisplayAlert("Validation Error", "User ID is invalid. Please log in again.", "OK");
                return;
            }

            var apiUrlClientSiteName = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetSiteName?clientsiteId={clientSiteId}";
            var clientSiteNameResponse = await _httpClient.GetAsync(apiUrlClientSiteName);

            if (clientSiteNameResponse.IsSuccessStatusCode)
            {
                var responseContent = await clientSiteNameResponse.Content.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(responseContent))
                {
                    try
                    {
                        using var json = System.Text.Json.JsonDocument.Parse(responseContent);

                        if (json.RootElement.TryGetProperty("siteName", out var siteNameElement))
                        {
                            var siteName = siteNameElement.GetString();
                            if (!string.IsNullOrWhiteSpace(siteName))
                            {
                                await SecureStorage.SetAsync("ClientSite", siteName);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        
                    }
                }
            }









            // Perform Guard Login
            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/EnterGuardLogin?guardId={guardData.GuardId}&clientsiteId={clientSiteId}&userId={userId}";
            var loginResponse = await _httpClient.GetAsync(apiUrl);

            if (loginResponse.IsSuccessStatusCode)
            {
                var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
                Application.Current.MainPage = new MainPage(volumeButtonService);
                //Application.Current.MainPage = new MainPage();
            }
            else
            {
                string errorMessage = await loginResponse.Content.ReadAsStringAsync();
                await DisplayAlert("Error", $"Guard login failed: {errorMessage}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Network error: {ex.Message}", "OK");
        }
        finally
        {
            activityIndicator.IsRunning = false;  // Hide loading indicator
            activityIndicator.IsVisible = false;
        }
    }




}