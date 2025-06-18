using C4iSytemsMobApp.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp
{
    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly System.Timers.Timer duressCheckTimer = new System.Timers.Timer(3000); // Check every 3 seconds
        private readonly IVolumeButtonService _volumeButtonService;
        private int _pcounter = 0;
        private int _CurrentCounter = 0;
        private int _totalpatrons = 0;
        private bool _CcounterShown = false;
        private bool _TcounterShown = true;
        private bool _IsCrowdControlCounterEnabled = false;
        private HubConnection _hubConnection;
        bool isDrawerOpen = false;
        public event PropertyChangedEventHandler PropertyChanged;
        private int? _clientSiteId;
        private int? _userId;
        private int? _guardId;
        private bool _guardCounterReset = false;
        private int selectedIncrement = 1;
        private int selectedDecrement = 1;
        private bool _IsVolumeControlButtonEnabled = false;
        private List<DropdownItemsControl> _crowdControllocationList;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool ShowCounters
        {
            get => _IsCrowdControlCounterEnabled;
            set
            {
                if (_IsCrowdControlCounterEnabled != value)
                {
                    _IsCrowdControlCounterEnabled = value;
                    OnPropertyChanged(nameof(ShowCounters));
                }
                if (_IsCrowdControlCounterEnabled) { CounterRow.Height = GridLength.Auto; } else { CounterRow.Height = new GridLength(0); }
            }
        }
        private bool _shouldOpenDrawerOnReturn = false;


        public MainPage(IVolumeButtonService volumeButtonService, bool? showDrawerOnStart = null)
        {
            InitializeComponent();
            _volumeButtonService = volumeButtonService;
            BindingContext = this;
            NavigationPage.SetHasNavigationBar(this, false);
            LoadLoggedInUser();
            LoadSecureData();
            // Start checking duress status on app load
            duressCheckTimer.Elapsed += async (s, e) => await CheckDuressStatus();
            duressCheckTimer.AutoReset = true;
            duressCheckTimer.Start();
            _shouldOpenDrawerOnReturn = showDrawerOnStart ?? false; // Defaults to false if null
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();



            // If InitializePatronsCounterDisplay is async, await it.
            await InitializePatronsCounterDisplay();
            if (_IsCrowdControlCounterEnabled)
            {
                if (_clientSiteId == null) return;
                string hubUrl = $"{AppConfig.MobileSignalRBaseUrl}/MobileAppSignalRHub";
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                //await Task.Run(() => {
                //    Dispatcher.Dispatch(async () => await _hubConnection.StartAsync());                   
                // });

                _hubConnection.On<ClientSiteMobileCrowdControl>("UpdateCrowdControl", (csmcc) =>
                {
                    _CurrentCounter = csmcc.Ccount;
                    _totalpatrons = csmcc.Tcount;
                    _pcounter = csmcc.ClientSiteCrowdControlGuards?.FirstOrDefault(x => x.GuardId == (int)_guardId && x.UserId == (int)_userId)?.Pcount ?? 0;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        RefreshCounterDisplay();
                    });
                });

                _hubConnection.On<ClientSiteMobileCrowdControl>("ResetSiteCrowdControlCount", (csmcc) =>
                {
                    _CurrentCounter = csmcc.Ccount;
                    _totalpatrons = csmcc.Tcount;
                    _pcounter = csmcc.ClientSiteCrowdControlGuards?.FirstOrDefault(x => x.GuardId == (int)_guardId && x.UserId == (int)_userId)?.Pcount ?? 0;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        RefreshCounterDisplay();
                        DisplayAlert("Success", "Site counter has been reset.", "Ok");
                    });
                });

                _hubConnection.On<ClientSiteMobileCrowdControl>("ResetGuardCrowdControlCount", (csmcc) =>
                {
                    _CurrentCounter = csmcc.Ccount;
                    _totalpatrons = csmcc.Tcount;
                    _pcounter = csmcc.ClientSiteCrowdControlGuards?.FirstOrDefault(x => x.GuardId == (int)_guardId && x.UserId == (int)_userId)?.Pcount ?? 0;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        RefreshCounterDisplay();
                        if (_guardCounterReset)
                        {
                            _guardCounterReset = false;
                            DisplayAlert("Success", "Location counter has been reset.", "Ok");
                        }

                    });
                });

                _hubConnection.Closed += async (error) =>
                {
                    Debug.WriteLine($"Connection closed. Reason: {error?.Message}");
                    // Optionally attempt reconnect
                    await Task.Delay(3000);
                    await _hubConnection.StartAsync();
                };

                _hubConnection.Reconnected += connectionId =>
                {
                    Debug.WriteLine($"Reconnected with connectionId: {connectionId}");
                    if (_hubConnection.State == HubConnectionState.Connected)
                    {
                        MobileCrowdControlGuard JoinGaurd = new MobileCrowdControlGuard()
                        {
                            ClientSiteId = (int)_clientSiteId,
                            GuardId = (int)_guardId,
                            UserId = (int)_userId
                        };
                        var r = Task.FromResult(_hubConnection.InvokeAsync<ClientSiteMobileCrowdControl>("JoinGroup", JoinGaurd)).Result;
                        if (r != null)
                        {
                            _CurrentCounter = r.Result.Ccount;
                            _totalpatrons = r.Result.Tcount;
                            _pcounter = r.Result.ClientSiteCrowdControlGuards?.FirstOrDefault()?.Pcount ?? 0;
                            RefreshCounterDisplay();
                        }
                    }
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnecting += error =>
                {
                    Debug.WriteLine($"Reconnecting due to: {error?.Message}");
                    return Task.CompletedTask;
                };



                await _hubConnection.StartAsync();

                if (_hubConnection.State == HubConnectionState.Connected)
                {
                    MobileCrowdControlGuard JoinGaurd = new MobileCrowdControlGuard()
                    {
                        ClientSiteId = (int)_clientSiteId,
                        GuardId = (int)_guardId,
                        UserId = (int)_userId
                    };
                    var r = await _hubConnection.InvokeAsync<ClientSiteMobileCrowdControl>("JoinGroup", JoinGaurd);
                    if (r != null)
                    {
                        _CurrentCounter = r.Ccount;
                        _totalpatrons = r.Tcount;
                        _pcounter = r.ClientSiteCrowdControlGuards?.FirstOrDefault()?.Pcount ?? 0;
                        RefreshCounterDisplay();
                    }
                }


                if (DeviceInfo.Platform == DevicePlatform.Android)
                {                    
                    if (_volumeButtonService != null)
                    {
                        _volumeButtonService.VolumeUpPressed += (s, e) =>
                        {
                            if (_IsVolumeControlButtonEnabled)
                                OnIncrementClicked(s, e);
                        };

                        _volumeButtonService.VolumeDownPressed += (s, e) =>
                        {
                            if (_IsVolumeControlButtonEnabled)
                                OnDecrementClicked(s, e);
                        };
                    }
                }

                if (DeviceInfo.Platform == DevicePlatform.iOS)
                {                    
                    if (_volumeButtonService != null)
                    {
                        _volumeButtonService.VolumeUpPressed += (s, e) =>
                        {
                            if (_IsVolumeControlButtonEnabled)
                                OnIncrementClicked(s, e);
                        };

                        _volumeButtonService.VolumeDownPressed += (s, e) =>
                        {
                            if (_IsVolumeControlButtonEnabled)
                                OnDecrementClicked(s, e);
                        };
                    }
                }

            }


            if (_shouldOpenDrawerOnReturn)
            {
                OpenDrawer();

            }
        }

        private async void LoadLoggedInUser()
        {
            try
            {
                string userName = await SecureStorage.GetAsync("UserName");
                if (!string.IsNullOrEmpty(userName))
                {
                    //lblLoggedInUser.Text = $"Welcome, {userName}";
                }
                else
                {
                    //lblLoggedInUser.Text = "Welcome, Guest";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading user: {ex.Message}");
            }
        }

        private async void LoadSecureData()
        {
            lblClientSite.Text = $"Client Site: {await SecureStorage.GetAsync("ClientSite") ?? "N/A"}";
            //lblClientType.Text = $"Client Type: {await SecureStorage.GetAsync("ClientType") ?? "N/A"}";
            //lblGuardName.Text = $"Guard Name: {await SecureStorage.GetAsync("GuardName") ?? "N/A"}";           
        }

        private async void OnManualPositionClicked(object sender, EventArgs e)
        {
            Application.Current.MainPage = new NavigationPage(new WebIncidentReport());
        }

        private async void OnLogActivityClicked(object sender, EventArgs e)
        {
            Application.Current.MainPage = new NavigationPage(new LogActivity()); // Redirect to LogActivityPage
        }

        private async void OnAudioClicked(object sender, EventArgs e)
        {
            Application.Current.MainPage = new NavigationPage(new Audio());

        }

        private async void OnMultimediaClicked(object sender, EventArgs e)
        {

            Application.Current.MainPage = new NavigationPage(new MultiMedia());

        }

        private CancellationTokenSource _countdownCts;

        private async void OnDuressClicked(object sender, EventArgs e)
        {
            if (DuressStatusLabel.Text.Contains("Active"))
            {

                return;
            }


            DuressPopup.IsVisible = true;
            _countdownCts = new CancellationTokenSource();

            try
            {
                for (int i = 5; i > 0; i--)
                {
                    CountdownLabel.Text = $"Duress will activate in {i} seconds.";
                    await Task.Delay(1000, _countdownCts.Token);
                }

                // Automatically activate duress if not cancelled
                await ActivateDuress();
            }
            catch (TaskCanceledException)
            {
                CountdownLabel.Text = "Duress activation cancelled.";
                await Task.Delay(1000); // Short delay to show cancellation message
            }
            finally
            {
                DuressPopup.IsVisible = false;
            }
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            _countdownCts?.Cancel();
        }

        private async Task ActivateDuress()
        {



            string gpsCoordinates = await SecureStorage.GetAsync("GpsCoordinates");


            // Validate Guard ID
            var guardId = await TryGetSecureId("GuardId", "Guard ID not found. Please validate the License Number first.");
            if (guardId == null) return;

            // Validate Client Site ID
            var clientSiteId = await TryGetSecureId("SelectedClientSiteId", "Please select a valid Client Site.");
            if (clientSiteId == null) return;

            // Validate User ID
            var userId = await TryGetSecureId("UserId", "User ID is invalid. Please log in again.");
            if (userId == null) return;

            if (string.IsNullOrWhiteSpace(gpsCoordinates))
            {
                await DisplayAlert("Location Error", "GPS coordinates not available. Please ensure location services are enabled.", "OK");
                return;
            }



            string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SaveClientSiteDuress?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}&gps={Uri.EscapeDataString(gpsCoordinates)}";

            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    UpdateDuressUI();
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    //await DisplayAlert("Error", $"Duress submission failed: {errorMessage}", "OK");
                }
            }
        }

        private void UpdateDuressUI()
        {
            DuressButtonFrame.BackgroundColor = Colors.Red;
            DuressStatusLabel.Text = "Status: Active";
            DuressStatusLabel.TextColor = Colors.White;
        }

        private async Task<int?> TryGetSecureId(string key, string errorMessage)
        {
            string idString = await SecureStorage.GetAsync(key);

            if (string.IsNullOrWhiteSpace(idString) || !int.TryParse(idString, out int id) || id <= 0)
            {
                await DisplayAlert("Validation Error", errorMessage, "OK");
                return null;
            }

            return id;
        }

        private async Task CheckDuressStatus()
        {
            try
            {


                string clientSiteIdString = await SecureStorage.GetAsync("SelectedClientSiteId");
                if (string.IsNullOrWhiteSpace(clientSiteIdString) || !int.TryParse(clientSiteIdString, out int clientSiteId) || clientSiteId <= 0)
                {
                    return; // No Client Site, stop checking
                }

                string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetDuressStatus?clientsiteId={clientSiteId}";

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponse);

                        if (result != null && result.TryGetValue("status", out string status))
                        {
                            //Update UI on the main thread
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                if (DuressButtonFrame != null && DuressStatusLabel != null)
                                {
                                    if (status == "Active")
                                    {
                                        DuressButtonFrame.BackgroundColor = Colors.Red;
                                        DuressStatusLabel.Text = "Status: Active";
                                        DuressStatusLabel.TextColor = Colors.White;
                                    }
                                    else
                                    {
                                        DuressButtonFrame.BackgroundColor = Color.FromArgb("#FFC107");
                                        DuressStatusLabel.Text = "Status: Normal";
                                        DuressStatusLabel.TextColor = Colors.Black;
                                    }
                                }
                            });
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking duress status: {ex.Message}");
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

        protected override bool OnBackButtonPressed()
        {
            bool isConfirmed = false;

            Device.BeginInvokeOnMainThread(async () =>
            {
                isConfirmed = await DisplayAlert("Exit", "Do you want to log out and close the app?", "Yes", "No");

                if (isConfirmed)
                {
                    try
                    {
                        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                        {
                            _hubConnection.StopAsync();
                            _hubConnection.DisposeAsync();
                        }
                    }
                    catch (Exception)
                    {
                        //  throw;
                    }
                    SecureStorage.RemoveAll(); // Clear SecureStorage (logout)
                    System.Diagnostics.Process.GetCurrentProcess().Kill(); // Close the app
                }
            });

            return true; // Prevent default back button behavior
        }
        private async void OnMenuClicked(object sender, EventArgs e)
        {
            if (!isDrawerOpen)
            {
                // Show overlay
                DrawerOverlay.IsVisible = true;

                // Slide drawer in
                await DrawerMenu.TranslateTo(0, 0, 250, Easing.SinIn);
                isDrawerOpen = true;
            }
            else
            {
                await CloseDrawer();
            }
        }

        private async void OnDrawerOverlayTapped(object sender, EventArgs e)
        {
            await CloseDrawer();
        }

        private async Task CloseDrawer()
        {
            // Slide drawer out
            await DrawerMenu.TranslateTo(-DrawerMenu.Width, 0, 250, Easing.SinOut);

            // Hide overlay
            DrawerOverlay.IsVisible = false;

            isDrawerOpen = false;
        }

        private void OnIncrementClicked(object sender, EventArgs e)
        {
            ClientSiteMobileCrowdControlData CountData = new ClientSiteMobileCrowdControlData()
            {
                ClientSiteId = (int)_clientSiteId,
                AddCount = true,
                Count = selectedIncrement,
                ClientSiteCrowdControlGuards = new List<ClientSiteMobileCrowdControlGuards>()
                {
                    new ClientSiteMobileCrowdControlGuards()
                    {
                        ClientSiteId = (int)_clientSiteId,
                                    GuardId = (int)_guardId,
                                    UserId = (int)_userId,
                                    Pcount = selectedIncrement
                    }
                }
            };
            _hubConnection.InvokeAsync("UpdateCCCToMobileSiteGroup", CountData);
        }

        private void OnDecrementClicked(object sender, EventArgs e)
        {
            ClientSiteMobileCrowdControlData CountData = new ClientSiteMobileCrowdControlData()
            {
                ClientSiteId = (int)_clientSiteId,
                AddCount = false,
                Count = selectedDecrement,
                ClientSiteCrowdControlGuards = new List<ClientSiteMobileCrowdControlGuards>()
                {
                    new ClientSiteMobileCrowdControlGuards()
                    {
                        ClientSiteId = (int)_clientSiteId,
                                    GuardId = (int)_guardId,
                                    UserId = (int)_userId,
                                    Pcount = selectedDecrement
                    }
                }
            };
            _hubConnection.InvokeAsync("UpdateCCCToMobileSiteGroup", CountData);
        }

        private async Task InitializePatronsCounterDisplay()
        {
            // Validate Client Site ID
            _clientSiteId = await TryGetSecureId("SelectedClientSiteId", "Please select a valid Client Site.");
            if (_clientSiteId == null) return;
            _guardId = await TryGetSecureId("GuardId", "Guard ID not found. Please validate the License Number first.");
            if (_guardId == null) return;

            // Validate User ID
            _userId = await TryGetSecureId("UserId", "User ID is invalid. Please log in again.");
            if (_userId == null) return;

            string apiUrl = $"{AppConfig.ApiBaseUrl}CrowdCount/GetCrowdCountControlSettings?siteId={_clientSiteId}";

            //var response = await _httpClient.GetFromJsonAsync<ClientSiteMobileAppSettings>(apiUrl);

            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<ClientSiteMobileAppSettings>();
                    if (settings != null)
                    {
                        _IsCrowdControlCounterEnabled = settings.IsCrowdCountEnabled;
                        ShowCounters = _IsCrowdControlCounterEnabled;
                        OnPropertyChanged(nameof(ShowCounters));


                        _crowdControllocationList = new List<DropdownItemsControl>();
                        int j = 1;
                        if (settings.IsDoorEnabled)
                        {
                            for (int i = 1; i <= settings.CounterQuantity; i++)
                            {
                                _crowdControllocationList.Add(new DropdownItemsControl() { Id = j, Name = $"Door {i:00}" });
                                j++;
                            }
                        }
                        if (settings.IsGateEnabled)
                        {
                            for (int i = 1; i <= settings.CounterQuantity; i++)
                            {
                                _crowdControllocationList.Add(new DropdownItemsControl() { Id = j, Name = $"Gate {i:00}" });
                                j++;
                            }
                        }
                        if (settings.IsRoomEnabled)
                        {
                            for (int i = 1; i <= settings.CounterQuantity; i++)
                            {
                                _crowdControllocationList.Add(new DropdownItemsControl() { Id = j, Name = $"Room {i:00}" });
                                j++;
                            }
                        }
                        if (settings.IsLevelFloorEnabled)
                        {
                            for (int i = 1; i <= settings.CounterQuantity; i++)
                            {
                                _crowdControllocationList.Add(new DropdownItemsControl() { Id = j, Name = $"Level(Floor) {i:00}" });
                                j++;
                            }
                        }

                        CrowdControlLocationPicker.ItemsSource = _crowdControllocationList;
                        CrowdControlLocationPicker.SelectedIndex = 0;

                        // Disable picker if only one item
                        CrowdControlLocationPicker.IsEnabled = _crowdControllocationList.Count > 1;
                        if (_crowdControllocationList.Count == 1)
                        {
                            var selectedLocation = CrowdControlLocationPicker.SelectedItem as DropdownItemsControl;
                            if (selectedLocation != null)
                            {
                                int locationId = selectedLocation.Id;
                                string locationName = selectedLocation.Name;
                                await SecureStorage.SetAsync("CrowdControlSelectedLocation", selectedLocation.Name);

                                // Validate Client Site ID            
                                if (_clientSiteId == null || _guardId == null || _userId == null)
                                    return;

                                MobileCrowdControlGuard mg = new MobileCrowdControlGuard()
                                {
                                    ClientSiteId = (int)_clientSiteId,
                                    GuardId = (int)_guardId,
                                    UserId = (int)_userId,
                                    Location = locationName
                                };

                                UpdateGuardCrowdControlLocation(mg);
                            }
                        }

                        LoadPickerValues();

                    }
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    // Handle error (log or show message)
                }
            }

            RefreshCounterDisplay();
        }

        private void ToggleCounterDisplay(object sender, EventArgs e)
        {
            if (_CcounterShown && _IsCrowdControlCounterEnabled)
            {
                _CcounterShown = false;
                _TcounterShown = true;
                RefreshCounterDisplay();
            }
            else if (_TcounterShown && _IsCrowdControlCounterEnabled)
            {
                _CcounterShown = true;
                _TcounterShown = false;
                RefreshCounterDisplay();
            }
        }
        private void RefreshCounterDisplay()
        {
            if (_CcounterShown && _IsCrowdControlCounterEnabled)
            {
                total_current_patronsLabel.Text = $"C{_CurrentCounter.ToString("000000")}";
            }
            else if (_TcounterShown && _IsCrowdControlCounterEnabled)
            {
                total_current_patronsLabel.Text = $"T{_totalpatrons.ToString("000000")}";
            }
            CounterLabel.Text = _pcounter.ToString("0000");
        }
        private async void OnCounterSettingsClicked(object sender, EventArgs e)
        {
            CrowdControlLocationPicker.IsEnabled = _crowdControllocationList.Count > 1;
            string _CrowdControlSelectedLocation = await SecureStorage.GetAsync("CrowdControlSelectedLocation") ?? "";
            int selectedIndex = 0;
            // Find the index in the list where the Name matches
            if (!string.IsNullOrEmpty(_CrowdControlSelectedLocation))
            {
                selectedIndex = _crowdControllocationList.FindIndex(loc => loc.Name == _CrowdControlSelectedLocation);
            }
            // Set picker index (fallback to 0 if not found)
            CrowdControlLocationPicker.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;

            IncrementValuePicker.SelectedItem = selectedIncrement;
            DecrementValuePicker.SelectedItem = selectedDecrement;

            CrowdControlSettingsPopup.IsVisible = true;
        }
        private async void OnCounterSettingsCloseClicked(object sender, EventArgs e)
        {
            CrowdControlSettingsPopup.IsVisible = false;
        }

        private async void OnResetLocationClicked(object sender, EventArgs e)
        {
            MobileCrowdControlGuard ResetData = new MobileCrowdControlGuard()
            {
                ClientSiteId = (int)_clientSiteId,
                GuardId = (int)_guardId,
                UserId = (int)_userId
            };
            _guardCounterReset = true;
            _hubConnection.InvokeAsync("ResetGuardCrowdControlCount", ResetData);
            CrowdControlSettingsPopup.IsVisible = false;
        }

        private async void OnResetSiteClicked(object sender, EventArgs e)
        {
            bool isConfirmed = false;
            isConfirmed = await DisplayAlert("Confirm", "Are you sure to reset Site Counter ?", "Yes", "No");
            if (isConfirmed)
            {

                MobileCrowdControlGuard ResetData = new MobileCrowdControlGuard()
                {
                    ClientSiteId = (int)_clientSiteId,
                    GuardId = (int)_guardId,
                    UserId = (int)_userId
                };
                _hubConnection.InvokeAsync("ResetSiteCrowdControlCount", ResetData);
                CrowdControlSettingsPopup.IsVisible = false;
            }
        }

        private async void OnCrowdControlLocationSelected(object sender, EventArgs e)
        {
            var selectedLocation = CrowdControlLocationPicker.SelectedItem as DropdownItemsControl;
            if (selectedLocation != null)
            {
                int locationId = selectedLocation.Id;
                string locationName = selectedLocation.Name;
                await SecureStorage.SetAsync("CrowdControlSelectedLocation", selectedLocation.Name);

                // Validate Client Site ID            
                if (_clientSiteId == null || _guardId == null || _userId == null)
                    return;

                MobileCrowdControlGuard mg = new MobileCrowdControlGuard()
                {
                    ClientSiteId = (int)_clientSiteId,
                    GuardId = (int)_guardId,
                    UserId = (int)_userId,
                    Location = locationName
                };

                UpdateGuardCrowdControlLocation(mg);
            }
        }

        private void LoadPickerValues()
        {
            // Set values from 1 to 1000 for both pickers
            List<int> values = Enumerable.Range(1, 1000).ToList();

            IncrementValuePicker.ItemsSource = values;
            DecrementValuePicker.ItemsSource = values;

            // Optionally set default selection
            IncrementValuePicker.SelectedIndex = 0; // Default to "1"
            DecrementValuePicker.SelectedIndex = 0; // Default to "1"
        }

        private void OnIncrementValueChanged(object sender, EventArgs e)
        {
            if (IncrementValuePicker.SelectedIndex != -1)
            {
                selectedIncrement = (int)IncrementValuePicker.SelectedItem;
            }
        }

        private void OnDecrementValueChanged(object sender, EventArgs e)
        {
            if (DecrementValuePicker.SelectedIndex != -1)
            {
                selectedDecrement = (int)DecrementValuePicker.SelectedItem;
            }
        }

        private void OnToggleVolumeControl(object sender, EventArgs e)
        {
            _IsVolumeControlButtonEnabled = !_IsVolumeControlButtonEnabled;
            VolumeButtonControl.Text = $"Volume Button Control = {(_IsVolumeControlButtonEnabled ? "ON" : "OFF")}";
        }

        private async void UpdateGuardCrowdControlLocation(MobileCrowdControlGuard mg)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}CrowdCount/SaveGuardLocation";
            using (HttpClient client = new HttpClient())
            {
                var json = JsonSerializer.Serialize(mg);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                try
                {
                    var response = await client.PostAsync(apiUrl, content);
                }
                catch (Exception)
                {
                    //throw;
                }

            }
        }

        private async void OnDownloadsClicked(object sender, EventArgs e)
        {

            Application.Current.MainPage = new DownloadsHome();
            CloseDrawer();

        }
        private async void OnToolsClicked(object sender, EventArgs e)
        {

            Application.Current.MainPage = new ToolsHome();
            CloseDrawer();

        }
        private async void OnSOPClicked(object sender, EventArgs e)
        {

            Application.Current.MainPage = new SOPPage();
            CloseDrawer();

        }
        private async void OnOffDutyClicked(object sender, EventArgs e)
        {
            // Validate Guard ID
            var guardId = await TryGetSecureId("GuardId", "Guard ID not found. Please validate the License Number first.");
            if (guardId == null) return;

            // Validate Client Site ID
            var clientSiteId = await TryGetSecureId("SelectedClientSiteId", "Please select a valid Client Site.");
            if (clientSiteId == null) return;

            // Validate User ID
            var userId = await TryGetSecureId("UserId", "User ID is invalid. Please log in again.");
            if (userId == null) return;

            try
            {
                string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/UpdateOffDuty?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}";

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        SecureStorage.RemoveAll(); // Clear SecureStorage (logout)
                        System.Diagnostics.Process.GetCurrentProcess().Kill(); // Close the appI
                    }
                    else
                    {
                        string errorMessage = await response.Content.ReadAsStringAsync();
                        await DisplayAlert("Error", $"Off Duty submission failed:\n{errorMessage}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Exception", $"Unexpected error: {ex.Message}", "OK");
            }
        }



        private void OpenDrawer()
        {
            DrawerMenu.TranslationX = 0;
            DrawerOverlay.IsVisible = true;
        }


    }





}
