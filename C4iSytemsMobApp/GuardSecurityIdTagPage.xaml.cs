using C4iSytemsMobApp.Interface;
using System.ComponentModel;
using System.Diagnostics;

namespace C4iSytemsMobApp;

public partial class GuardSecurityIdTagPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private int? _clientSiteId;
    private int? _userId;
    private int? _guardId;
    private string savedLicenseNumber;
    private string savedGuardName;
    private string savedBadgeKeyName;
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await GetStoredParameters();
            savedBadgeKeyName = $"{_clientSiteId}_{_guardId}_GuardSelectedBadgeNumber";


            if (!string.IsNullOrEmpty(savedLicenseNumber))
            {
                txtLicenseNumber.Text = savedLicenseNumber;      
            }

            if (!string.IsNullOrEmpty(savedGuardName))
            {
                lblGuardName.Text = $"Hello {savedGuardName}. Please select your badge number and click Enter Log Book";
            }

            var savedBadgeNumber = Preferences.Get(savedBadgeKeyName, "");
            if (!string.IsNullOrEmpty(savedBadgeNumber))
            {
                // Set initial badge number
                lblBadgeNumber.Text = savedBadgeNumber;
                numberPicker.SelectedItem = savedBadgeNumber;                
            }
        }
        catch (Exception ex)
        {
            // Optional: log or display error
            Debug.WriteLine($"Error loading badge number: {ex.Message}");
        }
    }


    public GuardSecurityIdTagPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient(); // Temporary fix
        BindingContext = this;
        LoadLoggedInUser();
        if (AppConfig.ApiBaseUrl.Contains("test") || AppConfig.ApiBaseUrl.Contains("localhost") || AppConfig.ApiBaseUrl.Contains("192.168.1."))
        {
            // Set default license number for test environment
            txtLicenseNumber.Text = "569-829-xxx";
        }

        // Populate the picker with numbers 1 to 999
        for (int i = 1; i <= 999; i++)
        {
            numberPicker.Items.Add(i.ToString("000"));
        }

        // Set initial badge number
        lblBadgeNumber.Text = "000";

    }

    private async Task GetStoredParameters()
    {
        // Validate Client Site ID
        _clientSiteId = await TryGetSecureId("SelectedClientSiteId", "Please select a valid Client Site.");
        _guardId = await TryGetSecureId("GuardId", "Guard ID not found. Please validate the License Number first.");       
        _userId = await TryGetSecureId("UserId", "User ID is invalid. Please log in again.");    
        savedLicenseNumber = Preferences.Get("SavedLicenseNumber", "");
        savedGuardName = Preferences.Get("GuardName", "");

    }
    private async Task<int?> TryGetSecureId(string key, string errorMessage)
    {
        string idString = Preferences.Get(key, "");

        if (string.IsNullOrWhiteSpace(idString) || !int.TryParse(idString, out int id) || id <= 0)
        {
            await DisplayAlert("Validation Error", errorMessage, "OK");
            return null;
        }

        return id;
    }


    private async void LoadLoggedInUser()
    {
        try
        {
            string userName = Preferences.Get("UserName", "");
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

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure you want to log out?", "Yes", "No");
        if (confirm)
        {
            Preferences.Remove("UserId");
            Preferences.Remove("UserName");
            Preferences.Remove("UserRole");
            

            Application.Current.MainPage = new LoginPage();

            // Navigate back to login page
        }
    }

    private void OnBadgeNumberChanged(object sender, EventArgs e)
    {
        if (numberPicker.SelectedIndex != -1)
        {
            lblBadgeNumber.Text = numberPicker.Items[numberPicker.SelectedIndex];
            btnEnterLogbook.IsEnabled = true;
        }
        else { btnEnterLogbook.IsEnabled = false; }
    }


    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle back button logic here
        Application.Current.MainPage = new NavigationPage(new LoginPage());

        // Return true to prevent default behavior (going back)
        return true;
    }


    private async void OnEnterLogbookClicked(object sender, EventArgs e)
    {
        try
        {            
            if (numberPicker.SelectedIndex != -1)
            {
                // Save the selected badge number securely
                var selectedBadgeNumber = numberPicker.Items[numberPicker.SelectedIndex];
                Preferences.Set(savedBadgeKeyName, selectedBadgeNumber);
            }
            else
            {
                await DisplayAlert("Error", "Please select a badge number.", "OK");
                return;
            }

            var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
            Application.Current.MainPage = new MainPage(volumeButtonService);

        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

    }


}

