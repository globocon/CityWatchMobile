using AutoMapper;
using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
//using static Android.Provider.MediaStore;
using static C4iSytemsMobApp.Services.AudioPlaybackService;
namespace C4iSytemsMobApp;

public partial class Audio : ContentPage
{


    private CancellationTokenSource _playbackCts;
    private readonly IAudioManager _audioManager = new AudioManager();
    private IAudioPlayer _player;
    public ObservableCollection<Mp3File> Mp3Files { get; set; } = new ObservableCollection<Mp3File>();
    private Mp3File _currentlyPlayingFile;
    private bool isPlaying = false;
    private int _selectedSilenceMinutes = 0;
    private readonly IScanDataDbServices _scanDataDbService;
    private readonly IMapper _mapper;


    public static class AudioState
    {
        public static bool IsPlaying { get; set; } = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Always check service state
        bool playing = AudioPlaybackService.Instance.IsPlaying;
        NavigationPage.SetHasNavigationBar(this, false);
        StopButton.IsEnabled = playing;
        isPlaying = playing;
        UpdatePlayPauseIcon();
    }
    public Audio()
    {
        InitializeComponent();

        // Set the BindingContext to the page itself if Mp3Files is a property here
        this.BindingContext = this;

        _scanDataDbService = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
        _mapper = IPlatformApplication.Current.Services.GetService<IMapper>();
        Mp3ListView.ItemsSource = Mp3Files;
        LoadMp3List();

        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;

        HookPlayedFileLogging();
    }

    #region "Played file" logbook entries

    /* One logbook entry per audio file, written as that file actually begins playing,
       matching what the Activity buttons on the logbook page do: posted to the API when
       online, cached for SyncService when not, with IsSystemEntry set.

       Every playback path on this page - single play, play-checked, and the looping
       silence mode - funnels through AudioPlaybackService, so one FileStarted subscription
       covers all of them instead of each entry point remembering to log. */

    private static readonly object _playLogLock = new();
    private static bool _playLogHooked;

    /* File path -> the label the guard saw in the list. Held statically because the handler
       below outlives any one page instance. */
    private static readonly Dictionary<string, string> _playLogLabels = new(StringComparer.OrdinalIgnoreCase);

    /* Files already logged in the CURRENT playback session. The silence mode loops the same
       queue indefinitely, and one entry per file per lap would bury the logbook - so a file
       is logged once per session, and a session starts when a play button is pressed. */
    private static readonly HashSet<string> _playLogSeen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Subscribed once for the life of the app rather than per page instance. The queue keeps
    /// playing after the guard leaves this page and those files still belong in the logbook,
    /// so a subscription tied to the page would lose them. It also cannot be per-instance: a
    /// new Audio page is constructed every time the guard opens the screen, so N subscriptions
    /// would write N entries for the same file.
    /// </summary>
    private static void HookPlayedFileLogging()
    {
        lock (_playLogLock)
        {
            if (_playLogHooked)
                return;

            AudioPlaybackService.Instance.FileStarted += OnAudioFileStarted;
            _playLogHooked = true;
        }
    }

    /// <summary>
    /// Called by the play buttons immediately before StartPlayback. Records what the guard
    /// sees each file called, and opens a fresh session so a file played again later is
    /// logged again.
    /// </summary>
    private static void BeginPlayedFileSession(IEnumerable<Mp3File> files)
    {
        lock (_playLogLock)
        {
            _playLogSeen.Clear();

            foreach (var file in files ?? Enumerable.Empty<Mp3File>())
            {
                if (!string.IsNullOrWhiteSpace(file?.Url))
                    _playLogLabels[file.Url] = file.Label;
            }
        }
    }

    private static void OnAudioFileStarted(object sender, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        string label;
        lock (_playLogLock)
        {
            // Add returns false when the file has already been logged this session.
            if (!_playLogSeen.Add(filePath))
                return;

            _playLogLabels.TryGetValue(filePath, out label);
        }

        /* Fire-and-forget: playback has already started and the guard pressed play to hear
           something. A slow logbook, a missing GPS fix or a dead connection must not stall
           the queue - StartPlayback is awaiting this file, so blocking here would delay the
           next one. */
        _ = LogPlayedFileAsync(label, filePath);
    }

