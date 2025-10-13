using C4iSytemsMobApp.Controls;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Devices.Sensors;
using Plugin.NFC;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using C4iSytemsMobApp.Enums;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using System.Windows.Input;



namespace C4iSytemsMobApp
{
    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly System.Timers.Timer duressCheckTimer = new System.Timers.Timer(3000); // Check every 3 seconds
        private readonly IVolumeButtonService _volumeButtonService;
        private readonly ILogBookServices _logBookServices;
        private int _pcounter = 0;
        private int _CurrentCounter = 0;
        private int _totalpatrons = 0;
        private bool _CcounterShown = false;
        private bool _TcounterShown = true;
        private bool _IsCrowdControlCounterEnabled = false;
        private HubConnection _hubConnection;
        public bool isDrawerOpen = false;
        public event PropertyChangedEventHandler PropertyChanged;
        private int? _clientSiteId;
        private int? _userId;
        private int? _guardId;
        private bool _guardCounterReset = false;
        private int selectedIncrement = 1;
        private int selectedDecrement = 1;
        private bool _IsVolumeControlButtonEnabled = false;
        private List<DropdownItemsControl> _crowdControllocationList;

        public const string ALERT_TITLE = "NFC";
        bool _eventsAlreadySubscribed = false;
        private readonly IScannerControlServices _scannerControlServices;
        private bool _isNfcEnabledForSite = false;
        bool _isDeviceiOS = false;
        private int _notificationCount = 0;
        //private bool HasNotifications = false;
        public int NotificationCount
        {
            get => _notificationCount;
            set
            {
                _notificationCount = value;
                OnPropertyChanged(nameof(NotificationCount));
                OnPropertyChanged(nameof(HasNotifications));
            }
        }
        public bool HasNotifications
        {
            get => _notificationCount > 0;
        }
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
        public bool _shouldOpenDrawerOnReturn = false;
        private int _tags;
        private int _hit;
        private int _frequency;
        private int _missed;
        private string _tour;

        public int Tags { get => _tags; set { _tags = value; OnPropertyChanged(); } }
        public int Hit { get => _hit; set { _hit = value; OnPropertyChanged(); } }
        public int Frequency { get => _frequency; set { _frequency = value; OnPropertyChanged(); } }
        public int Missed { get => _missed; set { _missed = value; OnPropertyChanged(); } }
        public string Tour { get => _tour; set { _tour = value; OnPropertyChanged(); } }

        private CancellationTokenSource _tagStatusCts;
        public ObservableCollection<SiteTagStatusPending> TagFields { get; set; } = new ObservableCollection<SiteTagStatusPending>();
        public ICommand LongPressCommand { get; }
        private DateTime _touchStartTime;
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
            _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
            _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();

            string isCrowdControlEnabledForSiteLocalStored = Preferences.Get("CrowdCountEnabledForSite", "false");

            if (!string.IsNullOrEmpty(isCrowdControlEnabledForSiteLocalStored) && bool.TryParse(isCrowdControlEnabledForSiteLocalStored, out _IsCrowdControlCounterEnabled))                
            ShowCounters = _IsCrowdControlCounterEnabled;
            OnPropertyChanged(nameof(ShowCounters));


            // Temporary command for testing
            LongPressCommand = new Command(() => OnDuressClicked(sender: null, e: null));

