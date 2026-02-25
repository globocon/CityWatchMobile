using AutoMapper;
using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Services;
using C4iSytemsMobApp.Views;
using CommunityToolkit.Maui.Views;
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
    private readonly IScanDataDbServices _scanDataDbService;
    private readonly ICustomLogEntryServices _customLogEntryServices;
    private readonly IMapper _mapper;
    private bool _isNewGuard = false;
    private bool _isPopupOpen = false;

    public ObservableCollection<DropdownItem> ClientTypes { get; set; } = new();
    public ObservableCollection<DropdownItem> ClientSites { get; set; } = new();

    private DropdownItem _selectedClientType;
    private DropdownItem _selectedClientSite;
    private bool _suppressSuggestions = false;
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
    // Change to store objects with timestamp
    private List<LicenseEntry> _previousNumbers = new List<LicenseEntry>();
    private ObservableCollection<string> _filteredSuggestions = new ObservableCollection<string>();
    private const string LastNumberKey = "LastEnteredNumber";

    private void OnLicenseUnfocused(object sender, FocusEventArgs e)
    {
        SuggestionsView.IsVisible = false;
        SuggestionsFrame.IsVisible = false;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var savedLicenseNumber = Preferences.Get("SavedLicenseNumber", "");

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
        NavigationPage.SetHasNavigationBar(this, false);
        _crowdControlServices = crowdControlServices;
        _scannerControlServices = scannerControlServices;
        _nfcService = IPlatformApplication.Current.Services.GetService<INfcService>();
        _scanDataDbService = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
        _mapper = IPlatformApplication.Current.Services.GetService<IMapper>();
        _customLogEntryServices = IPlatformApplication.Current.Services.GetService<ICustomLogEntryServices>();

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
        const string PrefKey = "SavedLicenseNumbers";
        const int ExpirationDays = 45;

        var json = Preferences.Get(PrefKey, string.Empty);

        if (string.IsNullOrEmpty(json))
        {
            _previousNumbers = new List<LicenseEntry>();
        }
        else
        {
            try
            {
                // Try to deserialize as new format
                _previousNumbers = JsonSerializer.Deserialize<List<LicenseEntry>>(json);
            }
            catch
            {
                // Fallback: Migration from old List<string>
                try
                {
                    var oldList = JsonSerializer.Deserialize<List<string>>(json);
                    _previousNumbers = oldList.Select(n => new LicenseEntry { Number = n, LastUsed = DateTime.Now }).ToList();
                }
                catch
                {
                    _previousNumbers = new List<LicenseEntry>();
                }
            }
        }

        // Cleanup expired entries
        var cutoffDate = DateTime.Now.AddDays(-ExpirationDays);
        _previousNumbers.RemoveAll(x => x.LastUsed < cutoffDate);

        // If changes made (cleanup or migration), save back
        SaveNumbers();

        string lastNumber = Preferences.Get(LastNumberKey, string.Empty);
        if (!string.IsNullOrEmpty(lastNumber))
        {
            txtLicenseNumber.Text = lastNumber;
        }

        if (_previousNumbers != null && _previousNumbers.Any())
        {
            // Sort by most recently used
            _previousNumbers = _previousNumbers.OrderByDescending(x => x.LastUsed).ToList();

            // Load full list into suggestion view
            _filteredSuggestions.Clear();
            foreach (var item in _previousNumbers)
                _filteredSuggestions.Add(item.Number);

            // Show suggestion list on first load
            SuggestionsView.IsVisible = true;
            SuggestionsFrame.IsVisible = true;
        }
        else
        {
            // No saved numbers -> hide suggestions
            SuggestionsView.IsVisible = false;
            SuggestionsFrame.IsVisible = false;
        }
    }


    private void SaveNumbers()
    {
        var json = JsonSerializer.Serialize(_previousNumbers);
        const string PrefKey = "SavedLicenseNumbers";
        Preferences.Set(PrefKey, json);
    }

    private void SaveNewNumber(string newNumber)
    {
        if (string.IsNullOrWhiteSpace(newNumber))
            return;

        // Ensure list is loaded
        if (_previousNumbers == null) LoadSavedNumbers();

        // Remove if already exists to update position/timestamp
        _previousNumbers.RemoveAll(x => x.Number.Equals(newNumber, StringComparison.OrdinalIgnoreCase));

        // Insert at the beginning (Most Recent)
        _previousNumbers.Insert(0, new LicenseEntry { Number = newNumber, LastUsed = DateTime.Now });

        // Save updated list to Preferences
        SaveNumbers();
    }


    private void OnLicenseTextChanged(object sender, TextChangedEventArgs e)
    {
        string query = e.NewTextValue?.Trim() ?? "";

        var matches = string.IsNullOrEmpty(query)
            ? _previousNumbers // show all when empty
            : _previousNumbers.Where(x => x.Number.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        _filteredSuggestions.Clear();
        foreach (var item in matches)
            _filteredSuggestions.Add(item.Number);

        SuggestionsView.IsVisible = _filteredSuggestions.Any();
        SuggestionsFrame.IsVisible = _filteredSuggestions.Any();
    }


    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        //if (string.IsNullOrEmpty(txtLicenseNumber.Text))
        //{
        _filteredSuggestions.Clear();
        foreach (var item in _previousNumbers)
            _filteredSuggestions.Add(item.Number);

        SuggestionsView.IsVisible = _filteredSuggestions.Any();
        SuggestionsFrame.IsVisible = _filteredSuggestions.Any();
        //}
    }

    private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is string selectedNumber)
        {
            //_suppressSuggestions = true; 
            txtLicenseNumber.Text = selectedNumber;
            //_suppressSuggestions = false;
            _filteredSuggestions.Clear();
            foreach (var item in _previousNumbers)
                _filteredSuggestions.Add(item.Number);

            SuggestionsView.IsVisible = _filteredSuggestions.Any();
            SuggestionsFrame.IsVisible = _filteredSuggestions.Any();
            //SuggestionsView.IsVisible = false;
            //SuggestionsFrame.IsVisible = false;
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

    bool isLoggedIn = false;

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

            if (!isLoggedIn)
            {


                // Valid guard, proceed
                //LoadSavedNumbers();
                SaveNewNumber(licenseNumber);
                Preferences.Set(LastNumberKey, licenseNumber);
                SuggestionsView.IsVisible = false;
                SuggestionsFrame.IsVisible = false;
                Preferences.Set("GuardId", guardData.GuardId.ToString());
                Preferences.Set("GuardName", guardData.Name);
                Preferences.Set("LicenseNumber", licenseNumber);


                isLoggedIn = true;
                btnLogin.Text = "Go Back"; // Change button text
                // Disable entry and button
                txtLicenseNumber.IsEnabled = false;
                btnRegister.IsEnabled = false;
                btnRegister.IsVisible = false;
                //((Button)sender).IsEnabled = false;

                lblGuardName.Text = $"Hello {guardData.Name}.  Please verify that your details are correct, then click \"Access C4i System.\"";
                lblGuardName.TextColor = Colors.Green;
                lblGuardName.IsVisible = true;

                // Show UI controls

                hrStatusLayout.IsVisible = true;
                SetLedColor(ledHR1, guardData.HR1Status);
                SetLedColor(ledHR2, guardData.HR2Status);
                SetLedColor(ledHR3, guardData.HR3Status);

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

            else
            {
                // Handle "Back" or "Clear" action

                isLoggedIn = false;
                btnLogin.Text = "Login"; // Change text back
                lblGuardName.Text = string.Empty;
                lblGuardName.IsVisible = false;
                txtLicenseNumber.IsEnabled = true; // Re-enable entry
                txtLicenseNumber.Text = string.Empty; // Clear text
                SuggestionsFrame.IsVisible = true; // Hide suggestions
                hrStatusLayout.IsVisible = false;
                pickerClientType.IsVisible = false;
                pickerClientSite.IsVisible = false;
                btnEnterLogbook.IsVisible = false;
                btnRegister.IsEnabled = true;
                btnRegister.IsVisible = true;
                ToggleInstructionalTextVisibility();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Network error: " + ex.Message + ". Please ensure you are online and have an internet connection", "OK");
        }
    }

    private void SetLedColor(BoxView led, string status)
    {
        switch (status?.Trim().ToLower())
        {
            case "red":
                led.Color = Colors.Red;
                break;
            case "yellow":
                led.Color = Colors.Yellow;
                break;
            case "green":
                led.Color = Colors.Green;
                break;
            default:
                led.Color = Colors.Gray;
                break;
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
        btnEnterLogbook.BackgroundColor = Colors.Gray;
        btnEnterLogbook.IsEnabled = false;
        loadingIndicator.IsVisible = true;
        loadingIndicator.IsRunning = true;
        UpdateInfoLabel("");
        lblloadinginfo.IsVisible = true;
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
            //var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/EnterGuardLogin" +
            //             $"?guardId={guardId}" +
            //             $"&clientsiteId={clientSiteId}" +
            //             $"&userId={userId}" +
            //             $"&gps={Uri.EscapeDataString(gpsCoordinates)}";

            //string apiUrl = $"https://cws-ir.com/api/GuardSecurityNumber/EnterGuardLogin?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}";

            PostActivityRequest request = new PostActivityRequest()
            {
                guardId = guardId,
                clientsiteId = clientSiteId,
                userId = userId,
                activityString = "Logbook Logged In (Mob App)",
                gps = gpsCoordinates,
                systemEntry = true,
                scanningType = 0,
                tagUID = "NA",
                EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
                EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
                EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
                EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
                EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute(),
                IsNewGuard = _isNewGuard
            };

            lblloadinginfo.Text = "Authenticating...Please wait...";
            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/EnterGuardLogin";
            using (HttpClient client = new HttpClient())
            {
                // var response = await client.GetAsync(apiUrl);
                HttpResponseMessage response = await client.PostAsJsonAsync(apiUrl, request);
                if (response.IsSuccessStatusCode)
                {
                    UpdateInfoLabel("Processing Offline Data...Please wait...");
                    string contentData = await response.Content.ReadAsStringAsync();
                    var responseJson = JsonSerializer.Deserialize<JsonElement>(contentData);
                    int tourMode = responseJson.GetProperty("tourMode").GetInt32();
                    App.TourMode = (PatrolTouringMode)tourMode;

                    try
                    {

                        await _scanDataDbService.ClearPrePopulatedActivitesButtonList();
                        // Deserialize activity list
                        var activityElement = responseJson.GetProperty("activity");
                        List<ActivityModel> activity = JsonSerializer.Deserialize<List<ActivityModel>>(
                            activityElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (activity != null && activity.Count > 0)
                        {
                            UpdateInfoLabel($"Processing activity Offline Data. {activity.Count} records...Please wait...");
                            // Save to local DB
                            await _scanDataDbService.RefreshPrePopulatedActivitesButtonList(activity);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        await _scanDataDbService.ClearPatrolCarCacheList();
                        // Deserialize pcarlogs list
                        var patrolCarLogElement = responseJson.GetProperty("patrolCarLog");
                        List<PatrolCarLog> pcarlogs = JsonSerializer.Deserialize<List<PatrolCarLog>>(
                            patrolCarLogElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (pcarlogs != null && pcarlogs.Count > 0)
                        {
                            var cacheEntity = _mapper.Map<List<PatrolCarLogCache>>(pcarlogs);
                            // Save to local DB
                            UpdateInfoLabel($"Processing PatrolCar Offline Data. {pcarlogs.Count} records...Please wait...");
                            await _scanDataDbService.RefreshPatrolCarCacheList(cacheEntity);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {

                        await _scanDataDbService.ClearCustomFieldLogCacheList();
                        // Deserialize CustomField list
                        var customFieldLogElement = responseJson.GetProperty("customFieldLog");
                        List<Dictionary<string, string?>> customFieldLogs = JsonSerializer.Deserialize<List<Dictionary<string, string?>>>(
                            customFieldLogElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (customFieldLogs != null && customFieldLogs.Count > 0)
                        {
                            UpdateInfoLabel($"Processing CustomField Offline Data. {customFieldLogs.Count} records...Please wait...");
                            await _customLogEntryServices.ProcessCustomFieldLogsOnlineDataToCache(customFieldLogs);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        await _scanDataDbService.ClearRCLinkedDuressClientSitesList();
                        // Deserialize RC Linked Duress ClientSites list
                        var rcLinkedDuressClientSitesElement = responseJson.GetProperty("rcLinkedClientSites");
                        List<RCLinkedDuressClientSitesCache> rcLinkedDuressClientSites = JsonSerializer.Deserialize<List<RCLinkedDuressClientSitesCache>>(
                            rcLinkedDuressClientSitesElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (rcLinkedDuressClientSites != null && rcLinkedDuressClientSites.Count > 0)
                        {
                            //var cacheEntity = _mapper.Map<List<RCLinkedDuressClientSitesCache>>(rcLinkedDuressClientSitesElement);
                            // Save to local DB
                            UpdateInfoLabel($"Processing Linked Client Sites Offline Data. {rcLinkedDuressClientSites.Count} records...Please wait...");
                            await _scanDataDbService.RefreshRCLinkedDuressClientSitesList(rcLinkedDuressClientSites);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        await _scanDataDbService.ClearIrClientSitesTypesLocalList();
                        // Deserialize irClientTypes list
                        var irClientTypesElement = responseJson.GetProperty("irClientTypes");
                        List<DropdownItem> irClientTypes = JsonSerializer.Deserialize<List<DropdownItem>>(
                            irClientTypesElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (irClientTypes != null && irClientTypes.Count > 0)
                        {
                            var cacheEntity = _mapper.Map<List<ClientSiteTypeLocal>>(irClientTypes);
                            // Save to local DB
                            UpdateInfoLabel($"Processing IR Client Types Offline Data. {irClientTypes.Count} records...Please wait...");
                            await _scanDataDbService.RefreshIrClientSitesTypesLocalList(cacheEntity);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        await _scanDataDbService.ClearIrClientSitesLocalList();
                        // Deserialize irClientSites list
                        var irClientSitesElement = responseJson.GetProperty("irClientSites");
                        List<WebIncidentReport.ClientSite> irClientSites = JsonSerializer.Deserialize<List<WebIncidentReport.ClientSite>>(
                            irClientSitesElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (irClientSites != null && irClientSites.Count > 0)
                        {
                            var cacheEntity = _mapper.Map<List<ClientSitesLocal>>(irClientSites);
                            // Save to local DB
                            UpdateInfoLabel($"Processing IR Client Sites Offline Data. {irClientSites.Count} records...Please wait...");
                            await _scanDataDbService.RefreshIrClientSitesLocalList(cacheEntity);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        await _scanDataDbService.ClearIrFeedbackTemplateLocalList();
                        // Deserialize irFeedbackTemplates list
                        var irFeedbackTemplatesElement = responseJson.GetProperty("irFeedbackTemplates");
                        List<WebIncidentReport.FeedbackTemplateViewModel> irFeedbackTemplates = JsonSerializer.Deserialize<List<WebIncidentReport.FeedbackTemplateViewModel>>(
                            irFeedbackTemplatesElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (irFeedbackTemplates != null && irFeedbackTemplates.Count > 0)
                        {
                            var cacheEntity = _mapper.Map<List<IrFeedbackTemplateViewModelLocal>>(irFeedbackTemplates);
                            // Save to local DB
                            UpdateInfoLabel($"Processing IR Feedback Templates Offline Data. {irFeedbackTemplates.Count} records...Please wait...");
                            await _scanDataDbService.RefreshIrFeedbackTemplateLocalList(cacheEntity);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        await _scanDataDbService.ClearIrNotifiedByLocalList();
                        // Deserialize irNotifiedBy list
                        var irNotifiedByElement = responseJson.GetProperty("irNotifiedByList");
                        List<string> irNotifiedBy = JsonSerializer.Deserialize<List<string>>(
                            irNotifiedByElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (irNotifiedBy != null && irNotifiedBy.Count > 0)
                        {

                            List<IrNotifiedByLocal> cacheEntity = irNotifiedBy
                                .Select(x => new IrNotifiedByLocal
                                {
                                    NotifiedBy = x
                                }).ToList();

                            // Save to local DB
                            UpdateInfoLabel($"Processing IR Notified By Offline Data. {irNotifiedBy.Count} records...Please wait...");
                            await _scanDataDbService.RefreshIrNotifiedByLocalList(cacheEntity);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        await _scanDataDbService.ClearIrAreasLocalList();
                        // Deserialize irAreas list
                        var irAreasElement = responseJson.GetProperty("irAreas");
                        List<WebIncidentReport.AreaItem> irAreas = JsonSerializer.Deserialize<List<WebIncidentReport.AreaItem>>(
                            irAreasElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (irAreas != null && irAreas.Count > 0)
                        {
                            var cacheEntity = _mapper.Map<List<ClientSiteAreaLocal>>(irAreas);
                            // Save to local DB
                            UpdateInfoLabel($"Processing IR Areas Offline Data. {irAreas.Count} records...Please wait...");
                            await _scanDataDbService.RefreshIrAreasLocalList(cacheEntity);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        // Deserialize Audio list
                        var Mp3FileElement = responseJson.GetProperty("audioList");
                        List<Audio.Mp3File> mp3files = JsonSerializer.Deserialize<List<Audio.Mp3File>>(
                            Mp3FileElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (mp3files != null && mp3files.Count > 0)
                        {
                            var cacheEntity = _mapper.Map<List<AudioAndMultimediaLocal>>(mp3files);
                            UpdateInfoLabel($"Processing Multimedia Audio Offline Data. {mp3files.Count} records...Please wait...");
                            await CheckAndSyncMultimediaFiles(cacheEntity);
                        }
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        // Deserialize multimediaList list
                        var videoElement = responseJson.GetProperty("multimediaList");
                        List<VideoFile> videos = JsonSerializer.Deserialize<List<VideoFile>>(
                            videoElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (videos != null && videos.Count > 0)
                        {
                            var cacheEntity = _mapper.Map<List<AudioAndMultimediaLocal>>(videos);
                            UpdateInfoLabel($"Processing Multimedia Video Offline Data. {videos.Count} records...Please wait...");
                            await CheckAndSyncMultimediaFiles(cacheEntity);
                        }
                    }
                    catch (Exception)
                    {

                    }

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
                    Preferences.Set("NfcOnboarded", "false");
                    Preferences.Set("iBeaconOnboarded", "false");
                    UpdateInfoLabel("Checking Smart Wand Tags Settings.");
                    if (App.TourMode != PatrolTouringMode.STND)
                    {
                        Preferences.Set("iBeaconOnboarded", "true");

                        Preferences.Set("NfcOnboarded", "true");
                        //check if nfc is available in the device
                        //check if nfc is enabled
                        if (_nfcService.IsSupported && _nfcService.IsAvailable && !NfcIsEnabled)
                        {
                            await DisplayAlert(ALERT_TITLE, "NFC is disabled. Please enable NFC to proceed.", "OK");
                            return;
                        }

                        if (_nfcService.IsSupported && _nfcService.IsAvailable && NfcIsEnabled) { UnsubscribeFromNFCEvents(); }

                        //Get all the smartwand tags asscosiated with the site
                        UpdateInfoLabel("Reading Smart Wand Tags for site.");
                        var swtags = await _scannerControlServices.GetSmartWandTagsForSite(clientSiteIdString);
                        if (swtags != null && swtags.Count > 0)
                        {
                            UpdateInfoLabel($"Processing Smart Wand Tags Offline Data. {swtags.Count} records...Please wait...");
                            await _scanDataDbService.RefreshSmartWandTagsList(swtags);
                        }
                    }
                    else
                    {
                        var scannerSettings = await _scannerControlServices.CheckScannerOnboardedAsync(clientSiteIdString);
                        if (scannerSettings != null && scannerSettings.Count > 0)
                        {
                            if (scannerSettings.Exists(x => x.ToLower().Equals("bluetooth")))
                            {
                                Preferences.Set("iBeaconOnboarded", "true");
                            }

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



                            //Get all the smartwand tags asscosiated with the site
                            UpdateInfoLabel("Reading Smart Wand Tags for site.");
                            var swtags = await _scannerControlServices.GetSmartWandTagsForSite(clientSiteIdString);
                            if (swtags != null && swtags.Count > 0)
                            {
                                UpdateInfoLabel($"Processing Smart Wand Tags Offline Data. {swtags.Count} records...Please wait...");
                                await _scanDataDbService.RefreshSmartWandTagsList(swtags);
                            }

                        }
                    }



                    Preferences.Set("CrowdCountEnabledForSite", "false");
                    UpdateInfoLabel("Checking Crowd Count Control Settings.");
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
        finally
        {
            btnEnterLogbook.BackgroundColor = Colors.Green;
            btnEnterLogbook.IsEnabled = true;
            loadingIndicator.IsVisible = false;
            loadingIndicator.IsRunning = false;
            lblloadinginfo.IsVisible = false;
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

    private async void OnRegisterNewGuardClicked(object sender, EventArgs e)
    {
        _isPopupOpen = true;

        var popup = new RegisterNewGuardPopup();
        var result = await this.ShowPopupAsync(popup);

        if (result is string action)
        {
            if (!string.IsNullOrEmpty(action))
            {
                switch (action)
                {
                    case "Cancel":
                        // Just close silently
                        break;
                    default:
                        // Handle all other actions here
                        txtLicenseNumber.Text = action;
                        _isNewGuard = true;
                        OnReadClicked(null, null);
                        break;
                }
            }
        }

        _isPopupOpen = false;
    }


    private async Task CheckAndSyncMultimediaFiles(List<AudioAndMultimediaLocal> _videos)
    {
        if (_videos == null || _videos.Count == 0) return;

        string _multimediaLocalFolder = "";
        int audioType = _videos.FirstOrDefault()?.AudioType ?? 0;
        //List<AudioAndMultimediaLocal> _FilesToDelete = new List<AudioAndMultimediaLocal>();

        if (audioType == 1)
        {
            _multimediaLocalFolder = Path.Combine(FileSystem.AppDataDirectory, "IrFolder", "Downloads", "AudioFiles");
        }
        else if (audioType == 3)
        {
            _multimediaLocalFolder = Path.Combine(FileSystem.AppDataDirectory, "IrFolder", "Downloads", "VideoFiles");
        }

        if (!Directory.Exists(_multimediaLocalFolder))
            Directory.CreateDirectory(_multimediaLocalFolder);

        var _existingFiles = await _scanDataDbService.GetMultimediaLocalList(audioType);

        foreach (var item in _videos)
        {
            UpdateInfoLabel($"Checking Offline file {item.Label}...Please wait...");
            var _isfileExisting = _existingFiles.Where(x => x.ServerUrl == item.ServerUrl).FirstOrDefault();
            if (_isfileExisting != null)
            {
                // File already exists, set local path and skip download
                item.LocalFilePath = _isfileExisting.LocalFilePath;
                item.Id = _isfileExisting.Id;

                if (_isfileExisting.LocalFilePath != "" && File.Exists(_isfileExisting.LocalFilePath))
                    continue;
                else
                {
                    // Local file is missing, need to re-download
                    UpdateInfoLabel($"Downloading Multimedia Offline file {item.Label}...Please wait...");
                    var _newFileName = await DownloadMultimediaFileFromServer(item.ServerUrl, _multimediaLocalFolder);
                    if (_newFileName != "")
                        item.LocalFilePath = _newFileName;
                    else
                        item.LocalFilePath = "";
                }
            }
            else
            {
                item.Id = 0;
                // File does not exist, need to download
                UpdateInfoLabel($"Downloading Multimedia Offline file {item.Label}...Please wait...");
                var _newFileName = await DownloadMultimediaFileFromServer(item.ServerUrl, _multimediaLocalFolder);
                if (_newFileName != "")
                    item.LocalFilePath = _newFileName;
            }
        }

        foreach (var _fl in _existingFiles)
        {
            // Remove any deleted file in server
            var _found = _videos.Any(x => x.ServerUrl == _fl.ServerUrl);
            if (!_found)
            {
                if (File.Exists(_fl.LocalFilePath))
                {
                    try
                    {
                        File.Delete(_fl.LocalFilePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting multimedia file {_fl.LocalFilePath}. Error:{ex.ToString()}");
                    }
                }
            }
        }

        UpdateInfoLabel($"Refreshing Multimedia Offline files list...Please wait...");
        await _scanDataDbService.RefreshAudioAndMultimediaLocalList(_videos);



    }

    //private async Task<string> DownloadMultimediaFileFromServer(string _serverUrl, string _localPath)
    //{
    //    string fileName = CommonHelper.GetSanitizedFileNameFromUrl(_serverUrl);
    //    string localfileNameWithPath = Path.Combine(_localPath, fileName);
    //    try
    //    {
    //        using (HttpClient client = new HttpClient())
    //        {
    //            client.Timeout = TimeSpan.FromMinutes(10);
    //            var response = await client.GetAsync(_serverUrl);
    //            if (response.IsSuccessStatusCode)
    //            {
    //                var content = await response.Content.ReadAsByteArrayAsync();
    //                File.WriteAllBytes(localfileNameWithPath, content);
    //            }
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Debug.WriteLine($"Error downloading file {_serverUrl}: {ex.Message}");
    //        return "";
    //    }

    //    return localfileNameWithPath;
    //}

    private async Task<string> DownloadMultimediaFileFromServer(string serverUrl, string localPath)
    {
        string fileName = CommonHelper.GetSanitizedFileNameFromUrl(serverUrl);
        string localFileNameWithPath = Path.Combine(localPath, fileName);

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(20)
            };

            using var response = await client.GetAsync(serverUrl, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(
                localFileNameWithPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await stream.CopyToAsync(fileStream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading file {serverUrl}: {ex}");
            return "";
        }

        return localFileNameWithPath;
    }


    private void UpdateInfoLabel(string msg)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            lblloadinginfo.Text = msg;
        });

    }
}

// Model class for API response
public class GuardResponse
{
    public string Name { get; set; }
    public int GuardId { get; set; }
    public string SecurityNo { get; set; }
    public bool IsActive { get; set; }

    public string HR1Status { get; set; }
    public string HR2Status { get; set; }
    public string HR3Status { get; set; }
    public bool GuardLockStatusBasedOnRedDoc { get; set; }
}
public class DropdownItem
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class LicenseEntry
{
    public string Number { get; set; }
    public DateTime LastUsed { get; set; }
}