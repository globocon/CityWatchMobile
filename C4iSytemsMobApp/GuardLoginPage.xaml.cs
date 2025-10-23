using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace C4iSytemsMobApp;

public partial class GuardLoginPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private readonly IScannerControlServices _scannerControlServices;
    private readonly ICrowdControlServices _crowdControlServices;
    private readonly INfcService _nfcService;

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

                // Load sites for this type
                LoadClientSites(value?.Id ?? 0);

                // Save selected type
                if (_selectedClientType != null)
                    Preferences.Set("SelectedClientTypeId", _selectedClientType.Id.ToString());
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

                if (_selectedClientSite != null)
                    Preferences.Set("SelectedClientSiteId", _selectedClientSite.Id.ToString());
            }
        }
    }

    #region "NFC Properties"

    public const string ALERT_TITLE = "NFC";

    private bool _isDeviceiOS = false;
    public bool DeviceIsListening
    {
        get => _deviceIsListening;
        set
        {
            _deviceIsListening = value;
            OnPropertyChanged(nameof(DeviceIsListening));
        }
    }

    private bool _deviceIsListening;

    private bool _nfcIsEnabled;
    public bool NfcIsEnabled
    {
        get => _nfcIsEnabled;
        set
        {
            _nfcIsEnabled = value;
            OnPropertyChanged(nameof(NfcIsEnabled));
            OnPropertyChanged(nameof(NfcIsDisabled));
        }
    }

    public bool NfcIsDisabled => !NfcIsEnabled;

    #endregion  "NFC Properties"


    private const string PrefKey = "SavedLicenseNumbers";
    private List<string> _previousNumbers = new List<string>();
    private ObservableCollection<string> _filteredSuggestions = new ObservableCollection<string>();

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var savedLicenseNumber = Preferences.Get("SavedLicenseNumber","");

            if (!string.IsNullOrEmpty(savedLicenseNumber))
            {
                //txtLicenseNumber.Text = savedLicenseNumber;
                //rememberMeCheckBox.IsChecked = true;
            }
        }
        catch (Exception ex)
        {
            // Optional: log or display error
        }

        // Check NFC status
        if (_nfcService.IsSupported)
        {
            if (_nfcService.IsAvailable)
            {
                NfcIsEnabled = _nfcService.IsEnabled;

                if (DeviceInfo.Platform == DevicePlatform.iOS)
                    _isDeviceiOS = true;

                await InitializeNFCAsync();
            }
        }

        LoadSavedNumbers(); // Reload preferences every time page appears
    }


    public GuardLoginPage(ICrowdControlServices crowdControlServices, IScannerControlServices scannerControlServices)
    {
        InitializeComponent();
        _httpClient = new HttpClient(); // Temporary fix
        _crowdControlServices = crowdControlServices;
        _scannerControlServices = scannerControlServices;
        _nfcService = IPlatformApplication.Current.Services.GetService<INfcService>();

        BindingContext = this;
        LoadLoggedInUser();
        //LoadDropdownData();
        if (AppConfig.ApiBaseUrl.Contains("test") || AppConfig.ApiBaseUrl.Contains("localhost") || AppConfig.ApiBaseUrl.Contains("192.168.1."))
        {
            // Set default license number for test environment
            txtLicenseNumber.Text = "569-829-xxx";
        }
        // Set ItemsSource for suggestions
        SuggestionsView.ItemsSource = _filteredSuggestions;

        // Load previously saved numbers
        LoadSavedNumbers();
        
       //RestorePreviousSelection();
    }

    private void LoadSavedNumbers()
    {
        var json = Preferences.Get(PrefKey, string.Empty);
        _previousNumbers = string.IsNullOrEmpty(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json);
    }

    private void SaveNumbers()
    {
        var json = JsonSerializer.Serialize(_previousNumbers);
        Preferences.Set(PrefKey, json);
    }

    private void SaveNewNumber(string newNumber)
    {
        if (string.IsNullOrWhiteSpace(newNumber))
            return;

        // Load existing numbers from Preferences
        var json = Preferences.Get(PrefKey, string.Empty);
        _previousNumbers = string.IsNullOrEmpty(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json);

        // Remove if already exists to avoid duplicates
        _previousNumbers.RemoveAll(x => x.Equals(newNumber, StringComparison.OrdinalIgnoreCase));

        // Insert at the beginning so it appears first
        _previousNumbers.Insert(0, newNumber);

        // Save updated list to Preferences
        SaveNumbers();
    }


    private void OnLicenseTextChanged(object sender, TextChangedEventArgs e)
    {
        string query = e.NewTextValue?.Trim() ?? "";

        var matches = string.IsNullOrEmpty(query)
            ? _previousNumbers
            : _previousNumbers.Where(x => x.Contains(query, StringComparison.OrdinalIgnoreCase))
                              .ToList();

        // Clear and re-add to ObservableCollection
        _filteredSuggestions.Clear();
        foreach (var item in matches)
            _filteredSuggestions.Add(item);

        SuggestionsView.IsVisible = _filteredSuggestions.Any();
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        _filteredSuggestions.Clear();
        foreach (var item in _previousNumbers)
            _filteredSuggestions.Add(item);

        SuggestionsView.IsVisible = _filteredSuggestions.Any();
    }

    private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is string selectedNumber)
        {
            SuggestionsView.IsVisible = false;
            txtLicenseNumber.Text = selectedNumber;
           
        }

        ((CollectionView)sender).SelectedItem = null;
    }

    private void RestorePreviousSelection()
    {
        string savedTypeId = Preferences.Get("SelectedClientTypeId", string.Empty);
        if (!string.IsNullOrEmpty(savedTypeId) && ClientTypes.Any())
        {
            var typeItem = ClientTypes.FirstOrDefault(x => x.Id.ToString() == savedTypeId);
            if (typeItem != null)
            {
                // Temporarily show picker to allow binding to update
                pickerClientType.IsVisible = true;

                SelectedClientType = typeItem;
                pickerClientType.SelectedItem = typeItem;
                // Update read-only Entry


            }
        }

        // Optionally restore ClientSite in a similar way
        
    }



    private void RestorePreviousClientSite()
    {
        string savedSiteId = Preferences.Get("SelectedClientSiteId", string.Empty);

        if (!string.IsNullOrEmpty(savedSiteId) && ClientSites?.Any() == true)
        {
            var siteItem = ClientSites.FirstOrDefault(x => x.Id.ToString() == savedSiteId);
            if (siteItem != null)
            {
                pickerClientSite.IsVisible = true;
                SelectedClientSite = siteItem;
                pickerClientSite.SelectedItem = siteItem;

               

                
            }
            else
            {
               
            }
        }
        else
        {
           
        }
    }



    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        //await StopListening();
    }


    private async void LoadLoggedInUser()
    {
        try
        {
            string userName = Preferences.Get("UserName","");
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

    //private async void LoadDropdownData()
    //{
    //    try
    //    {
    //        string userId = Preferences.Get("UserId","");

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

    private async Task LoadDropdownData()
    {
        try
        {
            string userId = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(userId)) return;

            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetUserClientTypes?userId={Uri.EscapeDataString(userId)}";
            var response = await _httpClient.GetFromJsonAsync<List<DropdownItem>>(url);

            if (response == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ClientTypes.Clear();

                foreach (var type in response.Where(t => t.Name != "Select").OrderBy(t => t.Name))
                    ClientTypes.Add(type);

                // Optional: auto-select if only one item
                if (ClientTypes.Count == 1)
                {
                    SelectedClientType = ClientTypes[0];
                    textBoxSelectedClientType.Text = SelectedClientType.Name;
                }
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Failed to load client types: " + ex.Message, "OK");
        }
    }




    private async void LoadClientSites(int clientTypeId)
    {
        if (clientTypeId == 0) return;

        try
        {
            string userId = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(userId)) return;

            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetClientSitesByClientType?userId={Uri.EscapeDataString(userId)}&clientTypeId={clientTypeId}";
            var response = await _httpClient.GetFromJsonAsync<List<DropdownItem>>(url);

            ClientSites.Clear();
            foreach (var site in response ?? new List<DropdownItem>())
            {
                if (site.Name != "Select")
                    ClientSites.Add(site);
            }

            Debug.WriteLine($"ClientSites loaded: {ClientSites.Count}");

            // Wait for UI to update bindings before restoring
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RestorePreviousClientSite();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading client sites: {ex.Message}");
        }
    }






    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected override bool OnBackButtonPressed()
    {
        UnsubscribeFromNFCEvents();
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
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetGuardDetails/{Uri.EscapeDataString(licenseNumber)}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                lblGuardName.Text = $"{errorResponse ?? "Error: Invalid License Number."}";
                lblGuardName.TextColor = Colors.Red;
                lblGuardName.IsVisible = true;
                return;
            }

            var guardData = await response.Content.ReadFromJsonAsync<GuardResponse>();

            if (guardData == null || guardData.GuardId <= 0)
            {
                lblGuardName.Text = "Invalid License Number";
                lblGuardName.TextColor = Colors.Red;
                lblGuardName.IsVisible = true;
                return;
            }

            // Valid guard, proceed
            LoadSavedNumbers();
            SaveNewNumber(licenseNumber);
            SuggestionsView.IsVisible = false;
            Preferences.Set("GuardId", guardData.GuardId.ToString());
            Preferences.Set("GuardName", guardData.Name);
            Preferences.Set("LicenseNumber", licenseNumber);

            // Disable entry and button
            txtLicenseNumber.IsEnabled = false;
            ((Button)sender).IsEnabled = false;

            lblGuardName.Text = $"Hello {guardData.Name}. Please verify your details and click Enter Log Book";
            lblGuardName.TextColor = Colors.Green;
            lblGuardName.IsVisible = true;

            // Show UI controls
            pickerClientType.IsVisible = true;
            pickerClientSite.IsVisible = true;
            btnEnterLogbook.IsVisible = true;
            ToggleInstructionalTextVisibility();

            // Load client types properly
            await LoadDropdownData(); // <-- await here, make LoadDropdownData async Task

            RestorePreviousSelection();

            if (SelectedClientType != null)
                pickerClientSite.IsVisible = true;
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
            string guardIdString = Preferences.Get("GuardId", "");
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
            string clientSiteIdString = Preferences.Get("SelectedClientSiteId", "");
            if (string.IsNullOrWhiteSpace(clientSiteIdString) || !int.TryParse(clientSiteIdString, out int clientSiteId) || clientSiteId <= 0)
            {
                await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
                return;
            }

            // Retrieve and validate User ID
            string userIdString = Preferences.Get("UserId", "");
            if (string.IsNullOrWhiteSpace(userIdString) || !int.TryParse(userIdString, out int userId) || userId <= 0)
            {
                await DisplayAlert("Validation Error", "User ID is invalid. Please log in again.", "OK");
                return;
            }

            string gpsCoordinates = Preferences.Get("GpsCoordinates", "");

            if (string.IsNullOrWhiteSpace(gpsCoordinates))
            {
                await DisplayAlert("Location Error", "GPS coordinates not available. Please ensure location services are enabled.", "OK");
                return;
            }

            // API URL
            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/EnterGuardLogin" +
                         $"?guardId={guardId}" +
                         $"&clientsiteId={clientSiteId}" +
                         $"&userId={userId}" +
                         $"&gps={Uri.EscapeDataString(gpsCoordinates)}";

            //string apiUrl = $"https://cws-ir.com/api/GuardSecurityNumber/EnterGuardLogin?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}";


            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    if (pickerClientSite.SelectedItem is DropdownItem selectedClientSite)
                    {
                        Preferences.Set("ClientSite", selectedClientSite.Name.Trim());
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
                        Preferences.Set("ClientType", selectedClientTypeName);
                    }

                    // Check if NFC is onboarded for site
                    var scannerSettings = await _scannerControlServices.CheckScannerOnboardedAsync(clientSiteIdString);
                    if (scannerSettings != null && scannerSettings.Count > 0)
                    {
                        // If scanner is onboarded, navigate to NFC page
                        if (scannerSettings.Exists(x => x.ToLower().Equals("nfc")))
                        {
                            Preferences.Set("NfcOnboarded", "true");
                            //check if nfc is available in the device
                            //check if nfc is enabled
                            if (_nfcService.IsSupported && _nfcService.IsAvailable && !NfcIsEnabled)
                            {
                                await DisplayAlert(ALERT_TITLE, "NFC is disabled. Please enable NFC to proceed.", "OK");
                                return;
                            }

                            if (_nfcService.IsSupported && _nfcService.IsAvailable && NfcIsEnabled) { UnsubscribeFromNFCEvents(); }
                        }
                        else
                        {
                            Preferences.Set("NfcOnboarded", "false");
                        }
                    }

                    Preferences.Set("CrowdCountEnabledForSite", "false");
                    var _crowdControlsettings = await _crowdControlServices.GetCrowdControlSettingsAsync(clientSiteIdString);
                    if (_crowdControlsettings != null && (_crowdControlsettings?.IsCrowdCountEnabled ?? false))
                    {
                        if (_crowdControlsettings.IsCrowdCountEnabled)
                        {
                            Preferences.Set("CrowdCountEnabledForSite", "true");
                            Application.Current.MainPage = new GuardSecurityIdTagPage();
                        }
                        else
                        {                            
                            var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
                            Application.Current.MainPage = new MainPage(volumeButtonService);
                        }
                    }
                    else
                    {
                        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
                        Application.Current.MainPage = new MainPage(volumeButtonService);
                    }

                    //Application.Current.MainPage = new MainPage();
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


    #region "NFC Methods"
    private async Task InitializeNFCAsync()
    {
        try
        {
            await _nfcService.InitializeAsync();
            SubscribeToNFCEvents();            
            // Some delay to prevent Java.Lang.IllegalStateException on Android
            await Task.Delay(500);
            //await StartListeningIfNotiOS();
        }
        catch (Exception ex)
        {
            await DisplayAlert(ALERT_TITLE, $"Failed to initialize NFC: {ex.Message}", "OK");
        }
    }

    private void SubscribeToNFCEvents()
    {
        //_nfcService.OnMessageReceived += OnMessageReceived;
        //_nfcService.OnMessagePublished += OnMessagePublished;
        //_nfcService.OnTagDiscovered += OnTagDiscovered;
        _nfcService.OnNfcStatusChanged += OnNfcStatusChanged;
        //_nfcService.OnTagListeningStatusChanged += OnTagListeningStatusChanged;        

        if (_isDeviceiOS)
            _nfcService.OniOSReadingSessionCancelled += OniOSReadingSessionCancelled;
    }

    private void UnsubscribeFromNFCEvents()
    {
        //_nfcService.OnMessageReceived -= OnMessageReceived;
        //_nfcService.OnMessagePublished -= OnMessagePublished;
        //_nfcService.OnTagDiscovered -= OnTagDiscovered;
        _nfcService.OnNfcStatusChanged -= OnNfcStatusChanged;
        //_nfcService.OnTagListeningStatusChanged -= OnTagListeningStatusChanged;

        _nfcService.Dispose();

        if (_isDeviceiOS)
            _nfcService.OniOSReadingSessionCancelled -= OniOSReadingSessionCancelled;
    }
    private async void OnNfcStatusChanged(object sender, bool isEnabled)
    {
        NfcIsEnabled = isEnabled;
        await DisplayAlert(ALERT_TITLE, $"NFC has been {(isEnabled ? "enabled" : "disabled")} from Guard Login Page.", "OK");
    }

    private void OniOSReadingSessionCancelled(object sender, EventArgs e) =>
       Debug.WriteLine("iOS NFC Session has been cancelled");



    #endregion "NFC Methods"



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