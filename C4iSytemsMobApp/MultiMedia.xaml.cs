using C4iSytemsMobApp.Interface;
using CommunityToolkit.Maui.Views;
using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using System.Net.Http.Json;

namespace C4iSytemsMobApp;

public partial class MultiMedia : ContentPage
{
    public ObservableCollection<VideoFile> VideoFiles { get; set; } = new();
    private ObservableCollection<MyFileModel> SelectedFiles = new();
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
                    string fileType =  "twentyfive";

                    SelectedFiles.Add(new MyFileModel
                    {
                        File = file,
                        FileType = fileType
                    });
                }

                if (SelectedFiles.Any())
                {
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
    }

    private async Task UploadFileToApiAsync()
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

                // File
                content.Add(fileContent, "files", fileModel.File.FileName);

                // Type (rear / twentyfive)
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

                

                
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Save & Close failed: {ex.Message}", "OK");
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



    private async void OnPickVideoClicked(object sender, EventArgs e)
    {
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
                    string fileType =   "video";

                    SelectedFiles.Add(new MyFileModel
                    {
                        File = file,
                        FileType = fileType
                    });
                }

                if (SelectedFiles.Any())
                {
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
    }

    private async Task UploadVideoToApiAsync()
    {
        try
        {
            var (guardId, clientSiteId, userId) = await GetSecureStorageValues();
            string gpsCoordinates = await SecureStorage.GetAsync("GpsCoordinates");

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

            // Add other form data
            content.Add(new StringContent(guardId.ToString()), "guardId");
            content.Add(new StringContent(clientSiteId.ToString()), "clientsiteId");
            content.Add(new StringContent(userId.ToString()), "userId");
            content.Add(new StringContent(gpsCoordinates ?? ""), "gps");

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