    private static async Task LogPlayedFileAsync(string label, string filePath)
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
    private static async Task<(bool isSuccess, string message)> LogPlayedFileToCacheAsync(string activity)
    {
        var scanDataDbService = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
        if (scanDataDbService == null)
            return (false, "Local cache is unavailable on this device.");

        /* An entry with no position is worth little to an investigation, so a missing fix
           fails the write rather than storing a blank - same rule as the online path. No
           DisplayAlert here: this runs off a playback callback, not a button press. */
        string gpsCoordinates = "";
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

        var request = new PostActivityRequestLocalCache()
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

        var saved = await scanDataDbService.SaveLogActivityCacheData(request);

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

    private void OnPlaybackStateChanged(object sender, PlaybackState state)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            bool isPlaying = state == PlaybackState.Playing;
            StopButton.IsEnabled = isPlaying;
            this.isPlaying = isPlaying;
            UpdatePlayPauseIcon();

            // Hide loader when playback state is resolved
            if (state == PlaybackState.Playing || state == PlaybackState.Stopped)
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            }
        });
    }

    private void ShowSilencePopup()
    {
        // Populate picker only once
        if (MinutesPicker.Items.Count == 0)
        {
            for (int i = 0; i <= 120; i++)
                MinutesPicker.Items.Add(i.ToString());
            MinutesPicker.SelectedIndex = 0; // default 0
        }

        SilencePopupOverlay.IsVisible = true;
    }


    private async void OnSilenceOkClicked(object sender, EventArgs e)
    {
        if (MinutesPicker.SelectedIndex >= 0)
        {
            _selectedSilenceMinutes = int.Parse(MinutesPicker.SelectedItem.ToString());
        }

        SilencePopupOverlay.IsVisible = false;

        // Show loader
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;

        var checkedFiles = Mp3Files.Where(f => f.IsChecked).Select(f => f.Url).ToList();
        if (!checkedFiles.Any())
        {
            await DisplayAlert("Info", "No files selected to play.", "OK");
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            return;
        }

        // Enqueue files and enable looping
        AudioPlaybackService.Instance.EnqueueFiles(checkedFiles, loop: true);

        // Subscribe to playback state change
        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;

        // One "Played file ..." entry per file as it starts; looping replays are not re-logged.
        BeginPlayedFileSession(Mp3Files.Where(f => f.IsChecked));

        await AudioPlaybackService.Instance.StartPlayback(_selectedSilenceMinutes);
    }

   


    private void OnStopButtonClicked(object sender, EventArgs e)
    {
        AudioPlaybackService.Instance.Stop();
        StopAudio();
    }


    private HttpClient _httpClient = new HttpClient(); // reuse one instance





    //public async Task PlayCheckedFilesWithSilence(List<Mp3File> files, int silenceMinutes = 0)
    //{
    //    _playbackCts?.Cancel();
    //    _playbackCts = new CancellationTokenSource();
    //    var token = _playbackCts.Token;

    //    try
    //    {
    //        MainThread.BeginInvokeOnMainThread(() =>
    //        {
    //            LoadingIndicator.IsVisible = true;
    //            LoadingIndicator.IsRunning = true;
    //            StopButton.IsEnabled = true;
    //            isPlaying = true;
    //            UpdatePlayPauseIcon();
    //        });

    //        while (!token.IsCancellationRequested) // loop forever until stopped
    //        {
    //            foreach (var file in files)
    //            {
    //                if (token.IsCancellationRequested)
    //                    return;

    //                _player?.Stop();
    //                _player?.Dispose();
    //                _player = null;

    //                var tcs = new TaskCompletionSource<bool>();

    //                // Download and buffer into memory
    //                using var remoteStream = await _httpClient.GetStreamAsync(file.Url, token);
    //                using var memoryStream = new MemoryStream();
    //                await remoteStream.CopyToAsync(memoryStream, token);
    //                memoryStream.Position = 0;

    //                _player = AudioManager.Current.CreatePlayer(memoryStream);
    //                _player.PlaybackEnded += (s, e) => tcs.TrySetResult(true);

    //                MainThread.BeginInvokeOnMainThread(() =>
    //                {
    //                    LoadingIndicator.IsVisible = false;
    //                    LoadingIndicator.IsRunning = false;
    //                });
    //                AudioState.IsPlaying = true; // t
    //                _player.Play();
    //                await tcs.Task; // wait for song to finish

    //                if (silenceMinutes > 0 && !token.IsCancellationRequested)
    //                {
    //                    await Task.Delay(TimeSpan.FromMinutes(silenceMinutes), token);
    //                }
    //            }
    //        }
    //    }
    //    catch (TaskCanceledException)
    //    {
    //        // Normal stop
    //    }
    //    finally
    //    {
    //        _player?.Stop();
    //        _player?.Dispose();
    //        _player = null;

    //        MainThread.BeginInvokeOnMainThread(() =>
    //        {
    //            isPlaying = false;
    //            StopButton.IsEnabled = false;
    //            LoadingIndicator.IsVisible = false;
    //            LoadingIndicator.IsRunning = false;
    //            UpdatePlayPauseIcon();
    //            AudioState.IsPlaying = false; // track state
    //        });
    //    }
    //}






    public async Task PlayCheckedFilesWithSilence(List<Mp3File> files, int silenceMinutes = 0)
    {
        // Cancel any ongoing playback
        _playbackCts?.Cancel();
        _playbackCts = new CancellationTokenSource();
        var token = _playbackCts.Token;

        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
                StopButton.IsEnabled = true;
                isPlaying = true;
                UpdatePlayPauseIcon();
            });

            var selectedFiles = Mp3Files.Where(f => f.IsChecked).Select(f => f.Url).ToList();
            AudioPlaybackService.Instance.EnqueueFiles(selectedFiles);

            // One "Played file ..." entry per file as it starts.
            BeginPlayedFileSession(Mp3Files.Where(f => f.IsChecked));

            await AudioPlaybackService.Instance.StartPlayback(_selectedSilenceMinutes);
        }
        catch (TaskCanceledException)
        {
            // playback was cancelled
        }
        finally
        {


            MainThread.BeginInvokeOnMainThread(() =>
            {
                isPlaying = false;
                StopButton.IsEnabled = false;
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                UpdatePlayPauseIcon();
            });
        }
    }







    public void StopPlayback()
    {
        _playbackCts?.Cancel();
        _player?.Stop();
    }






    private void OnSilenceCancelClicked(object sender, EventArgs e)
    {
        SilencePopupOverlay.IsVisible = false;
    }
    private async void LoopPlaybackButton_Clicked(object sender, EventArgs e)
    {
        ShowSilencePopup();

        // TODO: Call your loop logic here with silenceMinutes
    }
    private async void LoadMp3List()
    {
        //var httpClient = new HttpClient();
        //var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetActivitiesAudio?type=1";
        //var mp3List = await httpClient.GetFromJsonAsync<List<Mp3File>>(url);

        var _existingAudioFiles = await _scanDataDbService.GetMultimediaLocalList(1);
        var mp3List = _mapper.Map<List<Mp3File>>(_existingAudioFiles);

        foreach (var item in mp3List)
        {
            item.PlayCommand = new Command(async () => await PlayAudio(item));
            Mp3Files.Add(item);
        }
    }

    private async Task PlayAudio(Mp3File file)
    {
        try
        {
            // Show loader while preparing
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            string[] checkedFiles = new string[] { file.Url };
            if (file.Url == null || !file.Url.Any())
            {
                await DisplayAlert("Info", "No files selected to play.", "OK");
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                return;
            }


            await Task.Delay(100); // let UI render before starting playback
            AudioPlaybackService.Instance.EnqueueFiles(checkedFiles.ToList(), loop: false);

            // One "Played file ..." entry, written when the file actually starts.
            BeginPlayedFileSession(new[] { file });

            await AudioPlaybackService.Instance.StartPlayback(0);




            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;

            //StopAudio(); // Stop any existing playback

            //string localFilePath = await DownloadFileAsync(file.Url);

            //_player = _audioManager.CreatePlayer(File.OpenRead(localFilePath));
            //_currentlyPlayingFile = file;
            //StopButton.IsEnabled = true; // Enable the global stop button
            //_player.Play();
            //AudioState.IsPlaying = true; // track state
            //isPlaying = true;  // Set correct state
            //UpdatePlayPauseIcon();
            //LoadingIndicator.IsVisible = false;
            //LoadingIndicator.IsRunning = false;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    //private void StopAudio()
    //{

    //    _playbackCts?.Cancel();
    //    AudioPlaybackService.Instance.Stop();
    //    //if (_player != null && _currentlyPlayingFile != null)
    //    //{
    //    //    _player.Stop();
    //    //    _player.Dispose();
    //    //    _currentlyPlayingFile = null;
    //    //    StopButton.IsEnabled = false; // Disable the stop button when no audio is playing
    //    //    isPlaying = false; // Toggle state
    //    //    UpdatePlayPauseIcon();
    //    //}

    //    // Cancel any ongoing playback
    //    _playbackCts?.Cancel();

    //    if (_player != null)
    //    {
    //        try
    //        {
    //            _player.Stop();
    //        }
    //        catch { /* ignore if already stopped/disposed */ }

    //        try
    //        {
    //            if (_player != null)
    //            {
    //                _player.Dispose();

    //            }
    //        }
    //        catch { /* ignore if already disposed */ }

    //        _player = null;
    //    }


    //    _currentlyPlayingFile = null;
    //    StopButton.IsEnabled = false;
    //    isPlaying = false;
    //    AudioState.IsPlaying = false;
    //    UpdatePlayPauseIcon();

    //    LoadingIndicator.IsVisible = false;
    //    LoadingIndicator.IsRunning = false;

    //    foreach (var file in Mp3Files)
    //    {
    //        file.IsChecked = false;
    //    }
    //    Mp3ListView.ItemsSource = null;
    //    Mp3ListView.ItemsSource = Mp3Files;
    //}


    private void StopAudio()
    {
        // Cancel any ongoing playback loop
        _playbackCts?.Cancel();

        // Stop the service immediately


        // Reset UI state
        _currentlyPlayingFile = null;
        StopButton.IsEnabled = false;
        isPlaying = false;
        AudioState.IsPlaying = false;
        UpdatePlayPauseIcon();

        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;

        foreach (var file in Mp3Files)
            file.IsChecked = false;

        Mp3ListView.ItemsSource = null;
        Mp3ListView.ItemsSource = Mp3Files;
    }

    private void UpdatePlayPauseIcon()
    {
        StopButton.Source = isPlaying ? "stop.png" : "play.png";
        AudioState.IsPlaying = isPlaying;
    }

    private async Task<string> DownloadFileAsync(string fileUrl)
    {
        using (HttpClient client = new HttpClient())
        {
            var response = await client.GetAsync(fileUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to download file.");
            }

            byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
            string fileName = Path.GetFileName(new Uri(fileUrl).LocalPath);
            string localPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(localPath, fileBytes);

            return localPath;
        }
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        //Application.Current.MainPage = new NavigationPage(new MainPage());
    }

    protected override bool OnBackButtonPressed()
    {
        var volumeButtonService = IPlatformApplication.Current.Services.GetService<IVolumeButtonService>();
        Application.Current.MainPage = new NavigationPage(new MainPage(volumeButtonService));
        //Application.Current.MainPage = new NavigationPage(new MainPage());
        return true;
    }



    public class Mp3File : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string Url { get; set; }
        public string Label { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        [JsonIgnore]
        public Command PlayCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}