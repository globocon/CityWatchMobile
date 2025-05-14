using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace C4iSytemsMobApp
{
    public partial class MainPage : ContentPage
    {
        private readonly HttpClient _httpClient;
        private readonly System.Timers.Timer duressCheckTimer = new System.Timers.Timer(3000); // Check every 3 seconds
        private int _counter = 0;
        private int _CurrentCounter = 0;
        private int _totalpatrons = 0;
        private bool _CcounterShown = false;
        private bool _TcounterShown = true;

        public MainPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            LoadLoggedInUser();
            LoadSecureData();
            InitializePatronsCounterDisplay();
            // Start checking duress status on app load
            duressCheckTimer.Elapsed += async (s, e) => await CheckDuressStatus();
            duressCheckTimer.AutoReset = true;
            duressCheckTimer.Start();

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


        

        //private async void OnDuressClicked(object sender, EventArgs e)
        //{
        //    if (sender is Frame duressFrame)
        //    {
        //        var stackLayout = duressFrame.Content as StackLayout;
        //        if (stackLayout == null) return;

        //        var statusLabel = stackLayout.Children.OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("Status:"));
        //        if (statusLabel == null) return;

        //        // **Prevent clicking if already active**
        //        if (statusLabel.Text.Contains("Active"))
        //        {
        //            await DisplayAlert("Info", "Duress is already active.", "OK");
        //            return;
        //        }

        //        bool isConfirmed = await DisplayAlert("Confirm", "Are you sure you want to activate Duress?", "Yes", "No");
        //        if (!isConfirmed)
        //            return; // Exit if the user cancels

        //        // Retrieve GuardId securely
        //        string guardIdString = await SecureStorage.GetAsync("GuardId");
        //        if (string.IsNullOrWhiteSpace(guardIdString) || !int.TryParse(guardIdString, out int guardId) || guardId <= 0)
        //        {
        //            await DisplayAlert("Error", "Guard ID not found. Please validate the License Number first.", "OK");
        //            return;
        //        }

        //        // Retrieve and validate Client Site ID
        //        string clientSiteIdString = await SecureStorage.GetAsync("SelectedClientSiteId");
        //        if (string.IsNullOrWhiteSpace(clientSiteIdString) || !int.TryParse(clientSiteIdString, out int clientSiteId) || clientSiteId <= 0)
        //        {
        //            await DisplayAlert("Validation Error", "Please select a valid Client Site.", "OK");
        //            return;
        //        }

        //        // Retrieve and validate User ID
        //        string userIdString = await SecureStorage.GetAsync("UserId");
        //        if (string.IsNullOrWhiteSpace(userIdString) || !int.TryParse(userIdString, out int userId) || userId <= 0)
        //        {
        //            await DisplayAlert("Validation Error", "User ID is invalid. Please log in again.", "OK");
        //            return;
        //        }

        //        // API URL
        //        string apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SaveClientSiteDuress?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}";

        //        using (HttpClient client = new HttpClient())
        //        {
        //            var response = await client.GetAsync(apiUrl);
        //            if (response.IsSuccessStatusCode)
        //            {
        //                // **Update UI to indicate active status**
        //                duressFrame.BackgroundColor = Colors.Red;
        //                statusLabel.Text = "Status: Active";
        //                statusLabel.TextColor = Colors.White;
        //            }
        //            else
        //            {
        //                string errorMessage = await response.Content.ReadAsStringAsync();
        //                await DisplayAlert("Error", $"Duress submission failed: {errorMessage}", "OK");
        //            }
        //        }
        //    }
        //}





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
                    SecureStorage.RemoveAll(); // Clear SecureStorage (logout)
                    System.Diagnostics.Process.GetCurrentProcess().Kill(); // Close the app
                }
            });

            return true; // Prevent default back button behavior
        }
        private void OnMenuClicked(object sender, EventArgs e)
        {
           
        }

        private void OnIncrementClicked(object sender, EventArgs e)
        {
            _totalpatrons++;
            _counter++;
            _CurrentCounter++;
            CounterLabel.Text = _counter.ToString("0000");
            if (_CcounterShown)
            {
                total_current_patronsLabel.Text = $"C{_CurrentCounter.ToString("000000")}";
            }
            else
            {
                total_current_patronsLabel.Text = $"T{_totalpatrons.ToString("000000")}";
            }
               
        }

        private void OnDecrementClicked(object sender, EventArgs e)
        {
            if (_counter > 0)
            {
                _counter--;
                _CurrentCounter--;
            }            
            CounterLabel.Text = _counter.ToString("0000");
            if (_CcounterShown)
            {
                total_current_patronsLabel.Text = $"C{_CurrentCounter.ToString("000000")}";
            }
        }

        private void InitializePatronsCounterDisplay()
        {
            if (_CcounterShown)
            {
                total_current_patronsLabel.Text = $"C{_CurrentCounter.ToString("000000")}";
            }
            else
            {
                total_current_patronsLabel.Text = $"T{_totalpatrons.ToString("000000")}";
            }
        }
        private void ToggleCounterDisplay(object sender, EventArgs e)
        {
            if (_CcounterShown)
            {
                total_current_patronsLabel.Text = $"T{_totalpatrons.ToString("000000")}";
                _CcounterShown = false;
                _TcounterShown = true;
            }
            else
            {
                total_current_patronsLabel.Text = $"C{_CurrentCounter.ToString("000000")}";
                _CcounterShown = true;
                _TcounterShown = false;
            }
        }


    }


   
}
