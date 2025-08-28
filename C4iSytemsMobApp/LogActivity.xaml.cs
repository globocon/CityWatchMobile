using C4iSytemsMobApp.Interface;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;



namespace C4iSytemsMobApp;

public partial class LogActivity : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private System.Timers.Timer _logRefreshTimer;
    private List<int> _lastLogIds = new();
    private CancellationTokenSource _delayCancellationTokenSource;
    private List<GuardLogDto> _lastLogs = new();
    public ObservableCollection<MyFileModel> SelectedFiles { get; set; }
     = new ObservableCollection<MyFileModel>();
    public LogActivity()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        LoadActivities();
        //LoadLogs();
        FilesCollection.ItemsSource = SelectedFiles;

    

    }
    private void PopupOverlay_SizeChanged(object sender, EventArgs e)
    {
        if (PopupOverlay.Width > 0)
        {
            // Make popup width 90% of screen width
            MyPopupFrame.WidthRequest = PopupOverlay.Width * 0.9;

            // Optional: Limit height to 80% of screen height
            MyPopupFrame.HeightRequest = PopupOverlay.Height * 0.8;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        LoadLogs(); // Call when the page is about to appear

        // Set up a timer for periodic refresh every 1 second
        _logRefreshTimer = new System.Timers.Timer(1000); // 1 second = 1000 ms
        _logRefreshTimer.Elapsed += async (s, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadLogs(); // Refresh logs every second
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
            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
            
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetActivities?type=2&siteid={clientSiteId}";

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Error", "Failed to load activities", "OK");
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            var activities = JsonSerializer.Deserialize<List<ActivityModel>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (activities == null) return;

            // Sort by numeric prefix in Label (e.g., "01", "02", etc.)
            var sortedActivities = activities.OrderBy(a => ExtractLabelPrefix(a.Label)).ToList();

            foreach (var activity in sortedActivities)
            {
                var button = new Button
                {
                    Text = activity.Name,
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

    private int ExtractLabelPrefix(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return int.MaxValue;

        var parts = label.Split(' ');
        if (parts.Length == 0) return int.MaxValue;

        return int.TryParse(parts[0], out int result) ? result : int.MaxValue;
    }

    private async void LoadLogs()
    {
        try
        {
            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
            if (guardId <= 0 || clientSiteId <= 0 || userId <= 0)
                return;

            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetSiteLog?clientsiteId={clientSiteId}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Error", "Failed to load site logs.", "OK");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var logs = JsonSerializer.Deserialize<List<GuardLogDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // If logs haven't changed, don't update UI
            if (AreLogsEqual(logs, _lastLogs))
                return;

            _lastLogs = logs; // Update cache

            LogDisplayArea.Children.Clear();

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
                    }
                }
                };

                // Notes / IR handling
                Label noteLabel;
                if (log.IrEntryType == true)
                {
                    var formattedText = new FormattedString();
                    var noteText = log.Notes?.Trim() ?? "";
                    formattedText.Spans.Add(new Span
                    {
                        Text = noteText + " ",
                        TextColor = Colors.Black,
                        FontSize = 12
                    });

                    string blobUrl = null;
                    if (!string.IsNullOrWhiteSpace(noteText) && noteText.Length >= 8)
                    {
                        var folder = new string(noteText.Take(8).ToArray());
                        var blobFileName = noteText.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                            ? noteText
                            : noteText + ".pdf";

                        var encodedBlobName = Uri.EscapeDataString(blobFileName);
                        blobUrl = $"https://c4istorage1.blob.core.windows.net/irfiles/{folder}/{encodedBlobName}";
                    }

                    if (!string.IsNullOrWhiteSpace(blobUrl))
                    {
                        var linkSpan = new Span
                        {
                            Text = "Click here",
                            TextColor = Colors.Blue,
                            FontSize = 12,
                            TextDecorations = TextDecorations.Underline
                        };

                        var tapGesture = new TapGestureRecognizer();
                        tapGesture.Tapped += async (s, e) =>
                        {
                            try
                            {
                                await Browser.Default.OpenAsync(blobUrl, BrowserLaunchMode.SystemPreferred);
                            }
                            catch
                            {
                                await Application.Current.MainPage.DisplayAlert("Error", "Unable to open link.", "OK");
                            }
                        };

                        linkSpan.GestureRecognizers.Add(tapGesture);
                        formattedText.Spans.Add(linkSpan);
                    }

                    noteLabel = new Label
                    {
                        FormattedText = formattedText,
                        LineBreakMode = LineBreakMode.WordWrap,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                }
                else
                {
                    var noteText = log.Notes ?? "";

                    if (noteText.Contains("Mob app image upload", StringComparison.OrdinalIgnoreCase))
                    {
                        var formattedText = new FormattedString();

                        // Add the main text first
                        formattedText.Spans.Add(new Span
                        {
                            Text = "Mob app image upload\n",
                            TextColor = Colors.Black,
                            FontSize = 12
                        });

                        // Find all links like <a href="url">filename</a>
                        var regex = new System.Text.RegularExpressions.Regex(
                            "<a\\s+href=\\\"(?<url>[^\\\"]+)\\\"[^>]*>(?<text>[^<]+)</a>",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        foreach (System.Text.RegularExpressions.Match match in regex.Matches(noteText))
                        {
                            var url2 = match.Groups["url"].Value;
                            var linkText = match.Groups["text"].Value;

                            // Add prefix text
                            formattedText.Spans.Add(new Span
                            {
                                Text = "See attached file ",
                                TextColor = Colors.Black,
                                FontSize = 12
                            });

                            var linkSpan = new Span
                            {
                                Text = linkText + "\n",
                                TextColor = Colors.Blue,
                                FontSize = 12,
                                TextDecorations = TextDecorations.Underline
                            };

                            var tapGesture = new TapGestureRecognizer();
                            tapGesture.Tapped += async (s, e) =>
                            {
                                try
                                {
                                    await Browser.Default.OpenAsync(url2, BrowserLaunchMode.SystemPreferred);
                                }
                                catch
                                {
                                    await Application.Current.MainPage.DisplayAlert("Error", "Unable to open link.", "OK");
                                }
                            };

                            linkSpan.GestureRecognizers.Add(tapGesture);
                            formattedText.Spans.Add(linkSpan);
                        }

                        noteLabel = new Label
                        {
                            FormattedText = formattedText,
                            LineBreakMode = LineBreakMode.WordWrap,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                    }
                    else
                    {
                        // Original fallback
                        noteLabel = new Label
                        {
                            Text = noteText,
                            LineBreakMode = LineBreakMode.WordWrap,
                            TextColor = Colors.Black,
                            FontSize = 12,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                    }
                }

                // add noteLabel once, outside the if/else
                contentLayout.Children.Add(noteLabel);

                // Images (unchanged)
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




    //private async void LoadLogs()
    //{
    //    try
    //    {
    //        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
    //        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0)
    //            return;

    //        LogDisplayArea.Children.Clear();

    //        var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetSiteLog?clientsiteId={clientSiteId}";
    //        var response = await _httpClient.GetAsync(url);

    //        if (!response.IsSuccessStatusCode)
    //        {
    //            await DisplayAlert("Error", "Failed to load site logs.", "OK");
    //            return;
    //        }

    //        var json = await response.Content.ReadAsStringAsync();
    //        var logs = JsonSerializer.Deserialize<List<GuardLogDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    //        if (logs == null || logs.Count == 0)
    //        {
    //            LogDisplayArea.Children.Add(new Label
    //            {
    //                Text = "No logs available for today.",
    //                TextColor = Colors.Gray,
    //                FontSize = 12
    //            });
    //            return;
    //        }

    //        foreach (var log in logs)
    //        {
    //            var contentLayout = new VerticalStackLayout
    //            {
    //                Spacing = 3,
    //                Children =
    //            {
    //                new Label
    //                {
    //                    FormattedText = new FormattedString
    //                    {
    //                        Spans =
    //                        {
    //                            new Span
    //                            {
    //                                Text = log.GuardInitials,
    //                                FontAttributes = FontAttributes.Bold,
    //                                TextColor = Colors.Teal,
    //                                FontSize = 13
    //                            },
    //                            new Span
    //                            {
    //                                Text = $"  {log.EventDateTimeLocal:HH:mm}",
    //                                FontSize = 11,
    //                                TextColor = Colors.Gray
    //                            }
    //                        }
    //                    },
    //                    Margin = new Thickness(0, 0, 0, 2)
    //                }
    //            }
    //            };

    //            // Add Note with optional hyperlink
    //            Label noteLabel;

    //            if (log.IrEntryType == true)
    //            {
    //                var formattedText = new FormattedString();

    //                var noteText = log.Notes?.Trim() ?? "";
    //                formattedText.Spans.Add(new Span
    //                {
    //                    Text = noteText + " ",
    //                    TextColor = Colors.Black,
    //                    FontSize = 12
    //                });

    //                string blobUrl = null;
    //                if (!string.IsNullOrWhiteSpace(noteText) && noteText.Length >= 8)
    //                {
    //                    var folder = new string(noteText.Take(8).ToArray());

    //                    // Ensure .pdf extension
    //                    var blobFileName = noteText.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
    //                        ? noteText
    //                        : noteText + ".pdf";

    //                    var encodedBlobName = Uri.EscapeDataString(blobFileName);
    //                    blobUrl = $"https://c4istorage1.blob.core.windows.net/irfiles/{folder}/{encodedBlobName}";
    //                }

    //                if (!string.IsNullOrWhiteSpace(blobUrl))
    //                {
    //                    var linkSpan = new Span
    //                    {
    //                        Text = "Click here",
    //                        TextColor = Colors.Blue,
    //                        FontSize = 12,
    //                        TextDecorations = TextDecorations.Underline
    //                    };

    //                    var tapGesture = new TapGestureRecognizer();
    //                    tapGesture.Tapped += async (s, e) =>
    //                    {
    //                        try
    //                        {
    //                            await Browser.Default.OpenAsync(blobUrl, BrowserLaunchMode.SystemPreferred);                                
    //                        }
    //                        catch
    //                        {
    //                            await Application.Current.MainPage.DisplayAlert("Error", "Unable to open link.", "OK");
    //                        }
    //                    };

    //                    linkSpan.GestureRecognizers.Add(tapGesture);
    //                    formattedText.Spans.Add(linkSpan);
    //                }

    //                noteLabel = new Label
    //                {
    //                    FormattedText = formattedText,
    //                    LineBreakMode = LineBreakMode.WordWrap,
    //                    Margin = new Thickness(0, 0, 0, 10)
    //                };
    //            }
    //            else
    //            {
    //                noteLabel = new Label
    //                {
    //                    Text = log.Notes ?? "",
    //                    LineBreakMode = LineBreakMode.WordWrap,
    //                    TextColor = Colors.Black,
    //                    FontSize = 12,
    //                    Margin = new Thickness(0, 0, 0, 10)
    //                };
    //            }

    //            contentLayout.Children.Add(noteLabel);

    //            // Add any image URLs
    //            foreach (var imageUrl in log.ImageUrls ?? Enumerable.Empty<string>())
    //            {
    //                if (!string.IsNullOrWhiteSpace(imageUrl))
    //                {
    //                    try
    //                    {
    //                        contentLayout.Children.Add(new Image
    //                        {
    //                            Source = ImageSource.FromUri(new Uri(imageUrl)),
    //                            HeightRequest = 130,
    //                            Margin = new Thickness(0, 4, 0, 4)
    //                        });
    //                    }
    //                    catch
    //                    {
    //                        contentLayout.Children.Add(new Label
    //                        {
    //                            Text = "(Image could not be loaded)",
    //                            TextColor = Colors.Red,
    //                            FontSize = 11
    //                        });
    //                    }
    //                }
    //            }

    //            var logCard = new Frame
    //            {
    //                CornerRadius = 8,
    //                Padding = 6,
    //                Margin = new Thickness(2, 4),
    //                BackgroundColor = Color.FromArgb("#F2F2F2"),
    //                Content = contentLayout
    //            };

    //            LogDisplayArea.Children.Add(logCard);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        await DisplayAlert("Error", $"An error occurred while loading logs: {ex.Message}", "OK");
    //    }
    //}



    //private async void LoadLogs()
    //{
    //    try
    //    {
    //        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
    //        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0)
    //            return;

    //        LogDisplayArea.Children.Clear();

    //        var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetSiteLog?clientsiteId={clientSiteId}";
    //        var response = await _httpClient.GetAsync(url);

    //        if (!response.IsSuccessStatusCode)
    //        {
    //            await DisplayAlert("Error", "Failed to load site logs.", "OK");
    //            return;
    //        }

    //        var json = await response.Content.ReadAsStringAsync();
    //        var logs = JsonSerializer.Deserialize<List<GuardLogDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    //        if (logs == null || logs.Count == 0)
    //        {
    //            LogDisplayArea.Children.Add(new Label
    //            {
    //                Text = "No logs available for today.",
    //                TextColor = Colors.Gray,
    //                FontSize = 12
    //            });
    //            return;
    //        }

    //        foreach (var log in logs)
    //        {
    //            var contentLayout = new VerticalStackLayout
    //            {
    //                Spacing = 3,
    //                Children =
    //            {
    //            new Label
    //    {
    //        FormattedText = new FormattedString
    //        {
    //            Spans =
    //            {
    //                new Span
    //                {
    //                    Text = log.GuardInitials,
    //                    FontAttributes = FontAttributes.Bold,
    //                    TextColor = Colors.Teal,
    //                    FontSize = 13
    //                },
    //                new Span
    //                {
    //                    Text = $"  {log.EventDateTimeLocal:HH:mm}",
    //                    FontSize = 11,
    //                    TextColor = Colors.Gray
    //                }
    //            }
    //        },
    //        Margin = new Thickness(0, 0, 0, 2)
    //    },
    //    new Label
    //    {
    //        Text = log.Notes ?? "",
    //        LineBreakMode = LineBreakMode.WordWrap,
    //        TextColor = Colors.Black,
    //        FontSize = 12,
    //        Margin = new Thickness(0, 0, 0, 10)
    //    }

    //            }
    //            };

    //            foreach (var imageUrl in log.ImageUrls ?? Enumerable.Empty<string>())
    //            {
    //                if (!string.IsNullOrWhiteSpace(imageUrl))
    //                {
    //                    try
    //                    {
    //                        contentLayout.Children.Add(new Image
    //                        {
    //                            Source = ImageSource.FromUri(new Uri(imageUrl)),
    //                            HeightRequest = 130,
    //                            Margin = new Thickness(0, 4, 0, 4)
    //                        });
    //                    }
    //                    catch
    //                    {
    //                        contentLayout.Children.Add(new Label
    //                        {
    //                            Text = "(Image could not be loaded)",
    //                            TextColor = Colors.Red,
    //                            FontSize = 11
    //                        });
    //                    }
    //                }
    //            }

    //            var logCard = new Frame
    //            {
    //                CornerRadius = 8,
    //                Padding = 6,
    //                Margin = new Thickness(2, 4),
    //                BackgroundColor = Color.FromArgb("#F2F2F2"),
    //                Content = contentLayout
    //            };

    //            LogDisplayArea.Children.Add(logCard);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        await DisplayAlert("Error", $"An error occurred while loading logs: {ex.Message}", "OK");
    //    }
    //}




    private async Task LogActivityTask(string activityDescription)
    {
        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();

        string gpsCoordinates = await SecureStorage.GetAsync("GpsCoordinates");

        if (string.IsNullOrWhiteSpace(gpsCoordinates))
        {
            await DisplayAlert("Location Error", "GPS coordinates not available. Please ensure location services are enabled.", "OK");
            return;
        }


        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return;
        var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/PostActivity" +
             $"?guardId={guardId}" +
             $"&clientsiteId={clientSiteId}" +
             $"&userId={userId}" +
             $"&activityString={Uri.EscapeDataString(activityDescription)}" +
             $"&gps={Uri.EscapeDataString(gpsCoordinates)}";


        try
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    await ShowToastMessage("Log entry added successfully.");
                    _delayCancellationTokenSource = new CancellationTokenSource();
                    //CustomLogEntry.Text = string.Empty; // Clear entry after success

                    await Task.Delay(2000, _delayCancellationTokenSource.Token); // Wait for 2 seconds
                    var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
                    Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
                    //Application.Current.MainPage = new NavigationPage(new MainPage());
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    await ShowToastMessage($"Failed: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                //await ShowToastMessage($"Error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            //await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async void OnAddLogEntryClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CustomLogEntry.Text))
        {
            await DisplayAlert("Error", "Log entry cannot be empty", "OK");
            return;
        }

        string gpsCoordinates = await SecureStorage.GetAsync("GpsCoordinates");
        if (string.IsNullOrWhiteSpace(gpsCoordinates))
        {
            await DisplayAlert("Location Error", "GPS coordinates not available. Please ensure location services are enabled.", "OK");
            return;
        }

        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return;
        var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/PostActivity" +
      $"?guardId={guardId}" +
      $"&clientsiteId={clientSiteId}" +
      $"&userId={userId}" +
      $"&activityString={Uri.EscapeDataString(CustomLogEntry.Text.Trim())}" +
      $"&gps={Uri.EscapeDataString(gpsCoordinates)}";
        
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                await ShowToastMessage("Log entry added successfully.");
                CustomLogEntry.Text = string.Empty;

                // Cancel any previous delay token
                _delayCancellationTokenSource?.Cancel();
                _delayCancellationTokenSource = new CancellationTokenSource();

                try
                {
                    await Task.Delay(2000, _delayCancellationTokenSource.Token);

                    // Only navigate if user hasn't typed anything in those 2 seconds
                    var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
                    Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
                }
                catch (TaskCanceledException)
                {
                    // Navigation canceled due to user typing
                }
            }
            else
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                await ShowToastMessage($"Failed: {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            //await ShowToastMessage($"Error: {ex.Message}");
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
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        //Application.Current.MainPage = new NavigationPage(new MainPage());
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle back button logic here
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        //Application.Current.MainPage = new NavigationPage(new MainPage());

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

    private void CustomLogEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CustomLogEntry.Text))
        {
            // If there's any text in the textbox, cancel the navigation
            _delayCancellationTokenSource?.Cancel();
        }
    }

    private bool AreLogsEqual(List<GuardLogDto> newLogs, List<GuardLogDto> oldLogs)
    {
        if (newLogs == null || oldLogs == null)
            return false;

        if (newLogs.Count != oldLogs.Count)
            return false;

        for (int i = 0; i < newLogs.Count; i++)
        {
            if (newLogs[i].Id != oldLogs[i].Id || newLogs[i].Notes != oldLogs[i].Notes)
                return false;
        }

        return true;
    }


    private void ShowPopup()
    {
        PopupOverlay.IsVisible = true;
    }

   

    private void OnCameraClicked(object sender, EventArgs e)
    {
        // Show the popup when camera button is clicked
        ShowPopup();
    }





    private void OnOpenPopupClicked(object sender, EventArgs e)
    {
        PopupOverlay.IsVisible = true;
    }



    private void OnCheckboxCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!(sender is CheckBox changedCheckBox))
            return;

        // If this checkbox is checked
        if (e.Value)
        {
            // Uncheck the other checkbox
            if (changedCheckBox == chkRearFullPage)
                chkWithinField.IsChecked = false;
            else if (changedCheckBox == chkWithinField)
                chkRearFullPage.IsChecked = false;
        }
        else
        {
            // If both are unchecked, default to WithinField
            if (!chkRearFullPage.IsChecked && !chkWithinField.IsChecked)
                chkWithinField.IsChecked = true;
        }
    }
    private async void OnPickFileClicked(object sender, EventArgs e)
    {
        try
        {
            var results = await FilePicker.PickMultipleAsync(); // Multiple files
            if (results != null && results.Any())
            {
                // Allowed file extensions
                string[] allowedExtensions = { ".jpg", ".jpeg", ".bmp", ".gif", ".heic", ".png" };

                // Determine the file type based on the checkboxes
                string fileType = chkRearFullPage.IsChecked ? "rear" : "twentyfive";

                foreach (var file in results)
                {
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                    {
                        await DisplayAlert("Invalid File", $"File '{file.FileName}' is not a supported image type.", "OK");
                        continue; // Skip this file
                    }

                    SelectedFiles.Add(new MyFileModel
                    {
                        File = file,
                        FileType = fileType
                    });
                }
            }

            // Show the file list only if it has items
            FilesCollection.IsVisible = SelectedFiles.Any();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"File picking failed: {ex.Message}", "OK");
        }
    }




    private async void OnDownloadFileClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is FileResult file)
        {
            await DisplayAlert("Download", $"Simulating download of {file.FileName}", "OK");
            // TODO: Add your actual download logic
        }
    }

    private void OnDeleteFileClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is MyFileModel file)
        {
            var files = (ObservableCollection<MyFileModel>)FilesCollection.ItemsSource;
            files.Remove(file);

            // Hide the list if no files remain
            FilesCollection.IsVisible = files.Any();
        }
    }

    //    private async void OnSaveAndCloseClicked(object sender, EventArgs e)
    //    {
    //        try
    //        {
    //            using var client = new HttpClient();

    //            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
    //            if (guardId <= 0 || clientSiteId <= 0 || userId <= 0)
    //            {
    //                await DisplayAlert("Error", "Invalid session. Please login again.", "OK");
    //                return;
    //            }

    //            //  Build ONE multipart content for all files
    //            var content = new MultipartFormDataContent
    //        {
    //            { new StringContent("rear"), "type" }, // or "twentyfive"
    //            { new StringContent(guardId.ToString()), "guardId" },
    //            { new StringContent(AppConfig.ApiBaseUrl), "url" }
    //        };

    //            //Add all files into the same request
    //            foreach (var file in SelectedFiles)
    //            {
    //                if (file == null)
    //                    continue;

    //                var stream = await file.OpenReadAsync();
    //                var fileContent = new StreamContent(stream);
    //                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

    //                content.Add(fileContent, "files", file.FileName);
    //                // "files" should match your backend handler's expected parameter name
    //            }

    //            //Send one request with all files
    //            var uploadResponse = await client.PostAsync(
    //    $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/UploadMultiple",
    //    content
    //);

    //            if (!uploadResponse.IsSuccessStatusCode)
    //            {
    //                await DisplayAlert("Error", "One or more files failed to upload.", "OK");
    //            }
    //            else
    //            {
    //                // Clear files only after successful upload
    //                SelectedFiles.Clear();
    //                await DisplayAlert("Success", "Activity saved and all files uploaded successfully.", "OK");
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            await DisplayAlert("Error", $"Save & Close failed: {ex.Message}", "OK");
    //        }
    //    }


    private async void OnSaveAndCloseClicked(object sender, EventArgs e)
    {
        try
        {
            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
            string gpsCoordinates = await SecureStorage.GetAsync("GpsCoordinates");

            using var client = new HttpClient();
            var content = new MultipartFormDataContent();

            // Add files + types (same index order)
            foreach (var fileModel in SelectedFiles)
            {
                var stream = await fileModel.File.OpenReadAsync();
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                // Add file
                content.Add(fileContent, "files", fileModel.File.FileName);

                // Add matching type (rear / twentyfive / etc.)
                content.Add(new StringContent(fileModel.FileType), "types");
            }

            // Add other form data
            content.Add(new StringContent(guardId.ToString()), "guardId");
            content.Add(new StringContent(clientSiteId.ToString()), "clientsiteId");
            content.Add(new StringContent(userId.ToString()), "userId");
            content.Add(new StringContent(gpsCoordinates ?? ""), "gps");

            // Send request
            var uploadResponse = await client.PostAsync(
                $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/UploadMultiple",
                content
            );

            if (!uploadResponse.IsSuccessStatusCode)
            {
                await DisplayAlert("Error", "One or more files failed to upload.", "OK");
            }
            else
            {
                SelectedFiles.Clear();
                await DisplayAlert("Success", "All files uploaded successfully.", "OK");

                // Small delay for smoother UI transition
                await Task.Delay(300);

                // Hide popup
                PopupOverlay.IsVisible = false;

                // Reset checkboxes to default values
                chkRearFullPage.IsChecked = false;
                chkWithinField.IsChecked = true;

                // Clear the file list
                if (FilesCollection.ItemsSource is ObservableCollection<MyFileModel> files)
                {
                    files.Clear();
                }

                // Hide the file list control
                FilesCollection.IsVisible = false;

            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Save & Close failed: {ex.Message}", "OK");
        }
    }





    private void OnClosePopupClicked(object sender, EventArgs e)
    {
        // Hide the popup
        PopupOverlay.IsVisible = false;

        // Reset checkboxes to default values
        chkRearFullPage.IsChecked = false;
        chkWithinField.IsChecked = true;

        // Clear the file list
        if (FilesCollection.ItemsSource is ObservableCollection<MyFileModel> files)
        {
            files.Clear();
        }

        // Hide the file list
        FilesCollection.IsVisible = false;
    }

}


public class MyFileModel
{
    public FileResult File { get; set; }
    public string FileType { get; set; }  // rear / twentyfive / etc
    public string FileName => File?.FileName; // <-- useful for UI binding
}
public class ActivityModel
{
    public int Id { get; set; }
    public string Name { get; set; }

    public string Label { get; set; }
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
    public bool? IrEntryType { get; set; }
}
