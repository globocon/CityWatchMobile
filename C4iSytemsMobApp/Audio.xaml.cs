using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
namespace C4iSytemsMobApp;

public partial class Audio : ContentPage
{
    //private readonly IAudioManager _audioManager = new AudioManager();    
    //private IAudioPlayer _player;
    //public ObservableCollection<Mp3File> Mp3Files { get; set; } = new();
    //public Audio()
    //{
    //    InitializeComponent();
    //    NavigationPage.SetHasNavigationBar(this, false);
    //    Mp3ListView.ItemsSource = Mp3Files;
    //    LoadMp3List();
    //}

    //private async void LoadMp3List()
    //{
    //    var httpClient = new HttpClient();
    //    var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetActivitiesAudio?type=1";
    //    var mp3List = await httpClient.GetFromJsonAsync<List<Mp3File>>(url);

    //    foreach (var item in mp3List)
    //    {
    //        item.PlayCommand = new Command(async () => await PlayAudio(item.Url));
    //        Mp3Files.Add(item);
    //    }
    //}

    //private async Task PlayAudio(string url)
    //{
    //    try
    //    {
    //        LoadingIndicator.IsVisible = true;
    //        LoadingIndicator.IsRunning = true;

    //        if (_player != null)
    //        {
    //            _player.Stop();
    //            _player.Dispose();
    //        }

    //        string localFilePath = await DownloadFileAsync(url);

    //        LoadingIndicator.IsVisible = false;
    //        LoadingIndicator.IsRunning = false;

    //        _player = _audioManager.CreatePlayer(File.OpenRead(localFilePath));
    //        _player.Play();
    //    }
    //    catch (Exception ex)
    //    {
    //        LoadingIndicator.IsVisible = false;
    //        LoadingIndicator.IsRunning = false;
    //        await Application.Current.MainPage.DisplayAlert("Error", $"Failed to play audio: {ex.Message}", "OK");
    //    }
    //}

    //private async Task<string> DownloadFileAsync(string fileUrl)
    //{
    //    using (HttpClient client = new HttpClient())
    //    {
    //        var response = await client.GetAsync(fileUrl);
    //        if (!response.IsSuccessStatusCode)
    //        {
    //            throw new Exception("Failed to download file.");
    //        }

    //        byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
    //        string fileName = Path.GetFileName(new Uri(fileUrl).LocalPath);
    //        string localPath = Path.Combine(FileSystem.CacheDirectory, fileName);
    //        await File.WriteAllBytesAsync(localPath, fileBytes);

    //        return localPath;
    //    }
    //}

    //private async void OnBackButtonClicked(object sender, EventArgs e)
    //{
    //    Application.Current.MainPage = new NavigationPage(new MainPage());
    //}

    //protected override bool OnBackButtonPressed()
    //{
    //    // Handle back button logic here
    //    Application.Current.MainPage = new NavigationPage(new MainPage());

    //    // Return true to prevent default behavior (going back)
    //    return true;
    //}

    //public class Mp3File
    //{
    //    public string Label { get; set; }
    //    public string Url { get; set; }
    //    public Command PlayCommand { get; set; }
    //}


    private readonly IAudioManager _audioManager = new AudioManager();
    private IAudioPlayer _player;
    public ObservableCollection<Mp3File> Mp3Files { get; set; } = new();
    private Mp3File _currentlyPlayingFile;
    private bool isPlaying = false;

    public Audio()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        Mp3ListView.ItemsSource = Mp3Files;
        LoadMp3List();
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
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            StopAudio(); // Stop any existing playback

            string localFilePath = await DownloadFileAsync(file.Url);

            _player = _audioManager.CreatePlayer(File.OpenRead(localFilePath));
            _currentlyPlayingFile = file;
            StopButton.IsEnabled = true; // Enable the global stop button
            _player.Play();
            isPlaying = true;  // Set correct state
            UpdatePlayPauseIcon();
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
        catch (Exception ex)
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to play audio: {ex.Message}", "OK");
        }
    }

    private void StopAudio()
    {
        if (_player != null && _currentlyPlayingFile != null)
        {
            _player.Stop();
            _player.Dispose();
            _currentlyPlayingFile = null;
            StopButton.IsEnabled = false; // Disable the stop button when no audio is playing
            isPlaying = false; // Toggle state
            UpdatePlayPauseIcon();
        }
    }

    private void OnStopButtonClicked(object sender, EventArgs e)
    {
        StopAudio();
    }

    private void UpdatePlayPauseIcon()
    {
        StopButton.Source = isPlaying ? "stop.png" : "play.png";
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
        Application.Current.MainPage = new NavigationPage(new MainPage());
    }

    protected override bool OnBackButtonPressed()
    {
        Application.Current.MainPage = new NavigationPage(new MainPage());
        return true;
    }

    public class Mp3File
    {
        public string Label { get; set; }
        public string Url { get; set; }
        public Command PlayCommand { get; set; }
    }
}