            PopupCollectionView.BindingContext = this;

         
        }

        private async Task OnDuressClicked2(object sender, EventArgs e)
        {
            // Your existing duress popup logic here
        }


        protected override async void OnAppearing()
        {
            MainLayout.IsVisible = false;

            string savedTheme = Preferences.Get("AppTheme", "Dark");
            bool isDark = savedTheme == "Dark";

            ThemeSwitch.IsToggled = isDark;
            ThemeStateLabel.Text = isDark ? "On" : "Off";

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

            await StartNFC();

            MainLayout.IsVisible = true;


            // Subscribe to updates
            TagStatusService.Instance.Subscribe(OnTagStatusUpdated);

            // Start polling for this site
            TagStatusService.Instance.StartPolling(_clientSiteId);

            // Cancel any previous loop if exists
            //_tagStatusCts?.Cancel();
            //_tagStatusCts = new CancellationTokenSource();

            //var timer = new PeriodicTimer(TimeSpan.FromSeconds(1)); // every 1 seconds

            //try
            //{
            //    while (await timer.WaitForNextTickAsync(_tagStatusCts.Token))
            //    {
            //        if (_clientSiteId != null)
            //        {
            //            await LoadTagStatusAsync(_clientSiteId);
            //        }
            //    }
            //}
            //catch (OperationCanceledException)
            //{
            //    // Timer was canceled, ignore
            //}
        }



        

        private void OnTagStatusUpdated(int? clientId)
        {
            // Call your existing LoadTagStatusAsync safely
            _ = LoadTagStatusAsync(clientId);
        }


        public async Task LoadTagStatusAsync(int? clientId)
        {
            if (clientId == null) return;

            try
            {
                string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetTagStatus?clientId={clientId}";
                using var client = new HttpClient();
                var response = await client.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode) return;

                string json = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = System.Text.Json.JsonSerializer.Deserialize<List<SiteTagStatus>>(json, options);

                if (result != null && result.Any())
                {
                    var first = result.First();

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var formatted = new FormattedString();

                        // Tags
                        formatted.Spans.Add(new Span { Text = "Tags: ", FontSize = 12, TextColor = Colors.Gray });
                        formatted.Spans.Add(new Span { Text = first.TotalTags.ToString(), FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Colors.Black });

                        // Hit
                        formatted.Spans.Add(new Span { Text = "   Hit: ", FontSize = 12, TextColor = Colors.Gray });
                        formatted.Spans.Add(new Span { Text = first.ScannedTags.ToString(), FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Colors.Black });

                        // Fq
                        formatted.Spans.Add(new Span { Text = "   Fq: ", FontSize = 12, TextColor = Colors.Gray });
                        formatted.Spans.Add(new Span { Text = first.CompletedRounds.ToString(), FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Colors.Black });

                        // Missed (clickable)
                        formatted.Spans.Add(new Span { Text = "   Missed: ", FontSize = 12, TextColor = Colors.Gray });
                        var missedSpan = new Span
                        {
                            Text = first.RemainingTags.ToString(),
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 12,
                            TextColor = Colors.Black
                        };

                        // Add tap gesture directly to the Span
                        missedSpan.GestureRecognizers.Add(new TapGestureRecognizer
                        {
                            Command = new Command(async () =>
                            {
                                await Application.Current.MainPage.DisplayAlert("Missed Info", $"Missed value: {first.RemainingTags}", "OK");
                            })
                        });

                        formatted.Spans.Add(missedSpan);

                        // Tour
                        formatted.Spans.Add(new Span { Text = "   Tour: ", FontSize = 12, TextColor = Colors.Gray });
                        formatted.Spans.Add(new Span { Text = first.Tour, FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Colors.Black });

                        // Assign to Label
                        TagStatusLabel.FormattedText = formatted;
                    });

                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading tag status: {ex.Message}");
            }
        }


        public async Task LoadTagStatusPendingAsync(int? clientId)
        {
            if (clientId == null) return;

            try
            {
                string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetTagStatusPending?clientId={clientId}";
                using var client = new HttpClient();
                var response = await client.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode) return;

                string json = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = System.Text.Json.JsonSerializer.Deserialize<List<SiteTagStatus>>(json, options);

                if (result != null && result.Any())
                {
                    var first = result.First();

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var formatted = new FormattedString();

                        // Tags
                        formatted.Spans.Add(new Span { Text = "Tags: ", FontSize = 12, TextColor = Colors.Gray });
                        formatted.Spans.Add(new Span { Text = first.TotalTags.ToString(), FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Colors.Black });

                        // Hit
                        formatted.Spans.Add(new Span { Text = "   Hit: ", FontSize = 12, TextColor = Colors.Gray });
                        formatted.Spans.Add(new Span { Text = first.ScannedTags.ToString(), FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Colors.Black });

                        // Fq
                        formatted.Spans.Add(new Span { Text = "   Fq: ", FontSize = 12, TextColor = Colors.Gray });
                        formatted.Spans.Add(new Span { Text = first.CompletedRounds.ToString(), FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Colors.Black });

                        // Missed (clickable)
                        formatted.Spans.Add(new Span { Text = "   Missed: ", FontSize = 12, TextColor = Colors.Gray });
                        var missedSpan = new Span
                        {
                            Text = first.RemainingTags.ToString(),
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 12,
                            TextColor = Colors.Black
                        };

                        // Add tap gesture directly to the Span
                        missedSpan.GestureRecognizers.Add(new TapGestureRecognizer
                        {
                            Command = new Command(async () =>
                            {
                                await Application.Current.MainPage.DisplayAlert("Missed Info", $"Missed value: {first.RemainingTags}", "OK");
                            })
                        });

                        formatted.Spans.Add(missedSpan);

                        // Tour
                        formatted.Spans.Add(new Span { Text = "   Tour: ", FontSize = 12, TextColor = Colors.Gray });
                        formatted.Spans.Add(new Span { Text = first.Tour, FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Colors.Black });

                        // Assign to Label
                        TagStatusLabel.FormattedText = formatted;
                    });

                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading tag status: {ex.Message}");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            App.IsVolumeControlEnabledForCounter = false; // Disable custom volume handling

            if (_isNfcEnabledForSite && CrossNFC.IsSupported && CrossNFC.Current.IsAvailable)
            {
                await StopListening();
            }
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                _hubConnection.StopAsync();
                _hubConnection.DisposeAsync();
            }

            // Unsubscribe safely
            TagStatusService.Instance.Unsubscribe(OnTagStatusUpdated);
        }

        private async void LoadLoggedInUser()
        {
            try
            {
                string userName = Preferences.Get("UserName","");
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
            lblClientSite.Text = $"Client Site: {Preferences.Get("ClientSite", "N/A")}";                      
        }

        private async void OnManualPositionClicked(object sender, EventArgs e)
        {
            _tagStatusCts?.Cancel();
            _tagStatusCts?.Dispose();
            _tagStatusCts = null;
            Application.Current.MainPage = new NavigationPage(new WebIncidentReport());
        }

        private async void OnLogActivityClicked(object sender, EventArgs e)
        {
            _tagStatusCts?.Cancel();
            _tagStatusCts?.Dispose();
            _tagStatusCts = null;
            Application.Current.MainPage = new NavigationPage(new LogActivity()); // Redirect to LogActivityPage
        }

        private async void OnAudioClicked(object sender, EventArgs e)
        {
            _tagStatusCts?.Cancel();
            _tagStatusCts?.Dispose();
            _tagStatusCts = null;
            Application.Current.MainPage = new NavigationPage(new Audio());

        }

        private async void OnMultimediaClicked(object sender, EventArgs e)
        {
            _tagStatusCts?.Cancel();
            _tagStatusCts?.Dispose();
            _tagStatusCts = null;
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



            string gpsCoordinates = Preferences.Get("GpsCoordinates", "");


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
            string idString = Preferences.Get(key, "");

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


                string clientSiteIdString = Preferences.Get("SelectedClientSiteId", "");
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
                Preferences.Remove("UserId");
                Preferences.Remove("UserName");
                Preferences.Remove("UserRole");
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
                        if (_isNfcEnabledForSite && CrossNFC.IsSupported && CrossNFC.Current.IsAvailable)
                        {
                            Task.Run(async () => await StopListening());
                        }
                        
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
                    Preferences.Clear(); // Clear SecureStorage (logout)
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

            var crowdControlSettingsService = IPlatformApplication.Current.Services.GetService<ICrowdControlServices>();
            var settings = await crowdControlSettingsService.GetCrowdControlSettingsAsync(_clientSiteId.ToString());

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
                        Preferences.Set("CrowdControlSelectedLocation", selectedLocation.Name);

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
            string _CrowdControlSelectedLocation = Preferences.Get("CrowdControlSelectedLocation","");
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
                Preferences.Set("CrowdControlSelectedLocation", selectedLocation.Name);

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
            App.IsVolumeControlEnabledForCounter = !_IsVolumeControlButtonEnabled;
            _IsVolumeControlButtonEnabled = !_IsVolumeControlButtonEnabled;
            VolumeButtonControl.Text = $"Volume Button Control = {(_IsVolumeControlButtonEnabled ? "ON" : "OFF")}";
        }

        private async void OnShowAllSiteCountersButtonControl(object sender, EventArgs e)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}CrowdCount/GetCrowdCountControlDataAndSettings?siteId={_clientSiteId}";
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var settingsAndData = await response.Content.ReadFromJsonAsync<ClientSiteMobileCrowdControlDTO>();

                    if (settingsAndData != null && settingsAndData.CounterNameAndCount?.Any() == true)
                    {
                        // Clear existing rows (except header row with title and close button)
                        AllCrowdCounterGrid.RowDefinitions.Clear();
                        AllCrowdCounterGrid.Children.Clear();

                        // Add title and close button (row 0)
                        AllCrowdCounterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        var titleLabel = new Label
                        {
                            Text = "All Counter settings",
                            FontSize = 18,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Start,
                            VerticalOptions = LayoutOptions.Start
                        };
                        AllCrowdCounterGrid.Add(titleLabel, 0, 0);

                        var closeBtn = new ImageButton
                        {
                            BackgroundColor = Colors.Transparent,
                            WidthRequest = 30,
                            HeightRequest = 30,
                            HorizontalOptions = LayoutOptions.End,
                            VerticalOptions = LayoutOptions.Start
                        };
                        closeBtn.Clicked += OnAllCounterSettingsPopupCloseClicked;
                        closeBtn.Source = new FontImageSource
                        {
                            Glyph = "\uf00d",
                            FontFamily = "FontAwesome",
                            Color = Colors.White,
                            Size = 20
                        };
                        AllCrowdCounterGrid.Add(closeBtn, 1, 0);

                        // Add each counter row dynamically
                        int rowIndex = 1;

                        foreach (var kvp in settingsAndData.CounterNameAndCount)
                        {
                            // Add row definition
                            AllCrowdCounterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                            // AnimatedCounter
                            var counter = new AnimatedCounter
                            {
                                Padding = 5,
                                Value = kvp.Value,
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center
                            };
                            AllCrowdCounterGrid.Add(counter, 0, rowIndex);

                            // Label
                            var label = new Label
                            {
                                Text = kvp.Key,
                                TextColor = Colors.White,
                                FontAttributes = FontAttributes.Bold,
                                HorizontalOptions = LayoutOptions.Start,
                                VerticalOptions = LayoutOptions.Center,
                                Padding = 5
                            };
                            AllCrowdCounterGrid.Add(label, 1, rowIndex);

                            rowIndex++;
                        }

                        //Current row count
                        // Add row definition
                        AllCrowdCounterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        // AnimatedCounter
                        var currentcounter = new AnimatedCounter
                        {
                            Padding = 5,
                            Value = settingsAndData.CurrentCount,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        };
                        AllCrowdCounterGrid.Add(currentcounter, 0, rowIndex);

                        // Label
                        var currentlabel = new Label
                        {
                            Text = "Current",
                            TextColor = Colors.White,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Center,
                            Padding = 5
                        };
                        AllCrowdCounterGrid.Add(currentlabel, 1, rowIndex);
                        rowIndex++;

                        //Total Count
                        // Add row definition
                        AllCrowdCounterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        // AnimatedCounter
                        var totalcounter = new AnimatedCounter
                        {
                            Padding = 5,
                            Value = settingsAndData.TotalCount,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        };
                        AllCrowdCounterGrid.Add(totalcounter, 0, rowIndex);

                        // Label
                        var totallabel = new Label
                        {
                            Text = "Total",
                            TextColor = Colors.White,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Center,
                            Padding = 5
                        };
                        AllCrowdCounterGrid.Add(totallabel, 1, rowIndex);
                        rowIndex++;

                        //Till Date Count
                        // Add row definition
                        AllCrowdCounterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        // AnimatedCounter
                        var tillDatecounter = new AnimatedCounter
                        {
                            Padding = 5,
                            Value = settingsAndData.TillDateCount,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        };
                        AllCrowdCounterGrid.Add(tillDatecounter, 0, rowIndex);

                        // Label
                        var tillDatelabel = new Label
                        {
                            Text = settingsAndData.TillDate,
                            TextColor = Colors.White,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Center,
                            Padding = 5
                        };
                        AllCrowdCounterGrid.Add(tillDatelabel, 1, rowIndex);


                    }
                }
            }

            AllCrowdControlViewPopup.IsVisible = true;
        }


        private void OnAllCounterSettingsPopupCloseClicked(object sender, EventArgs e)
        {
            AllCrowdControlViewPopup.IsVisible = false;
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

        private void OnNotificationsClicked(object sender, EventArgs e)
        {
            // Your update check logic here
            DisplayAlert("Notification", "New feature coming soon...", "OK");
            NotificationCount += 1;
            //NotificationIcon.IsVisible = !NotificationIcon.IsVisible;
        }
        private async void OnSOPClicked(object sender, EventArgs e)
        {

            Application.Current.MainPage = new SOPPage();
            CloseDrawer();

        }

        private void OnRosterClicked(object sender, EventArgs e)
        {

            DisplayAlert("Roster", "New feature coming soon...", "OK");

        }

        private async void OnDrawerMenuSettingsClicked(object sender, EventArgs e)
        {
            Application.Current.MainPage = new MenuSettingsPage();
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
                        Preferences.Clear(); // Clear SecureStorage (logout)
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
        private void OnThemeSwitchToggled(object sender, ToggledEventArgs e)
        {
            bool isDark = e.Value;

            // Set the app theme
            Application.Current.UserAppTheme = isDark ? AppTheme.Dark : AppTheme.Light;

            // Save preference
            Preferences.Set("AppTheme", isDark ? "Dark" : "Light");

            // Optional: update label
            ThemeStateLabel.Text = isDark ? "On" : "Off";
        }

        #region "NFC Methods"

        private async Task StartNFC()
        {
            // Check NFC status
            string isNfcEnabledForSiteLocalStored = Preferences.Get("NfcOnboarded", "");

            if (!string.IsNullOrEmpty(isNfcEnabledForSiteLocalStored) && bool.TryParse(isNfcEnabledForSiteLocalStored, out _isNfcEnabledForSite))
            {
                // In order to support Mifare Classic 1K tags (read/write), you must set legacy mode to true.
                CrossNFC.Legacy = false;

                if (CrossNFC.IsSupported)
                {                    
                    if (CrossNFC.Current.IsAvailable)
                    {
                        NfcIsEnabled = CrossNFC.Current.IsEnabled;
                        if (!NfcIsEnabled)
                            await DisplayAlert(ALERT_TITLE, "NFC is disabled from Home Page", "OK");

                        if (DeviceInfo.Platform == DevicePlatform.iOS)
                            _isDeviceiOS = true;

                        //await InitializeNFCAsync();
                        await AutoStartAsync().ConfigureAwait(false);
                    }
                }
            }

        }

        async Task AutoStartAsync()
        {
            // Some delay to prevent Java.Lang.IllegalStateException "Foreground dispatch can only be enabled when your activity is resumed" on Android
            await Task.Delay(500);
            await StartListeningIfNotiOS();
        }

        void SubscribeEvents()
        {
            if (_eventsAlreadySubscribed)
                UnsubscribeEvents();

            _eventsAlreadySubscribed = true;

            CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
            CrossNFC.Current.OnNfcStatusChanged += Current_OnNfcStatusChanged;
            CrossNFC.Current.OnTagListeningStatusChanged += Current_OnTagListeningStatusChanged;

            if (_isDeviceiOS)
                CrossNFC.Current.OniOSReadingSessionCancelled += Current_OniOSReadingSessionCancelled;
        }

        void UnsubscribeEvents()
        {
            CrossNFC.Current.OnMessageReceived -= Current_OnMessageReceived;
            CrossNFC.Current.OnNfcStatusChanged -= Current_OnNfcStatusChanged;
            CrossNFC.Current.OnTagListeningStatusChanged -= Current_OnTagListeningStatusChanged;

            if (_isDeviceiOS)
                CrossNFC.Current.OniOSReadingSessionCancelled -= Current_OniOSReadingSessionCancelled;

            _eventsAlreadySubscribed = false;
        }
        void Current_OnTagListeningStatusChanged(bool isListening) => DeviceIsListening = isListening;

        async void Current_OnNfcStatusChanged(bool isEnabled)
        {
            NfcIsEnabled = isEnabled;
            await DisplayAlert(ALERT_TITLE, $"NFC has been {(isEnabled ? "enabled" : "disabled")} from Home Page", "OK");
        }

        async void Current_OnMessageReceived(ITagInfo tagInfo)
        {
            if (tagInfo == null)
            {
                await DisplayAlert(ALERT_TITLE, "No tag found", "OK");
                return;
            }

            var identifier = tagInfo.Identifier;
            var serialNumber = NFCUtils.ByteArrayToHexString(identifier, "");
            var title = !tagInfo.IsEmpty ? $"Tag Info: {tagInfo}" : "Tag Info";

            if (!tagInfo.IsSupported)
            {
                await DisplayAlert(ALERT_TITLE, "Unsupported NFC tag", "OK");
            }
            else if (!string.IsNullOrEmpty(serialNumber))
            {
                await ShowToastMessage($"Tag scanned. Logging activity...");
                var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
                if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return;

                var scannerSettings = await _scannerControlServices.FetchTagInfoDetailsAsync(clientSiteId.ToString(), serialNumber, guardId.ToString(), userId.ToString());
                if (scannerSettings != null)
                {
                    if (scannerSettings.IsSuccess)
                    {
                        // Valid tag - log activity
                        //LogActivityTask(scannerSettings.tagInfoLabel);
                        int _scannerType = (int)ScanningType.NFC;
                        var _taguid = serialNumber;
                        if (!scannerSettings.tagFound) { _taguid = "NA"; }
                        var (isSuccess, msg) = await _logBookServices.LogActivityTask(scannerSettings.tagInfoLabel, _scannerType, _taguid);
                        if (isSuccess)
                        {
                            await ShowToastMessage(msg);
                        }
                        else
                        {
                            await DisplayAlert("Error", msg ?? "Failed to log activity", "OK");
                        }
                    }
                    else
                    {
                        await DisplayAlert(ALERT_TITLE, scannerSettings?.message ?? "Unknown error", "OK");
                        return;
                    }
                }
                else
                {
                    await DisplayAlert(ALERT_TITLE, scannerSettings?.message ?? "Unknown error", "OK");
                    return;
                }

            }
            else
            {
                //var first = tagInfo.Records[0];
                //await DisplayAlert(ALERT_TITLE, GetMessage(first), "OK");
                await DisplayAlert(ALERT_TITLE, "Tag UID not found", "OK");
                return;
            }
        }

        void Current_OniOSReadingSessionCancelled(object sender, EventArgs e) => Debug.WriteLine("iOS NFC Session has been cancelled");

        async Task StartListeningIfNotiOS()
        {
            if (_isDeviceiOS)
            {
                SubscribeEvents();
                return;
            }
            await BeginListening();
        }

        async Task BeginListening()
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SubscribeEvents();
                    CrossNFC.Current.StartListening();
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert(ALERT_TITLE, ex.Message, "OK");
            }
        }

        async Task StopListening()
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CrossNFC.Current.StopListening();
                    UnsubscribeEvents();
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert(ALERT_TITLE, ex.Message, "OK");
            }
        }


        #endregion "NFC Methods"

        private async Task ShowToastMessage(string message)
        {
            await Toast.Make(message, ToastDuration.Long).Show();
            
        }

        private async Task<(int guardId, int clientSiteId, int userId)> GetSecureStorageValues()
        {
            int.TryParse(Preferences.Get("GuardId","0"), out int guardId);
            int.TryParse(Preferences.Get("SelectedClientSiteId","0"), out int clientSiteId);
            int.TryParse(Preferences.Get("UserId","0"), out int userId);

            if (guardId <= 0)
            {
                await DisplayAlert("Error", "Guard ID not found. Please validate the License Number first.", "OK");
                return (-1, -1, -1);
            }
            if (clientSiteId <= 0)
            {
                await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
                return (-1, -1, -1);
            }
            if (userId <= 0)
            {
                await DisplayAlert("Validation Error", "User ID is invalid. Please log in again.", "OK");
                return (-1, -1, -1);
            }

            return (guardId, clientSiteId, userId);
        }

        private async void OnExclamationClicked(object sender, EventArgs e)
        {
            PopupOverlay.IsVisible = true;
            var clientSiteId = await TryGetSecureId("SelectedClientSiteId", "Please select a valid Client Site.");
            if (clientSiteId == null) return;
            // Call API to get tag fields
            // Call API to get tag fields for this client site
            var fieldsFromApi = await GetTagFieldsFromApi(clientSiteId.Value);
            if (fieldsFromApi == null || !fieldsFromApi.Any()) return;

            // Clear the existing collection
            TagFields.Clear();

            // Add fetched fields to the ObservableCollection
            foreach (var field in fieldsFromApi)
            {
                TagFields.Add(field);
            }
        }


        private void OnClosePopupClicked(object sender, EventArgs e)
        {
            PopupOverlay.IsVisible = false;
        }


        // Dummy API call
        private async Task<List<SiteTagStatusPending>> GetTagFieldsFromApi(int? clientId)
        {
            try
            {
                string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetTagStatusPending?clientId={clientId}";

                using var client = new HttpClient();
                var response = await client.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"API call failed with status code: {response.StatusCode}");
                    return new List<SiteTagStatusPending>();
                }

                string json = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = System.Text.Json.JsonSerializer.Deserialize<List<SiteTagStatusPending>>(json, options);

                return result ?? new List<SiteTagStatusPending>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching tag fields from API: {ex.Message}");
                return new List<SiteTagStatusPending>();
            }
        }


    }


    public class SiteTagStatusPending
    {

        public string LabelDescription { get; set; }   // Tag label / description
        public string TagType { get; set; }            // NFC, BLE, Other
        public int RoundNumber { get; set; }           // Round number
        public int TodayScanCount { get; set; }             // How many times scanned today

    }


    public class TagField
    {
        public string Type { get; set; }
        public string Label { get; set; }
    }

    public class SiteTagStatus
    {
        public int ClientSiteId { get; set; }
        public int TotalTags { get; set; }
        public int ScannedTags { get; set; }
        public int RemainingTags { get; set; }
        public int CompletedRounds { get; set; }
        public string Tour { get; set; }
    }


}
