using AutoMapper;
using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using System.Net.Http.Json;

namespace C4iSytemsMobApp;

public partial class MultiMedia : ContentPage
{
    public ObservableCollection<VideoFile> VideoFiles { get; set; } = new();
    private ObservableCollection<MyFileModel> SelectedFiles = new();
    private readonly IScanDataDbServices _scanDataDbService;
    private readonly IMapper _mapper;

    public MultiMedia()
    {
        InitializeComponent();
        BindingContext = this;
        _scanDataDbService = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
        _mapper = IPlatformApplication.Current.Services.GetService<IMapper>();
        LoadVideos();
    }

    private async void LoadVideos()
    {
        try
        {
            // Loading indicator code is commented out; uncomment it if needed.
            // LoadingIndicator.IsVisible = true;
            // LoadingIndicator.IsRunning = true;

            //using var client = new HttpClient();
            //var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetActivitiesAudio?type=3";
            //var videos = await client.GetFromJsonAsync<List<VideoFile>>(url);

            var _existingVideosFiles = await _scanDataDbService.GetMultimediaLocalList(3);
            var videos = _mapper.Map<List<VideoFile>>(_existingVideosFiles);

            if (videos != null)
            {
                foreach (var video in videos)
                {
                    VideoFiles.Add(video);
                }
            }
            else
            {
                await DisplayAlert("Error", "No videos found.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load videos: {ex.Message}", "OK");
        }
        finally
        {
            // Loading indicator code is commented out; uncomment it if needed.
            // LoadingIndicator.IsVisible = false;
            // LoadingIndicator.IsRunning = false;
        }
    }

    // Method to handle label tap (play video)
    private void OnLabelTapped(object sender, EventArgs e)
    {
        var tappedLabel = (Label)sender;
        var video = (VideoFile)tappedLabel.BindingContext;

        if (video != null)
        {
            // Set the MediaElement source to the selected video URL
            VideoPlayer.Source = video.Url;

            // Show the fullscreen player
            FullscreenPlayer.IsVisible = true;

            // Start playing the video
            VideoPlayer.Play();

            // Logged after Play() so a logbook or GPS problem cannot stop the guard watching.
            _ = LogPlayedFileAsync(video.Label, video.Url);
        }
    }

    private async void OnMediaEnded(object sender, EventArgs e)
    {
        //try
        //{
        //    var currentSource = VideoPlayer.Source;

        //    // Stop first (don't assign null!)
        //    VideoPlayer.Stop();

        //    await Task.Delay(100); // short delay before restart

        //    // Re-assign source directly to trigger reload
        //    VideoPlayer.Source = currentSource;

        //    // Start playback again
        //    VideoPlayer.Play();
        //}
        //catch (Exception ex)
        //{
        //    await DisplayAlert("Error", $"Video replay failed: {ex.Message}", "OK");
        //}
    }


    private void OnVideoButtonClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is VideoFile videoFile)
        {
            if (string.IsNullOrWhiteSpace(videoFile.Url) || !File.Exists(videoFile.Url))
            {
                Console.WriteLine($"Local video file {button.Text} not found.");
                DisplayAlert("Error", "File not found.", "OK");
                return;
            }

            FullscreenPlayer.IsVisible = true;
            VideoPlayer.Stop();

            // Use FromUri for online videos
            //VideoPlayer.Source = MediaSource.FromUri(new Uri(videoFile.Url));

            // Use FromFile for offline videos
            VideoPlayer.Source = MediaSource.FromFile(videoFile.Url);

            VideoPlayer.Play();

            // Logged after Play() so a logbook or GPS problem cannot stop the guard watching.
            _ = LogPlayedFileAsync(videoFile.Label, videoFile.Url);
        }
    }

    #region "Played file" logbook entries

    /* One logbook entry per video, written as it starts playing, matching what the Activity
       buttons on the logbook page do: posted to the API when online, cached for SyncService
       when not, with IsSystemEntry set.

       Both playback paths on this page drive the MediaElement directly - there is no queue
       and no playback service - so each one calls this itself. */

    private async Task LogPlayedFileAsync(string label, string filePath)
    {
        try
        {
            var activity = BuildPlayedFileActivity(label, filePath);

            /* Same branch as OnActivityClicked on the logbook page: live when there is a
               connection, local cache otherwise, and SyncService pushes the cache later. */
            if (App.IsOnline)
            {
                var logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
                if (logBookServices == null)
                    return;

                var (isSuccess, message) = await logBookServices.LogActivityTask(
                    activity, ResolveLogbookClientSiteId(), 0, "NA", IsSystemEntry: true);

                if (!isSuccess)
                    await ShowPlayedFileLogFailureAsync(message);
            }
            else
            {
                var (isSuccess, message) = await LogPlayedFileToCacheAsync(activity);
                if (!isSuccess)
                    await ShowPlayedFileLogFailureAsync(message);
            }
        }
        catch (Exception ex)
        {
            // Never allowed to escape: this runs unawaited, so an exception here would be
            // unhandled on a pool thread rather than caught anywhere.
            await ShowPlayedFileLogFailureAsync(ex.Message);
        }
    }

    /// <summary>
    /// The logbook the entry belongs to. On a standard tour that is the guard's own site; on
    /// a patrol car or inspection tour it is the site whose tag was last scanned, because the
    /// car moves between sites within one login. Same rule as GetLocalSiteForPCAR on the
    /// logbook page.
    /// </summary>
    private static int? ResolveLogbookClientSiteId()
    {
        int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out int clientSiteId);

        if (App.TourMode != PatrolTouringMode.PCAR && App.TourMode != PatrolTouringMode.INSP)
            return clientSiteId;

        return App.PcarInspLastScannedSiteId.HasValue && App.PcarInspLastScannedSiteId.Value > 0
            ? App.PcarInspLastScannedSiteId.Value
            : clientSiteId;
    }

    /// <summary>
    /// Offline path, mirroring LogActivityToCache on the logbook page: build the cache row and
    /// hand it to the existing SaveLogActivityCacheData for SyncService to push later.
    /// </summary>
    private async Task<(bool isSuccess, string message)> LogPlayedFileToCacheAsync(string activity)
    {
        if (_scanDataDbService == null)
            return (false, "Local cache is unavailable on this device.");

        /* An entry with no position is worth little to an investigation, so a missing fix
           fails the write rather than storing a blank - same rule as the online path. */
        string gpsCoordinates;
        if (await PermissionService.CheckIfHasLocationPermission())
            gpsCoordinates = await PermissionService.CheckAndGetGpsLocationAsync();
        else
            gpsCoordinates = await PermissionService.CheckAndGetGpsLocationAsync();

        if (string.IsNullOrWhiteSpace(gpsCoordinates))
            return (false, "GPS coordinates not available. Please ensure location services are enabled.");

        int.TryParse(Preferences.Get("GuardId", "0"), out int guardId);
        int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out int clientSiteId);
        int.TryParse(Preferences.Get("UserId", "0"), out int userId);

        if (guardId <= 0 || clientSiteId <= 0 || userId <= 0)
            return (false, "Guard, site or user is not set. Please log in again.");

        var infoService = IPlatformApplication.Current.Services.GetService<IDeviceInfoService>();

        var request = new Data.Entity.PostActivityRequestLocalCache()
        {
            guardId = guardId,
            clientsiteId = clientSiteId,
            userId = userId,
            activityString = activity,
            gps = gpsCoordinates,
            systemEntry = true,
            scanningType = 0,
            tagUID = "NA",
            EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
            EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
            EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
            EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
            EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute(),
            EventMobileUtcDateTime = TimeZoneHelper.GetCurrentUtcDateTime(),
            IsNewGuard = false,
            IsSynced = false,
            UniqueRecordId = Guid.NewGuid(),
            DeviceId = infoService?.GetDeviceId(),
            DeviceName = infoService?.GetDeviceName(),
            IsEntryByPCAR = App.TourMode == PatrolTouringMode.PCAR || App.TourMode == PatrolTouringMode.INSP,
            CallSignId = App.PcarCallSignId,
            PositionId = App.PcarPostionId,
            LogbookclientsiteId = ResolveLogbookClientSiteId()
        };

        var saved = await _scanDataDbService.SaveLogActivityCacheData(request);

        return saved
            ? (true, "Log entry added successfully to cache.")
            : (false, "Failed to add the log entry to cache.");
    }

    /// <summary>
    /// "Played file &lt;name&gt;". Prefers the label the guard saw in the list, because that is
    /// what they would describe if asked about the entry later; falls back to the file name.
    /// Never throws - an unnamed file must still produce an entry.
    /// </summary>
    private static string BuildPlayedFileActivity(string label, string filePath)
    {
        var name = label?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            try { name = Path.GetFileName(filePath); }
            catch { name = null; }
        }

        return $"Played file {(string.IsNullOrWhiteSpace(name) ? "Unknown file" : name)}";
    }

    private static async Task ShowPlayedFileLogFailureAsync(string message)
    {
        Console.WriteLine($"Failed to log played file: {message}");

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Toast.Make($"Played-file log entry not saved. {message}", ToastDuration.Long).Show();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to show played-file log toast: {ex.Message}");
            }
        });
    }

    #endregion


    private void OnStopVideoClicked(object sender, EventArgs e)
    {
        // Pause the video
        VideoPlayer.Pause();

        // Hide the fullscreen player
        FullscreenPlayer.IsVisible = false;
    }


    // VideoFile model class


    private async void OnBackButtonClicked(object sender, EventArgs e)
    {

        // If video is playing in fullscreen, hide it and stop playback
        // Pause the video
        VideoPlayer.Pause();

        // Hide the fullscreen player
        FullscreenPlayer.IsVisible = false;

        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        //Application.Current.MainPage = new NavigationPage(new MainPage());
    }

    protected override bool OnBackButtonPressed()
    {

        // Pause the video
        VideoPlayer.Pause();

        // Hide the fullscreen player
        FullscreenPlayer.IsVisible = false;
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        //Application.Current.MainPage = new NavigationPage(new MainPage());
        return true;
    }

    private void OnStopButtonClicked(object sender, EventArgs e)
    {

    }



    private async void OnPickFileClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;            

            try
            {
                var results = await FilePicker.PickMultipleAsync();
                if (results != null && results.Any())
                {
                    string[] allowedExtensions = { ".jpg", ".jpeg", ".bmp", ".gif", ".heic", ".png" };

                    foreach (var file in results)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(extension))
                        {
                            await DisplayAlert("Invalid File",
                                $"File '{file.FileName}' is not a supported image type.", "OK");
                            continue;
                        }

                        // Default type is twentyfive unless user ticks "rear full page"
                        string fileType = "twentyfive";

                        SelectedFiles.Add(new MyFileModel
                        {
                            File = file,
                            FileType = fileType
                        });
                    }

                    if (SelectedFiles.Any())
                    {
                        FileUploadLoadingOverlay.IsVisible = true;
                        await UploadFileToApiAsync();
                    }
                    else
                    {
                        await DisplayAlert("Notice", "No valid files selected.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"File picking failed: {ex.Message}", "OK");
            }
            finally
            {
                FileUploadLoadingOverlay.IsVisible = false;
                btn.IsEnabled = true;
            }
        }
    }

    private async Task UploadFileToApiAsync()
    {
        try
        {
            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
            string gpsCoordinates = "";
            var _hasGpsLocationPermission = await PermissionService.CheckIfHasLocationPermission();
            if (_hasGpsLocationPermission)
            {
                var _gpsLocation = await PermissionService.CheckAndGetGpsLocationAsync();
                gpsCoordinates = _gpsLocation;
            }
            else
            {
                await DisplayAlert("Location Error", "GPS coordinates not available. Please ensure location services are enabled.", "OK");
                var _gpsLocation = await PermissionService.CheckAndGetGpsLocationAsync();
                if (string.IsNullOrEmpty(_gpsLocation))
                    return;
                else
                    gpsCoordinates = _gpsLocation;
            }

            using var client = new HttpClient();
            var content = new MultipartFormDataContent();

            // Add files + types (same index order)
            foreach (var fileModel in SelectedFiles)
            {
                // Antigravity: Added modular image compression and downsizing to dramatically reduce data usage
                var stream = await Helpers.ImageCompressionHelper.CompressImageAsync(fileModel.File);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                // File
                content.Add(fileContent, "files", fileModel.File.FileName);

                // Type (rear / twentyfive)
                content.Add(new StringContent(fileModel.FileType), "types");
            }

            //If PCAR then change local client site to latest scanned site
            var _localClientSiteId = clientSiteId;
            if (App.TourMode == PatrolTouringMode.PCAR || App.TourMode == PatrolTouringMode.INSP)
            {
                _localClientSiteId = App.PcarInspLastScannedSiteId.HasValue ? (App.PcarInspLastScannedSiteId.Value > 0 ? App.PcarInspLastScannedSiteId.Value : clientSiteId) : clientSiteId;
            }

            // Add other form data
            content.Add(new StringContent(guardId.ToString()), "guardId");
            content.Add(new StringContent(clientSiteId.ToString()), "clientsiteId");
            content.Add(new StringContent(userId.ToString()), "userId");
            content.Add(new StringContent(gpsCoordinates ?? ""), "gps");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZoneCurrentTime().ToString("o")), "eventDateTimeLocal");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset().ToString("o")), "eventDateTimeLocalWithOffset");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZone()), "eventDateTimeZone");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZoneShortName()), "eventDateTimeZoneShort");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZoneOffsetMinute().ToString()), "eventDateTimeUtcOffsetMinute");
            content.Add(new StringContent(_localClientSiteId.ToString()), "logbookclientsiteId");
            content.Add(new StringContent((App.TourMode == PatrolTouringMode.PCAR || App.TourMode == PatrolTouringMode.INSP).ToString()), "isEntryByPCAR");
            content.Add(new StringContent((App.PcarCallSignId.HasValue ? App.PcarCallSignId.ToString() : "")), "callSignId");
            content.Add(new StringContent((App.PcarPostionId.HasValue ? App.PcarPostionId.ToString() : "")), "positionId");

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


    private async Task<(int guardId, int clientSiteId, int userId)> GetSecureStorageValues()
    {
        int.TryParse(Preferences.Get("GuardId", "0"), out int guardId);
        int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out int clientSiteId);
        int.TryParse(Preferences.Get("UserId", "0"), out int userId);

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



    private async void OnPickVideoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;            
            try
            {
                var results = await FilePicker.PickMultipleAsync();
                if (results != null && results.Any())
                {
                    // Allowed video extensions
                    string[] allowedExtensions = { ".mp4", ".mov", ".avi", ".mkv" };

                    foreach (var file in results)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(extension))
                        {
                            await DisplayAlert("Invalid File",
                                $"File '{file.FileName}' is not a supported video type.", "OK");
                            continue;
                        }

                        // Default type is twentyfive unless user ticks "rear full page"
                        string fileType = "video";

                        SelectedFiles.Add(new MyFileModel
                        {
                            File = file,
                            FileType = fileType
                        });
                    }

                    if (SelectedFiles.Any())
                    {
                        FileUploadLoadingOverlay.IsVisible = true;
                        await UploadVideoToApiAsync();
                    }
                    else
                    {
                        await DisplayAlert("Notice", "No valid videos selected.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Video picking failed: {ex.Message}", "OK");
            }
            finally
            {
                btn.IsEnabled = true;
                FileUploadLoadingOverlay.IsVisible = false;
            }
        }
    }

    private async Task UploadVideoToApiAsync()
    {
        try
        {
            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
            string gpsCoordinates = "";
            var _hasGpsLocationPermission = await PermissionService.CheckIfHasLocationPermission();
            if (_hasGpsLocationPermission)
            {
                var _gpsLocation = await PermissionService.CheckAndGetGpsLocationAsync();
                gpsCoordinates = _gpsLocation;
            }
            else
            {
                await DisplayAlert("Location Error", "GPS coordinates not available. Please ensure location services are enabled.", "OK");
                var _gpsLocation = await PermissionService.CheckAndGetGpsLocationAsync();
                if (string.IsNullOrEmpty(_gpsLocation))
                    return;
                else
                    gpsCoordinates = _gpsLocation;
            }

            using var client = new HttpClient();
            var content = new MultipartFormDataContent();

            // Add videos + types
            foreach (var fileModel in SelectedFiles)
            {
                var stream = await fileModel.File.OpenReadAsync();
                var fileContent = new StreamContent(stream);

                // Set MIME type based on extension
                var ext = Path.GetExtension(fileModel.File.FileName).ToLowerInvariant();
                string mimeType = ext switch
                {
                    ".mp4" => "video/mp4",
                    ".mov" => "video/quicktime",
                    ".avi" => "video/x-msvideo",
                    ".mkv" => "video/x-matroska",
                    _ => "application/octet-stream"
                };

                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

                // Add file
                content.Add(fileContent, "files", fileModel.File.FileName);

                // Add matching type (rear / twentyfive)
                content.Add(new StringContent(fileModel.FileType), "types");
            }

            //If PCAR then change local client site to latest scanned site
            var _localClientSiteId = clientSiteId;
            if (App.TourMode == PatrolTouringMode.PCAR || App.TourMode == PatrolTouringMode.INSP)
            {
                _localClientSiteId = App.PcarInspLastScannedSiteId.HasValue ? (App.PcarInspLastScannedSiteId.Value > 0 ? App.PcarInspLastScannedSiteId.Value : clientSiteId) : clientSiteId;
            }

            // Add other form data
            content.Add(new StringContent(guardId.ToString()), "guardId");
            content.Add(new StringContent(clientSiteId.ToString()), "clientsiteId");
            content.Add(new StringContent(userId.ToString()), "userId");
            content.Add(new StringContent(gpsCoordinates ?? ""), "gps");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZoneCurrentTime().ToString("o")), "eventDateTimeLocal");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset().ToString("o")), "eventDateTimeLocalWithOffset");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZone()), "eventDateTimeZone");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZoneShortName()), "eventDateTimeZoneShort");
            content.Add(new StringContent(TimeZoneHelper.GetCurrentTimeZoneOffsetMinute().ToString()), "eventDateTimeUtcOffsetMinute");
            content.Add(new StringContent(_localClientSiteId.ToString()), "logbookclientsiteId");
            content.Add(new StringContent((App.TourMode == PatrolTouringMode.PCAR || App.TourMode == PatrolTouringMode.INSP).ToString()), "isEntryByPCAR");
            content.Add(new StringContent((App.PcarCallSignId.HasValue ? App.PcarCallSignId.ToString() : "")), "callSignId");
            content.Add(new StringContent((App.PcarPostionId.HasValue ? App.PcarPostionId.ToString() : "")), "positionId");

            // Send request
            var uploadResponse = await client.PostAsync(
                $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/UploadMultipleVideos",
                content
            );

            if (!uploadResponse.IsSuccessStatusCode)
            {
                await DisplayAlert("Error", "One or more videos failed to upload.", "OK");
            }
            else
            {
                SelectedFiles.Clear();
                await DisplayAlert("Success", "All videos uploaded successfully.", "OK");

                await Task.Delay(300);


            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Upload failed: {ex.Message}", "OK");
        }
    }


}



public class VideoFile
{
    public string Label { get; set; }
    public string Url { get; set; }
}