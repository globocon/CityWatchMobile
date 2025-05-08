using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace C4iSytemsMobApp;

public partial class LogActivity : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private System.Timers.Timer _logRefreshTimer;
    private List<int> _lastLogIds = new();
    public LogActivity()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        LoadActivities();
        //LoadLogs();
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();

        LoadLogs(); // Call when the page is about to appear

        // Set up a timer for periodic refresh (if needed)
        _logRefreshTimer = new System.Timers.Timer(30000); // 30 seconds
        _logRefreshTimer.Elapsed += async (s, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadLogs(); // Refresh logs every second (or change the interval)
            });
        };
        _logRefreshTimer.AutoReset = true;
        _logRefreshTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_logRefreshTimer != null)
        {
            _logRefreshTimer.Stop();
            _logRefreshTimer.Dispose();
            _logRefreshTimer = null;
        }
    }

    private async void LoadActivities()
    {
        try
        {
            ButtonContainer.Children.Clear(); // Clear previous items if reloading
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetActivities?type=2";

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Error", "Failed to load activities", "OK");
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            var activities = JsonSerializer.Deserialize<List<ActivityModel>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (activities == null) return;

            foreach (var activity in activities)
            {
                var button = new Button
                {
                    Text = activity.Name,
                    // BackgroundColor = Colors.Blue,
                    TextColor = Colors.White,
                    Margin = new Thickness(5)
                };

                button.Clicked += (s, e) => MainThread.InvokeOnMainThreadAsync(() => LogActivityTask(activity.Name));
                ButtonContainer.Children.Add(button);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }


    private async void LoadLogs()
    {
        try
        {
            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
            if (guardId <= 0 || clientSiteId <= 0 || userId <= 0)
                return;

            LogDisplayArea.Children.Clear();

            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetSiteLog?clientsiteId={clientSiteId}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Error", "Failed to load site logs.", "OK");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var logs = JsonSerializer.Deserialize<List<GuardLogDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (logs == null || logs.Count == 0)
            {
                LogDisplayArea.Children.Add(new Label
                {
                    Text = "No logs available for today.",
                    TextColor = Colors.Gray,
                    FontSize = 12
                });
                return;
            }

            foreach (var log in logs)
            {
                var contentLayout = new VerticalStackLayout
                {
                    Spacing = 3,
                    Children =
                {
                new Label
        {
            FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span
                    {
                        Text = log.GuardInitials,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.Teal,
                        FontSize = 13
                    },
                    new Span
                    {
                        Text = $"  {log.EventDateTimeLocal:HH:mm}",
                        FontSize = 11,
                        TextColor = Colors.Gray
                    }
                }
            },
            Margin = new Thickness(0, 0, 0, 2)
        },
        new Label
        {
            Text = log.Notes ?? "",
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = Colors.Black,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10)
        }

                }
                };

                foreach (var imageUrl in log.ImageUrls ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                    {
                        try
                        {
                            contentLayout.Children.Add(new Image
                            {
                                Source = ImageSource.FromUri(new Uri(imageUrl)),
                                HeightRequest = 130,
                                Margin = new Thickness(0, 4, 0, 4)
                            });
                        }
                        catch
                        {
                            contentLayout.Children.Add(new Label
                            {
                                Text = "(Image could not be loaded)",
                                TextColor = Colors.Red,
                                FontSize = 11
                            });
                        }
                    }
                }

                var logCard = new Frame
                {
                    CornerRadius = 8,
                    Padding = 6,
                    Margin = new Thickness(2, 4),
                    BackgroundColor = Color.FromArgb("#F2F2F2"),
                    Content = contentLayout
                };

                LogDisplayArea.Children.Add(logCard);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred while loading logs: {ex.Message}", "OK");
        }
    }




    private async Task LogActivityTask(string activityDescription)
    {
        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return;
        var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/PostActivity?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}&activityString={Uri.EscapeDataString(activityDescription)}";


        try
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    await ShowToastMessage("Log entry added successfully.");
                    CustomLogEntry.Text = string.Empty; // Clear entry after success
                    await Task.Delay(2000); // Wait for 2 seconds
                    Application.Current.MainPage = new NavigationPage(new MainPage());
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    await ShowToastMessage($"Failed: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                await ShowToastMessage($"Error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async void OnAddLogEntryClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CustomLogEntry.Text))
        {
            await DisplayAlert("Error", "Log entry cannot be empty", "OK");
            return;
        }

        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return;
        var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/PostActivity?guardId={guardId}&clientsiteId={clientSiteId}&userId={userId}&activityString={Uri.EscapeDataString(CustomLogEntry.Text.Trim())}";



        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                await ShowToastMessage("Log entry added successfully.");
                CustomLogEntry.Text = string.Empty; // Clear entry after success
                await Task.Delay(2000); // Wait for 2 seconds
                Application.Current.MainPage = new NavigationPage(new MainPage());
            }
            else
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                await ShowToastMessage($"Failed: {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            await ShowToastMessage($"Error: {ex.Message}");
        }
    }

    private async Task<(int guardId, int clientSiteId, int userId)> GetSecureStorageValues()
    {
        int.TryParse(await SecureStorage.GetAsync("GuardId"), out int guardId);
        int.TryParse(await SecureStorage.GetAsync("SelectedClientSiteId"), out int clientSiteId);
        int.TryParse(await SecureStorage.GetAsync("UserId"), out int userId);

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


    private async void OnHomeClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new MainPage());
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle back button logic here
        Application.Current.MainPage = new NavigationPage(new MainPage());

        // Return true to prevent default behavior (going back)
        return true;
    }


    // Method to display toast message
    private async Task ShowToastMessage(string message)
    {
        var toast = new Frame
        {
            Content = new Label
            {
                Text = message,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 16
            },
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.7),
            CornerRadius = 10,
            Padding = 15,
            Margin = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, // Centered vertically
            Opacity = 0
        };

        // Add the toast to the main grid
        MainGrid.Children.Add(toast);

        // Animate the toast appearance and disappearance
        await toast.FadeTo(1, 250);       // Fade In
        await Task.Delay(2000);           // Show for 2 seconds
        await toast.FadeTo(0, 250);       // Fade Out

        // Remove toast after display
        MainGrid.Children.Remove(toast);
    }

}

public class ActivityModel
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class GuardLogDto
{
    public int Id { get; set; }
    public DateTime EventDateTime { get; set; }
    public string EventDateTimeLocal { get; set; }
    public string EventDateTimeZoneShort { get; set; }
    public string Notes { get; set; }
    public List<string> ImageUrls { get; set; }
    public string GuardInitials { get; set; }
}
