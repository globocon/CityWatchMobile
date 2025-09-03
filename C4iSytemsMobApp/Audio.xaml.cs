using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Services;
using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
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

        Mp3ListView.ItemsSource = Mp3Files;
        LoadMp3List();


        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
    }

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
        var httpClient = new HttpClient();
        var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetActivitiesAudio?type=1";
        var mp3List = await httpClient.GetFromJsonAsync<List<Mp3File>>(url);

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