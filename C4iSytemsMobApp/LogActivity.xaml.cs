using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Plugin.NFC;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;


namespace C4iSytemsMobApp;

public partial class LogActivity : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private HubConnection _hubConnection;
    private HubConnection _hubConnectionRC;
    private int? _clientSiteId;
    private int? _userId;
    private int? _guardId;
    private int _badgeNo = 0;
    private bool _isLogsLoading;
    private readonly ILogBookServices _logBookServices;
    private GuardLogDto _selectedLogForEdit;
    public ObservableCollection<MyFileModel> SelectedFiles { get; set; }
     = new ObservableCollection<MyFileModel>();

    public const string ALERT_TITLE = "NFC";
    bool _eventsAlreadySubscribed = false;
    private readonly IScannerControlServices _scannerControlServices;
    private bool _isNfcEnabledForSite = false;
    bool _isDeviceiOS = false;
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
    private GuardLogDto SelectedLogForPush { get; set; }
    public LogActivity()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        LoadSecureData();
        LoadActivities();
        FilesCollection.ItemsSource = SelectedFiles;

        _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
        _scannerControlServices = IPlatformApplication.Current.Services.GetService<IScannerControlServices>();
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await StartNFC();
        _isLogsLoading = false;
        await SetupHubConnection(); // LoadLogs(); is Called when the SignalRHub connection is established
        await SetupRCHubConnection();
    }



    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isNfcEnabledForSite && CrossNFC.IsSupported && CrossNFC.Current.IsAvailable)
        {
            await StopListening();
        }
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            _hubConnection.StopAsync();
            _hubConnection.DisposeAsync();
        }
        if (_hubConnectionRC != null && _hubConnectionRC.State == HubConnectionState.Connected)
        {
            _hubConnectionRC.StopAsync();
            _hubConnectionRC.DisposeAsync();
        }
        _isLogsLoading = false;
    }

    private async void LoadActivities()
    {
        try
        {
            //ButtonContainer.Children.Clear(); // Clear previous items if reloading
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetActivities?type=2&siteid={_clientSiteId}";

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
            ActivitiesCollection.ItemsSource = sortedActivities;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async void OnActivityClicked(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            string activityName = button.Text;
            //await MainThread.InvokeOnMainThreadAsync(() => LogActivityTask(activityName, 0, "NA"));
            await LogActivityTask(activityName, 0, "NA");
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

        if (_isLogsLoading)
            return;

        _isLogsLoading = true;

        try
        {
            if (_guardId <= 0 || _clientSiteId <= 0 || _userId <= 0)
                return;

            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetSiteLog?clientsiteId={_clientSiteId}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Error", "Failed to load site logs.", "OK");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var logs = JsonSerializer.Deserialize<List<GuardLogDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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

            var bgColorPaleYellow = Color.FromArgb("#fcf8d1");
            var bgColorPaleRed = Color.FromArgb("#ffcccc");
            var bgColorNormal = Color.FromArgb("#F2F2F2"); // default

            LogDisplayArea.Children.Clear(); // Refresh UI

            foreach (var log in logs) // 
            {
                bool isAlarm = false;
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
                if (log.IrEntryType == 1)
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
                    if (!string.IsNullOrWhiteSpace(noteText) && noteText.Length >= 8 && noteText.Contains("IR Report", StringComparison.OrdinalIgnoreCase))
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
                        var noteText2 = (log.Notes ?? "").Replace("<br>", "\n").Replace("<br/>", "\n");
                        var formattedText = new FormattedString();

                        // Regex to find all anchor tags
                        var regex = new System.Text.RegularExpressions.Regex(
                            "(?<textBefore>[^<]*)<a\\s+href=\"(?<url>[^\"]+)\">(?<text>[^<]+)</a>",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        var matches = regex.Matches(noteText2);

                        if (matches.Count > 0)
                        {
                            int lastIndex = 0;
                            foreach (System.Text.RegularExpressions.Match match in matches)
                            {
                                // Add text before the link
                                var textBefore = match.Groups["textBefore"].Value;
                                if (!string.IsNullOrEmpty(textBefore))
                                {
                                    formattedText.Spans.Add(new Span
                                    {
                                        Text = textBefore,
                                        TextColor = Colors.Black,
                                        FontSize = 12
                                    });
                                }

                                // Add clickable link
                                var url2 = match.Groups["url"].Value;
                                var linkText = match.Groups["text"].Value;

                                var linkSpan = new Span
                                {
                                    Text = linkText,
                                    TextColor = Colors.Blue,
                                    TextDecorations = TextDecorations.Underline,
                                    FontSize = 12
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

                                // Move pointer
                                lastIndex = match.Index + match.Length;
                            }

                            // Add any text remaining after last link
                            if (lastIndex < noteText2.Length)
                            {
                                formattedText.Spans.Add(new Span
                                {
                                    Text = noteText2.Substring(lastIndex),
                                    TextColor = Colors.Black,
                                    FontSize = 12
                                });
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
                            // No links ? normal label
                            noteLabel = new Label
                            {
                                Text = noteText2,
                                LineBreakMode = LineBreakMode.WordWrap,
                                TextColor = Colors.Black,
                                FontSize = 12,
                                Margin = new Thickness(0, 0, 0, 10)
                            };
                        }
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

                Color cardBgColor = bgColorNormal;
                if (log.IrEntryType == 2)
                {
                    cardBgColor = bgColorPaleRed;
                    isAlarm = true;
                }
                else if (log.IrEntryType == 1)
                    cardBgColor = bgColorPaleYellow;

                // Create a Grid to hold content + optional button
                var cardGrid = new Grid
                {
                    ColumnDefinitions =
    {
        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // main content
        new ColumnDefinition { Width = GridLength.Auto } // button
    }
                };

                // Add existing contentLayout to the first column
                cardGrid.Add(contentLayout, 0, 0);

                // Add button only if IrEntryType == 2
                if (isAlarm)
                {
                    var actionButton = new ImageButton
                    {
                        Source = "envelope.png", // replace with your image filename in Resources/Images
                        BackgroundColor = Colors.Transparent,
                        HeightRequest = 30,
                        WidthRequest = 30,
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Start,
                        CornerRadius = 6,
                        Padding = 2
                    };

                    actionButton.Clicked += (s, e) =>
                    {
                        SelectedLogForPush = log; // store the selected log
                        ShowPushNotificationsPopup();
                        // Optional: Display alert for testing
                        // await Application.Current.MainPage.DisplayAlert("Alarm", $"Action triggered for {log.GuardInitials}", "OK");
                    };


                    cardGrid.Add(actionButton, 1, 0);
                }


                bool hasImages = (log.ImageUrls != null && log.ImageUrls.Any()) ||
                 (log.RearFileUrls != null && log.RearFileUrls.Any());

                bool notesContainUploadText = log.Notes?.Contains(
                    "Mob app image upload",
                    StringComparison.OrdinalIgnoreCase) ?? false;

                if (log.GuardId.HasValue
      && log.GuardId.Value == _guardId
      && log.IrEntryType != 1
      && log.IsSystemEntry == false
      //&& (log.ImageUrls == null || log.ImageUrls.Count == 0)
      //&& !(log.Notes?.Contains("Mob app image upload", StringComparison.OrdinalIgnoreCase) ?? false)


      )
                {
                    var editButton = new ImageButton
                    {
                        Source = "edit.png", // a pencil or edit icon in Resources/Images
                        BackgroundColor = Colors.Transparent,
                        HeightRequest = 30,
                        WidthRequest = 30,
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Start,
                        CornerRadius = 6,
                        Padding = 2
                    };




                    editButton.Clicked += async (s, e) =>
                    {
                        // Decide which popup to show based on images
                        if ((hasImages || notesContainUploadText))
                            ShowEditLogImagePopup(log);
                        else
                            ShowEditLogPopup(log);
                    };


                    // Optionally combine with alarm button
                    cardGrid.Add(editButton, 1, 0);
                }

                var logCard = new Frame
                {
                    CornerRadius = 8,
                    Padding = 6,
                    Margin = new Thickness(2, 4),
                    BackgroundColor = cardBgColor,
                    Content = cardGrid
                };

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LogDisplayArea.Children.Add(logCard);
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred while loading logs: {ex.Message}.Please try again", "OK");
        }
        finally
        {
            _isLogsLoading = false;
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
                        FileType = fileType,
                        IsNew = true // Mark as new file
                    });
                }
            }

            // Show the file list only if it has items
            FilesCollection.IsVisible = SelectedFiles.Any();

            ShowPopup();





            //var results = await FilePicker.PickMultipleAsync();
            //if (results != null && results.Any())
            //{
            //    string[] allowedExtensions = { ".jpg", ".jpeg", ".bmp", ".gif", ".heic", ".png" };

            //    foreach (var file in results)
            //    {
            //        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            //        if (!allowedExtensions.Contains(extension))
            //        {
            //            await DisplayAlert("Invalid File",
            //                $"File '{file.FileName}' is not a supported image type.", "OK");
            //            continue;
            //        }

            //        // Default type is twentyfive unless user ticks "rear full page"
            //        string fileType = "twentyfive";

            //        SelectedFiles.Add(new MyFileModel
            //        {
            //            File = file,
            //            FileType = fileType
            //        });
            //    }

            //    if (SelectedFiles.Any())
            //    {
            //        await UploadFileToApiAsync();
            //    }
            //    else
            //    {
            //        await DisplayAlert("Notice", "No valid files selected.", "OK");
            //    }
            //}
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"File picking failed: {ex.Message}", "OK");
        }
    }

    private async Task UploadFileToApiAsync()
    {
        try
        {
            string gpsCoordinates = Preferences.Get("GpsCoordinates", "");

            using var client = new HttpClient();
            var content = new MultipartFormDataContent();

            // Add files + types (same index order)
            foreach (var fileModel in SelectedFiles)
            {
                var stream = await fileModel.File.OpenReadAsync();
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                // File
                content.Add(fileContent, "files", fileModel.File.FileName);

                // Type (rear / twentyfive)
                content.Add(new StringContent(fileModel.FileType), "types");
            }

            // Add other form data
            content.Add(new StringContent(_guardId.ToString()), "guardId");
            content.Add(new StringContent(_clientSiteId.ToString()), "clientsiteId");
            content.Add(new StringContent(_userId.ToString()), "userId");
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




            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Save & Close failed: {ex.Message}", "OK");
        }
    }


    // Show popup and preload existing note
    private void ShowEditLogImagePopup(GuardLogDto log)
    {
        _selectedLogForEdit = log;
        EditLogPopupEntry.Text = log.Notes;

        SelectedFiles.Clear();

        // Load 25% images
        if (log.ImageUrls != null && log.ImageUrls.Any())
        {
            foreach (var imageUrl in log.ImageUrls)
            {
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    var fileName = Path.GetFileName(imageUrl);

                    SelectedFiles.Add(new MyFileModel
                    {
                        File = new FileResult(fileName),
                        FileType = "twentyfive", // 25% file
                        IsNew = false,
                        LogBookId = log.Id,
                    });
                }
            }
        }

        // Load rear/full-page images
        if (log.RearFileUrls != null && log.RearFileUrls.Any())
        {
            foreach (var imageUrl in log.RearFileUrls)
            {
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    var fileName = Path.GetFileName(imageUrl);

                    SelectedFiles.Add(new MyFileModel
                    {
                        File = new FileResult(fileName),
                        FileType = "rear", // rear/full-page file
                        IsNew = false,
                        LogBookId = log.Id,
                    });
                }
            }
        }

        // Bind to CollectionView
        FilesCollectionEditImage.ItemsSource = SelectedFiles;
        FilesCollectionEditImage.IsVisible = SelectedFiles.Any();
        PopupOverlayEditImage.IsVisible = true;

    }


    private void ShowEditLogPopup(GuardLogDto log)
    {
        _selectedLogForEdit = log;
        EditLogPopupEntry.Text = log.Notes;


        EditLogPopupOverlay.IsVisible = true;

    }


    // Hide popup without saving
    private void OnEditLogCancelClicked(object sender, EventArgs e)
    {
        EditLogPopupOverlay.IsVisible = false;
        _selectedLogForEdit = null;
    }

    // Save updated note
    private async void OnEditLogSaveClicked(object sender, EventArgs e)
    {
        if (_selectedLogForEdit == null)
            return;

        string newNotes = EditLogPopupEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(newNotes))
        {
            await Application.Current.MainPage.DisplayAlert("Validation", "Please enter a note.", "OK");
            return;
        }

        try
        {
            var updateRequest = new
            {
                Id = _selectedLogForEdit.Id,
                GuardId = _selectedLogForEdit.GuardId,
                Notes = newNotes
            };


            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/UpdateGuardLogNotes" +
             $"?id={_selectedLogForEdit.Id}" +
             $"&notes={Uri.EscapeDataString(newNotes.Trim())}";

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    await ShowToastMessage("Log updated successfully.");
                    // LoadLogs();

                    EditLogPopupOverlay.IsVisible = false;

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
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to update: {ex.Message}", "OK");
        }
    }
    private void chkPushAcknowledge_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (chkPushAcknowledge.IsChecked)
        {
            // Uncheck the other
            chkPushMessageBack.IsChecked = false;

            // Disable & clear text box
            txtPushMessage.IsEnabled = false;
            txtPushMessage.Text = string.Empty;
        }
    }


    private void chkPushMessageBack_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (chkPushMessageBack.IsChecked)
        {
            // Uncheck the other
            chkPushAcknowledge.IsChecked = false;

            // Enable text box
            txtPushMessage.IsEnabled = true;
        }
        else
        {
            // If unchecked, also clear/disable the text box
            txtPushMessage.IsEnabled = false;
            txtPushMessage.Text = string.Empty;
        }
    }


    private void ShowPushNotificationsPopup()
    {
        PopupOverlayPushNotifications.IsVisible = true;
    }

    private void OnPushClosePopupClicked(object sender, EventArgs e)
    {
        PopupOverlayPushNotifications.IsVisible = false;
    }

    private async void OnPushSendClicked(object sender, EventArgs e)
    {

        if (SelectedLogForPush == null)
        {
            await DisplayAlert("Error", "No log selected for notification.", "OK");
            return;
        }

        if (_guardId == null || _clientSiteId == null || _userId == null || _guardId <= 0 || _clientSiteId <= 0 || _userId <= 0) return;


        // Build the message including the selected log details
        string logInfo = $" {SelectedLogForPush.Notes}";


        string messageToSend;

        // Check which option is selected
        if (chkPushAcknowledge.IsChecked)
        {
            messageToSend = $"{logInfo}\n--ACKNOWLEDGED";
        }
        else if (chkPushMessageBack.IsChecked)
        {
            messageToSend = $"{logInfo}\n--{txtPushMessage.Text}";
        }
        else
        {
            // fallback, in case neither is selected
            messageToSend = logInfo;
        }

        long rcPushMessageId = SelectedLogForPush?.RcPushMessageId ?? 0;

        var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SavePushNotificationTestMessage" +
                     $"?guardId={_guardId}" +
                     $"&clientsiteId={_clientSiteId}" +
                     $"&userId={_userId}" +
                     $"&notifications={Uri.EscapeDataString(messageToSend)}" +
                     $"&rcPushMessageId={rcPushMessageId}";

        try
        {
            // Use POST (because API is [HttpPost])
            HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, null);

            if (response.IsSuccessStatusCode)
            {
                await ShowToastMessage("Notification sent successfully.");
                txtPushMessage.Text = string.Empty;
                PopupOverlayPushNotifications.IsVisible = false;
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


    private async Task LogActivityTask(string activityDescription, int scanningType = 0, string _taguid = "NA", bool IsSystemEntry = true)
    {

        var (isSuccess, msg) = await _logBookServices.LogActivityTask(activityDescription, scanningType, _taguid);        
        if (isSuccess)
        {
            if (scanningType == (int)ScanningType.NFC)
                await ShowToastMessage($"{ALERT_TITLE}\n{msg}");
            else if (scanningType == (int)ScanningType.BLUETOOTH)
                await ShowToastMessage($"BLE\n{msg}");
            else
                await ShowToastMessage(msg);


            HideCustomLogPopup();

            var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
            Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        }
        else
        {
            if (scanningType == (int)ScanningType.NFC)
                await ShowToastMessage($"{ALERT_TITLE}\n{msg}");
            else if (scanningType == (int)ScanningType.BLUETOOTH)
                await ShowToastMessage($"BLE\n{msg}");
            else
                await ShowToastMessage(msg);
        }
    }

    private async void OnAddLogEntryNewClicked(object sender, EventArgs e)
    {
        ShowCustomLogPopup();
    }

    private void ShowCustomLogPopup()
    {
        CustomLogPopupEntry.Text = string.Empty; // Ensure fresh input
        CustomLogPopupOverlay.IsVisible = true;
    }

    private void HideCustomLogPopup()
    {
        CustomLogPopupOverlay.IsVisible = false;
    }

    private void OnCustomLogCancelClicked(object sender, EventArgs e)
    {
        HideCustomLogPopup();
    }

    private async void OnCustomLogSaveClicked(object sender, EventArgs e)
    {
        if (CustomLogPopupEntry == null)
        {
            await DisplayAlert("Error", "Text box not available. Please reopen the popup.", "OK");
            return;
        }

        var text = CustomLogPopupEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            await DisplayAlert("Error", "Please enter a custom log!", "OK");
            return;
        }

        await SaveCustomLog(text);
    }

    private async Task SaveCustomLog(string log)
    {
        if (string.IsNullOrWhiteSpace(log))
        {
            await DisplayAlert("Error", "Log entry cannot be empty", "OK");
            return;
        }

        string gpsCoordinates = Preferences.Get("GpsCoordinates", "");
        if (string.IsNullOrWhiteSpace(gpsCoordinates))
        {
            await DisplayAlert("Location Error", "GPS coordinates not available. Please ensure location services are enabled.", "OK");
            return;
        }





        //if (_guardId == null || _clientSiteId == null || _userId == null || _guardId <= 0 || _clientSiteId <= 0 || _userId <= 0) return;

        //var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/PostActivity" +
        //$"?guardId={_guardId}" +
        //$"&clientsiteId={_clientSiteId}" +
        //$"&userId={_userId}" +
        //$"&activityString={Uri.EscapeDataString(log.Trim())}" +
        //$"&gps={Uri.EscapeDataString(gpsCoordinates)}" +
        //$"&systemEntry=false";

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => LogActivityTask(log.Trim(), 0, "NA", false));

            //HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
            //if (response.IsSuccessStatusCode)
            //{
            //    await ShowToastMessage("Log entry added successfully.");

            //    // Close Popup
            //    HideCustomLogPopup();


            //}
            //else
            //{
            //    string errorMessage = await response.Content.ReadAsStringAsync();
            //    await ShowToastMessage($"Failed: {errorMessage}");
            //}
        }
        catch (Exception ex)
        {
            // Log silently or toast error
            // await ShowToastMessage($"Error: {ex.Message}");
        }
    }

    private async Task<(int guardId, int clientSiteId, int userId)> GetSecureStorageValues()
    {
        int.TryParse(Preferences.Get("GuardId", ""), out int guardId);
        int.TryParse(Preferences.Get("SelectedClientSiteId", ""), out int clientSiteId);
        int.TryParse(Preferences.Get("UserId", ""), out int userId);

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
        if (_isNfcEnabledForSite && CrossNFC.IsSupported && CrossNFC.Current.IsAvailable)
        {
            Task.Run(async () => await StopListening());
        }

        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        //Application.Current.MainPage = new NavigationPage(new MainPage());
    }

    protected override bool OnBackButtonPressed()
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

        if (_hubConnectionRC != null && _hubConnectionRC.State == HubConnectionState.Connected)
        {
            _hubConnectionRC.StopAsync();
            _hubConnectionRC.DisposeAsync();
        }

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
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Toast.Make(message, ToastDuration.Long).Show();
        });
    }
       
    private void CustomLogEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        //if (!string.IsNullOrWhiteSpace(CustomLogEntry.Text))
        //{
        //    // If there's any text in the textbox, cancel the navigation
        //    _delayCancellationTokenSource?.Cancel();
        //}
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
    // edit image checkboxes
    private void OnEditCheckboxCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!(sender is CheckBox changedCheckBox))
            return;

        // If this checkbox is checked
        if (e.Value)
        {
            // Uncheck the other checkbox
            if (changedCheckBox == chkEditRearFullPage)
                chkEditWithinField.IsChecked = false;
            else if (changedCheckBox == chkEditWithinField)
                chkEditRearFullPage.IsChecked = false;
        }
        else
        {
            // If both are unchecked, default to WithinField
            if (!chkEditRearFullPage.IsChecked && !chkEditWithinField.IsChecked)
                chkEditWithinField.IsChecked = true;
        }
    }

    //private async void OnPickFileClicked(object sender, EventArgs e)
    //{
    //    try
    //    {
    //        var results = await FilePicker.PickMultipleAsync(); // Multiple files
    //        if (results != null && results.Any())
    //        {
    //            // Allowed file extensions
    //            string[] allowedExtensions = { ".jpg", ".jpeg", ".bmp", ".gif", ".heic", ".png" };

    //            // Determine the file type based on the checkboxes
    //            string fileType = chkRearFullPage.IsChecked ? "rear" : "twentyfive";

    //            foreach (var file in results)
    //            {
    //                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    //                if (!allowedExtensions.Contains(extension))
    //                {
    //                    await DisplayAlert("Invalid File", $"File '{file.FileName}' is not a supported image type.", "OK");
    //                    continue; // Skip this file
    //                }

    //                SelectedFiles.Add(new MyFileModel
    //                {
    //                    File = file,
    //                    FileType = fileType
    //                });
    //            }
    //        }

    //        // Show the file list only if it has items
    //        FilesCollection.IsVisible = SelectedFiles.Any();
    //    }
    //    catch (Exception ex)
    //    {
    //        await DisplayAlert("Error", $"File picking failed: {ex.Message}", "OK");
    //    }
    //}


    private async void OnEditPickFileClicked(object sender, EventArgs e)
    {
        try
        {
            var results = await FilePicker.PickMultipleAsync(); // Allow multiple file selection
            if (results != null && results.Any())
            {
                // Allowed image formats
                string[] allowedExtensions = { ".jpg", ".jpeg", ".bmp", ".gif", ".heic", ".png" };

                // Determine the file type from checkboxes
                string fileType = chkEditRearFullPage.IsChecked ? "rear" : "twentyfive";

                foreach (var file in results)
                {
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(extension))
                    {
                        await DisplayAlert("Invalid File", $"File '{file.FileName}' is not a supported image type.", "OK");
                        continue;
                    }

                    SelectedFiles.Add(new MyFileModel
                    {
                        File = file,
                        FileType = fileType,
                        IsNew = true,
                        LogBookId = _selectedLogForEdit.Id
                    });
                }
            }

            // Show the file list only if it has items
            FilesCollectionEditImage.IsVisible = SelectedFiles.Any();
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

    private async void OnEditDeleteFileClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is MyFileModel file)
        {


            var files = (ObservableCollection<MyFileModel>)FilesCollectionEditImage.ItemsSource;

            // If it's an existing file (already in DB)
            if (!file.IsNew)
            {
                try
                {

                    using var client = new HttpClient();
                    var content = new MultipartFormDataContent();
                    content.Add(new StringContent(file.LogBookId.ToString()), "logbookId");
                    content.Add(new StringContent(file.FileName), "fileName");

                    var response = await client.PostAsync($"{AppConfig.ApiBaseUrl}GuardSecurityNumber/DeleteFile", content);

                    if (response.IsSuccessStatusCode)
                    {
                        files.Remove(file);
                        await DisplayAlert("Deleted", "File deleted successfully.", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to delete file from database.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Delete failed: {ex.Message}", "OK");
                }
            }
            else
            {
                // It's a new file (not yet uploaded)
                files.Remove(file);
            }

            // Hide list if no items remain
            FilesCollectionEditImage.IsVisible = files.Any();
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
            if (_guardId == null || _clientSiteId == null || _userId == null || _guardId <= 0 || _clientSiteId <= 0 || _userId <= 0) return;
            string gpsCoordinates = Preferences.Get("GpsCoordinates", "");

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
            content.Add(new StringContent(_guardId.ToString()), "guardId");
            content.Add(new StringContent(_clientSiteId.ToString()), "clientsiteId");
            content.Add(new StringContent(_userId.ToString()), "userId");
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

    private async void OnEditSaveAndCloseClicked(object sender, EventArgs e)
    {
        try
        {
            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
            string gpsCoordinates = Preferences.Get("GpsCoordinates", "");

            using var client = new HttpClient();
            var content = new MultipartFormDataContent();

            if (_selectedLogForEdit == null)
            {
                await DisplayAlert("Error", "No log selected for editing.", "OK");
                return;
            }

            // Filter only new files to upload
            var newFiles = SelectedFiles.Where(f => f.IsNew).ToList();
            if (!newFiles.Any())
            {
                PopupOverlayEditImage.IsVisible = false;
                //await DisplayAlert("Info", "No new files to upload.", "OK");
                return;
            }

            // Add files + their types (same index order)
            foreach (var fileModel in newFiles)
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

            // Add form data
            content.Add(new StringContent(_selectedLogForEdit.Id.ToString()), "logbookId");


            // Send request
            var uploadResponse = await client.PostAsync(
                $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/UploadMultipleEdit",
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

                // Hide the Edit popup
                PopupOverlayEditImage.IsVisible = false;

                // Reset Edit checkboxes
                chkEditRearFullPage.IsChecked = false;
                chkEditWithinField.IsChecked = true;

                // Clear file list
                if (FilesCollectionEditImage.ItemsSource is ObservableCollection<MyFileModel> files)
                {
                    files.Clear();
                }

                // Hide file list control
                FilesCollectionEditImage.IsVisible = false;
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
    private async void LoadSecureData()
    {
        var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
        _clientSiteId = clientSiteId;
        _guardId = guardId;
        _userId = userId;
        string savedBadgeKeyName = $"{_clientSiteId}_{_guardId}_GuardSelectedBadgeNumber";
        string savedBadgeNumber = Preferences.Get(savedBadgeKeyName, "0");
        _badgeNo = int.TryParse(savedBadgeNumber, out int badgeNum) ? badgeNum : 0;
    }
    private async Task SetupHubConnection()
    {

        if (_clientSiteId == null) return;
        try
        {
            string hubUrl = $"{AppConfig.MobileSignalRBaseUrl}/MobileAppSignalRHub";
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            //_hubConnection.Closed += async (error) =>
            //{
            //    Debug.WriteLine($"Connection closed. Reason: {error?.Message}");
            //    // Optionally attempt reconnect
            //    await Task.Delay(3000);
            //    await _hubConnection.StartAsync();
            //};

            //_hubConnection.Reconnecting += error =>
            //{
            //    Debug.WriteLine($"Reconnecting due to: {error?.Message}");
            //    return Task.CompletedTask;
            //};


            _hubConnection.Reconnected += connectionId =>
            {
                Debug.WriteLine($"Reconnected with connectionId: {connectionId}");
                if (_hubConnection.State == HubConnectionState.Connected)
                {
                    MobileCrowdControlGuard JoinGaurd = new MobileCrowdControlGuard()
                    {
                        ClientSiteId = (int)_clientSiteId,
                        GuardId = (int)_guardId,
                        UserId = (int)_userId,
                        BadgeNo = _badgeNo,
                    };
                    var z = Task.FromResult(_hubConnection.InvokeAsync<string>("JoinGroup", JoinGaurd)).Result;
                    Console.WriteLine(z);

                    if (!string.IsNullOrEmpty(z.Result))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            LoadLogs();
                        });
                    }
                }
                return Task.CompletedTask;
            };

            _hubConnection.On("GuardLogChanged", () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadLogs();
                });
            });

            await _hubConnection.StartAsync();

            if (_hubConnection.State == HubConnectionState.Connected)
            {
                MobileCrowdControlGuard JoinGaurd = new MobileCrowdControlGuard()
                {
                    ClientSiteId = (int)_clientSiteId,
                    GuardId = (int)_guardId,
                    UserId = (int)_userId,
                    BadgeNo = _badgeNo,
                };
                var z = await _hubConnection.InvokeAsync<string>("JoinGroup", JoinGaurd);
                Console.WriteLine(z);

                if (!string.IsNullOrEmpty(z))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LoadLogs();
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SignalR connection: {ex.Message}");
        }
    }
    private async Task SetupRCHubConnection()
    {

        if (_clientSiteId == null) return;
        try
        {
            string hubUrl = $"{AppConfig.MobileSignalRRCBaseUrl}/MobileAppSignalRHub";
            _hubConnectionRC = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            //_hubConnectionRC.Closed += async (error) =>
            //{
            //    Debug.WriteLine($"RC Connection closed. Reason: {error?.Message}");
            //    // Optionally attempt reconnect
            //    await Task.Delay(3000);
            //    await _hubConnectionRC.StartAsync();
            //};

            //_hubConnectionRC.Reconnecting += error =>
            //{
            //    Debug.WriteLine($"RC Reconnecting due to: {error?.Message}");
            //    return Task.CompletedTask;
            //};


            _hubConnectionRC.Reconnected += connectionId =>
            {
                Debug.WriteLine($"RC Reconnected with connectionId: {connectionId}");
                if (_hubConnectionRC.State == HubConnectionState.Connected)
                {
                    MobileCrowdControlGuard JoinGaurd = new MobileCrowdControlGuard()
                    {
                        ClientSiteId = (int)_clientSiteId,
                        GuardId = (int)_guardId,
                        UserId = (int)_userId,
                        BadgeNo = _badgeNo,
                    };
                    var z = Task.FromResult(_hubConnectionRC.InvokeAsync<string>("JoinGroup", JoinGaurd)).Result;
                    Console.WriteLine(z);
                }
                return Task.CompletedTask;
            };

            _hubConnectionRC.On("GuardLogChanged", () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadLogs();
                });
            });

            await _hubConnectionRC.StartAsync();

            if (_hubConnectionRC.State == HubConnectionState.Connected)
            {
                MobileCrowdControlGuard JoinGaurd = new MobileCrowdControlGuard()
                {
                    ClientSiteId = (int)_clientSiteId,
                    GuardId = (int)_guardId,
                    UserId = (int)_userId,
                    BadgeNo = _badgeNo,
                };
                var z = await _hubConnectionRC.InvokeAsync<string>("JoinGroup", JoinGaurd);
                Console.WriteLine(z);
            }
        }
        catch (Exception ex)
        {

            Console.WriteLine($"Error in RC signalR connection: {ex.Message}");
        }

    }

    private void OnEditClosePopupClicked(object sender, EventArgs e)
    {
        // Hide the edit popup
        PopupOverlayEditImage.IsVisible = false;

        // Reset checkboxes to default values
        chkEditRearFullPage.IsChecked = false;
        chkEditWithinField.IsChecked = true;

        // Clear the edit file list
        if (FilesCollectionEditImage.ItemsSource is ObservableCollection<MyFileModel> files)
        {
            files.Clear();
        }

        // Hide the file list view
        FilesCollectionEditImage.IsVisible = false;
    }

    private async Task LogScannedDataToCache(string _TagUid, ScanningType _scannerType)
    {  
        await ShowToastMessage($"{ALERT_TITLE}\nNFC Tag scanned. Logging activity to Cache...");
        var (isSuccess, msg, _ChaceCount) = await _scannerControlServices.SaveScanDataToLocalCache(_TagUid, _scannerType, _clientSiteId.Value, _userId.Value, _guardId.Value);
        if (isSuccess)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SyncState.SyncedCount = _ChaceCount;
            });
            //await ShowToastMessage(msg);            
            await ShowToastMessage($"{ALERT_TITLE}\n{msg}");
        }
        else
        {
            await DisplayAlert($"{ALERT_TITLE} Error", msg ?? "Failed to save tag scan", "OK");
        }
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
                        await DisplayAlert(ALERT_TITLE, "NFC is disabled.", "OK");

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
        await DisplayAlert(ALERT_TITLE, $"NFC has been {(isEnabled ? "enabled" : "disabled")}.", "OK");
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
            if (_guardId == null || _clientSiteId == null || _userId == null || _guardId <= 0 || _clientSiteId <= 0 || _userId <= 0) return;

            if (!App.IsOnline)
            {
                // Log To Cache                    
                await LogScannedDataToCache(serialNumber, ScanningType.NFC);
                return;
            }

            
            var scannerSettings = await _scannerControlServices.FetchTagInfoDetailsAsync(_clientSiteId.ToString(), serialNumber, _guardId.ToString(), _userId.ToString(), ScanningType.NFC);
            if (scannerSettings != null)
            {
                if (scannerSettings.IsSuccess)
                {
                    var SnackbarMessage = scannerSettings.tagInfoLabel.Length > 35 ? scannerSettings.tagInfoLabel.Substring(0, 35) + "..." : scannerSettings.tagInfoLabel;
                    await ShowToastMessage($"{ALERT_TITLE}\nNFC Tag {SnackbarMessage} scanned.\nLogging activity...");

                    // Valid tag - log activity
                    int _scannerType = (int)ScanningType.NFC;
                    var _taguid = serialNumber;
                    if (!scannerSettings.tagFound) { _taguid = "NA"; }
                    LogActivityTask(scannerSettings.tagInfoLabel, _scannerType, _taguid);
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
}


public class MyFileModel
{
    public FileResult File { get; set; }
    public string FileType { get; set; }  // rear / twentyfive / etc
    public string FileName => File?.FileName; // <-- useful for UI binding

    public bool IsNew { get; set; } = true;  // true → newly added via picker

    public int? LogBookId { get; set; }  // null for new files, set for existing files from DB
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

    public DateTime? EventDateTime { get; set; }
    public string EventDateTimeLocal { get; set; }  // Changed to string
    public string EventDateTimeZoneShort { get; set; }

    public string Notes { get; set; }
    public string GuardInitials { get; set; }

    public int? IrEntryType { get; set; }
    public bool? IsSystemEntry { get; set; }
    public long? RcPushMessageId { get; set; }  // Changed to string

    public int? GuardId { get; set; }

    public List<string> ImageUrls { get; set; } = new List<string>();
    public List<string> RearFileUrls { get; set; } = new List<string>();
}


