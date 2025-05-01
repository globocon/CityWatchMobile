using CommunityToolkit.Maui.Views;
using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using System.Net.Http.Json;

namespace C4iSytemsMobApp;

public partial class MultiMedia : ContentPage
{
    public ObservableCollection<VideoFile> VideoFiles { get; set; } = new();

    public MultiMedia()
    {
        InitializeComponent();
        BindingContext = this;
        LoadVideos();
    }

    private async void LoadVideos()
    {
        try
        {
            // Loading indicator code is commented out; uncomment it if needed.
            // LoadingIndicator.IsVisible = true;
            // LoadingIndicator.IsRunning = true;

            using var client = new HttpClient();
            var url = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetActivitiesAudio?type=3";
            var videos = await client.GetFromJsonAsync<List<VideoFile>>(url);

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
            FullscreenPlayer.IsVisible = true;
            VideoPlayer.Stop();

            // Use FromUri for online videos
            VideoPlayer.Source = MediaSource.FromUri(new Uri(videoFile.Url));

            VideoPlayer.Play();
        }
    }


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
        Application.Current.MainPage = new NavigationPage(new MainPage());
    }

    protected override bool OnBackButtonPressed()
    {
        Application.Current.MainPage = new NavigationPage(new MainPage());
        return true;
    }

    private void OnStopButtonClicked(object sender, EventArgs e)
    {
        
    }
}
public class VideoFile
{
    public string Label { get; set; }
    public string Url { get; set; }
}