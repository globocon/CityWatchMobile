using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;

namespace C4iSytemsMobApp;

public partial class GuardLoginPage : ContentPage
{
    private readonly HttpClient _httpClient;
    public ObservableCollection<DropdownItem> ClientTypes { get; set; } = new();
    public ObservableCollection<DropdownItem> ClientSites { get; set; } = new();

    private DropdownItem _selectedClientType;
    private DropdownItem _selectedClientSite;
    public DropdownItem SelectedClientType
    {
        get => _selectedClientType;
        set
        {
            if (_selectedClientType != value)
            {
                _selectedClientType = value;
                OnPropertyChanged(nameof(SelectedClientType));

                // Load client sites when a client type is selected
                LoadClientSites(value?.Id ?? 0);
            }
        }
    }

    public DropdownItem SelectedClientSite
    {
        get => _selectedClientSite;
        set
        {
            if (_selectedClientSite != value)
            {
                _selectedClientSite = value;
                OnPropertyChanged(nameof(SelectedClientSite));

                // Store selected client site ID in SecureStorage
                if (_selectedClientSite != null)
                {
                    SecureStorage.SetAsync("SelectedClientSiteId", _selectedClientSite.Id.ToString());
                }
            }
        }
    }
  
    public GuardLoginPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient(); // Temporary fix
        BindingContext = this;
        LoadLoggedInUser();
        LoadDropdownData();
        if (AppConfig.ApiBaseUrl.Contains("test"))
        {
            // Set default license number for test environment
            txtLicenseNumber.Text = "569-829-xxx";
        }

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

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure you want to log out?", "Yes", "No");
        if (confirm)
        {
            SecureStorage.Remove("UserId");
            SecureStorage.Remove("UserName");
            SecureStorage.Remove("UserRole");

            Application.Current.MainPage = new LoginPage();
            
             // Navigate back to login page
        }
    }

    //private async void LoadDropdownData()
    //{
    //    try
    //    {
    //        string userId = await SecureStorage.GetAsync("UserId");

    //        if (string.IsNullOrEmpty(userId))
    //        {
    //            Debug.WriteLine("User ID not found.");
    //            return;
    //        }

    //        var response = await _httpClient.GetFromJsonAsync<List<DropdownItem>>(
    //        //$"https://cws-ir.com/api/GuardSecurityNumber/GetUserClientTypes?userId={Uri.EscapeDataString(userId)}");
    //            $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetUserClientTypes?userId={Uri.EscapeDataString(userId)}");

    //        ClientTypes.Clear();
    //        foreach (var type in response ?? new List<DropdownItem>())
    //        {
    //            ClientTypes.Add(type);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Debug.WriteLine($"Error loading dropdown: {ex.Message}");
    //    }
    //}

    private async void LoadDropdownData()
    {
        try
        {
            string userId = await SecureStorage.GetAsync("UserId");

            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("User ID not found.");
                return;
            }

            var response = await _httpClient.GetFromJsonAsync<List<DropdownItem>>(
                $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetUserClientTypes?userId={Uri.EscapeDataString(userId)}");

            ClientTypes.Clear();

            foreach (var type in response ?? new List<DropdownItem>())
            {
                if(type.Name!="Select")
                ClientTypes.Add(type);
            }

            // If only one item exists, select it and remove from the list
            if (ClientTypes.Count == 1)
            {
                SelectedClientType = ClientTypes[0];  // Automatically select the first item
                textBoxSelectedClientType.Text = SelectedClientType.Name;
                //textBoxSelectedClientType.IsVisible = true;// Set TextBox value
               
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading dropdown: {ex.Message}");
        }
    }


    private async void LoadClientSites(int clientTypeId)
    {
        if (clientTypeId == 0) return;

        try
        {
            // Retrieve the logged-in user's ID from SecureStorage
            string userId = await SecureStorage.GetAsync("UserId");
            if (string.IsNullOrEmpty(userId)) return;

            // Use both userId and clientTypeId in the request
            var response = await _httpClient.GetFromJsonAsync<List<DropdownItem>>(
                //$"https://cws-ir.com/api/GuardSecurityNumber/GetClientSitesByClientType?userId={userId}&clientTypeId={clientTypeId}");
              $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetClientSitesByClientType?userId={Uri.EscapeDataString(userId)}&clientTypeId={clientTypeId}");


            ClientSites.Clear();
            foreach (var site in response ?? new List<DropdownItem>())
            {
                ClientSites.Add(site);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading client sites: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected override bool OnBackButtonPressed()
    {
        // Handle back button logic here
        Application.Current.MainPage = new NavigationPage(new LoginPage());

        // Return true to prevent default behavior (going back)
        return true;
    }


    private async void OnReadClicked(object sender, EventArgs e)
    {
        string licenseNumber = txtLicenseNumber.Text;

        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            lblGuardName.Text = "Please enter a license number.";
            lblGuardName.TextColor = Colors.Red;
            lblGuardName.IsVisible = true;
            return;
        }

        try
        {
            // Call API to validate the license number
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetGuardDetails/{Uri.EscapeDataString(licenseNumber)}";
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var guardData = await response.Content.ReadFromJsonAsync<GuardResponse>();

                if (guardData != null && guardData.GuardId > 0) // Ensure guardId is valid
                {
                    // Store guardId securely
                    await SecureStorage.SetAsync("GuardId", guardData.GuardId.ToString());

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        lblGuardName.Text = $"Hello {guardData.Name}. Please verify your details and click Enter Log Book";
                        SecureStorage.SetAsync("GuardName",guardData.Name);
                        lblGuardName.TextColor = Colors.Green;
                        lblGuardName.IsVisible = true;

                        // Enable the dropdowns and Enter Logbook button
                        //pickerClientType.IsEnabled = true;
                        //pickerClientSite.IsEnabled = true;
                        //btnEnterLogbook.IsEnabled = true;

                        // Show the hidden controls

                        if (!string.IsNullOrWhiteSpace(textBoxSelectedClientType.Text))
                        {
                            pickerClientType.IsVisible = false;
                            textBoxSelectedClientType.IsVisible = true;
                        }
                        else
                        {
                            pickerClientType.IsVisible = true;
                            textBoxSelectedClientType.IsVisible = false;
                        }




                        pickerClientSite.IsVisible = true;
                        btnEnterLogbook.IsVisible = true;
                        ToggleInstructionalTextVisibility();
                        // Disable the license number Entry and Read button
                        txtLicenseNumber.IsEnabled = false;
                        ((Button)sender).IsEnabled = false;
                    });
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        lblGuardName.Text = "Invalid License Number";
                        lblGuardName.TextColor = Colors.Red;
                        lblGuardName.IsVisible = true;
                    });
                }
            }
            else
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    lblGuardName.Text = $"Invalid License Number. {errorResponse}";
                    lblGuardName.TextColor = Colors.Red;
                    lblGuardName.IsVisible = true;
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Network error: " + ex.Message, "OK");
        }
    }


    private void ToggleInstructionalTextVisibility()
    {
        // If ANY of these elements are visible, hide the instructional text
        if (pickerClientType.IsVisible || pickerClientSite.IsVisible || btnEnterLogbook.IsVisible || textBoxSelectedClientType.IsVisible)
        {
            instructionalFrame.IsVisible = false;
            instructionalTextContainer.IsVisible = false;
            ConsentSection.IsVisible = false; // Hide the consent section
        }
        else
        {
            instructionalFrame.IsVisible = true;
            instructionalTextContainer.IsVisible = true;
            ConsentSection.IsVisible = true;
        }
    }

    private async void OnEnterLogbookClicked(object sender, EventArgs e)
    {
        try
        {
            // Retrieve GuardId securely
            string guardIdString = await SecureStorage.GetAsync("GuardId");
            if (string.IsNullOrWhiteSpace(guardIdString) || !int.TryParse(guardIdString, out int guardId) || guardId <= 0)
            {
                await DisplayAlert("Error", "Guard ID not found. Please validate the License Number first.", "OK");
                return;
            }



            // Validate Client Type
            // Check if the Picker selected item is null or "Select"
            // Validate Client Type
            bool isInvalid = false;

            if (textBoxSelectedClientType.IsVisible)
            {
                // Validate TextBox only
                isInvalid = string.IsNullOrWhiteSpace(textBoxSelectedClientType.Text) ||
                            textBoxSelectedClientType.Text.Trim().Equals("Select", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Validate Picker only
                isInvalid = pickerClientType.SelectedItem == null ||
                            pickerClientType.SelectedItem.ToString().Trim().Equals("Select", StringComparison.OrdinalIgnoreCase);
            }

            if (isInvalid)
            {
                await DisplayAlert("Validation Error", "Please select a valid Client Type.", "OK");
                return;
            }


            // Validate Client Site
            if (pickerClientSite.SelectedItem == null || pickerClientSite.SelectedItem.ToString().Trim().Equals("Select", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
                return;
            }

            // Retrieve and validate Client Site ID
            string clientSiteIdString = await SecureStorage.GetAsync("SelectedClientSiteId");
            if (string.IsNullOrWhiteSpace(clientSiteIdString) || !int.TryParse(clientSiteIdString, out int clientSiteId) || clientSiteId <= 0)
            {
                await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
                return;
            }

            // Retrieve and validate User ID
            string userIdString = await SecureStorage.GetAsync("UserId");
            if (string.IsNullOrWhiteSpace(userIdString) || !int.TryParse(userIdString, out int userId) || userId <= 0)
            {
                await DisplayAlert("Validation Error", "User ID is invalid. Please log in again.", "OK");
                return;
            }

            // API URL
            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/EnterGuardLogin?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}";

            //string apiUrl = $"https://cws-ir.com/api/GuardSecurityNumber/EnterGuardLogin?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}";

            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    if (pickerClientSite.SelectedItem is DropdownItem selectedClientSite)
                    {
                        await SecureStorage.SetAsync("ClientSite", selectedClientSite.Name.Trim());
                    }

                    string selectedClientTypeName = null;

                    if (textBoxSelectedClientType.IsVisible && !string.IsNullOrWhiteSpace(textBoxSelectedClientType.Text))
                    {
                        selectedClientTypeName = textBoxSelectedClientType.Text.Trim();
                    }
                    else if (pickerClientType.SelectedItem is DropdownItem selectedClientType)
                    {
                        selectedClientTypeName = selectedClientType.Name.Trim();
                    }

                    if (!string.IsNullOrEmpty(selectedClientTypeName))
                    {
                        await SecureStorage.SetAsync("ClientType", selectedClientTypeName);
                    }

                    Application.Current.MainPage = new MainPage();
                    // await Shell.Current.GoToAsync("//Multimedia");
                    //await DisplayAlert("Success", "Guard successfully logged in.", "OK");
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Error", $"Guard login failed: {errorMessage}", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

    }




   

}

// Model class for API response
public class GuardResponse
{
    public string Name { get; set; }
    public int GuardId { get; set; }
}
public class DropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; }
}