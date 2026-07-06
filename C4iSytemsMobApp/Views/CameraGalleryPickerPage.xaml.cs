using System.Collections.ObjectModel;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Services;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.DependencyInjection;

namespace C4iSytemsMobApp.Views;

/// <summary>
/// WhatsApp-style image picker: live camera preview with multi-shot capture,
/// a strip of recent gallery thumbnails, and a full-gallery fallback.
/// Returns the selection as FileResults so the existing logbook upload
/// pipeline (MyFileModel / ImageCompressionHelper) works unchanged.
/// </summary>
public partial class CameraGalleryPickerPage : ContentPage
{
    private readonly TaskCompletionSource<List<FileResult>?> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly ObservableCollection<SelectedImageItem> _selected = new();
    private readonly ObservableCollection<RecentImage> _recentImages = new();
    private IRecentImagesService? _recentImagesService;
    private bool _completed;
    private bool _recentLoaded;
    private bool _previewStartedOnce;
    private int _captureCounter;

    /// <summary>Pushes the picker modally and returns the selected files; null = user cancelled.</summary>
    public static async Task<List<FileResult>?> ShowAsync(INavigation navigation)
    {
        var page = new CameraGalleryPickerPage();
        await navigation.PushModalAsync(page, animated: true);
        return await page._tcs.Task;
    }

    public CameraGalleryPickerPage()
    {
        InitializeComponent();
        SelectedStrip.ItemsSource = _selected;
        _selected.CollectionChanged += (_, _) => UpdateSelectionUi();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Start the camera and load the gallery strip in parallel so neither
        // waits on the other — the strip must be visible as soon as the page opens.
        _ = StartPreviewAsync();
        if (!_recentLoaded)
        {
            _recentLoaded = true;
            _ = LoadRecentImagesAsync();
        }
    }

    private async Task StartPreviewAsync()
    {
        try
        {
            if (!_previewStartedOnce)
                CameraLoadingOverlay.IsVisible = true;

            // The CameraView auto-starts its preview when its platform view attaches,
            // so this manual start mainly covers re-appearing after StopCameraPreview.
            // On some devices the call lingers even though the preview is already live,
            // so never keep the spinner up longer than 2 seconds.
            await Task.WhenAny(StartCameraPreviewSafeAsync(), Task.Delay(2000));
            _previewStartedOnce = true;
        }
        catch { }
        finally
        {
            try
            {
                await CameraLoadingOverlay.FadeTo(0, 250);
                CameraLoadingOverlay.IsVisible = false;
                CameraLoadingOverlay.Opacity = 1;
            }
            catch { }
        }
    }

    private async Task StartCameraPreviewSafeAsync()
    {
        try { await Camera.StartCameraPreview(CancellationToken.None); }
        catch { /* preview may already be running */ }
    }

    protected override void OnDisappearing()
    {
        try { Camera.StopCameraPreview(); }
        catch { }
        base.OnDisappearing();
    }

    private async Task LoadRecentImagesAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<ReadMediaImagesPermission>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<ReadMediaImagesPermission>();

            bool mediaGranted = status == PermissionStatus.Granted;
#if ANDROID
            // Android 14 partial access: READ_MEDIA_IMAGES reports denied but the
            // user-selected subset is readable via READ_MEDIA_VISUAL_USER_SELECTED.
            if (!mediaGranted && OperatingSystem.IsAndroidVersionAtLeast(34))
                mediaGranted = await Permissions.CheckStatusAsync<ReadMediaVisualUserSelectedPermission>() == PermissionStatus.Granted;
#endif

            _recentImagesService = IPlatformApplication.Current?.Services?.GetService<IRecentImagesService>();
            if (!mediaGranted || _recentImagesService == null)
            {
                GalleryStrip.IsVisible = false;
                return;
            }

            // Fast metadata-only query: show the strip immediately with placeholders,
            // then stream the thumbnails in as they load.
            var images = await _recentImagesService.GetRecentImagesAsync(30);
            if (images.Count == 0)
            {
                GalleryStrip.IsVisible = false;
                return;
            }

            foreach (var image in images)
                _recentImages.Add(image);
            GalleryStrip.ItemsSource = _recentImages;
            GalleryStrip.IsVisible = true;

            _ = LoadThumbnailsAsync();
        }
        catch
        {
            GalleryStrip.IsVisible = false;
        }
    }

    private async Task LoadThumbnailsAsync()
    {
        if (_recentImagesService == null)
            return;

        bool galleryButtonSet = false;
        foreach (var image in _recentImages.ToList())
        {
            if (_completed)
                return;

            bool ok = false;
            try { ok = await _recentImagesService.LoadThumbnailAsync(image); }
            catch { }

            if (!ok)
            {
                MainThread.BeginInvokeOnMainThread(() => _recentImages.Remove(image));
                continue;
            }

            // WhatsApp-style: the gallery button shows the latest photo as its thumbnail
            if (!galleryButtonSet)
            {
                galleryButtonSet = true;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    GalleryButtonImage.Source = image.Thumbnail;
                    GalleryButtonImage.IsVisible = true;
                });
            }
        }
    }

    private async void OnShutterClicked(object sender, EventArgs e)
    {
        try
        {
            ShutterButton.IsEnabled = false;
            TriggerFlashEffect();
            await Camera.CaptureImage(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Camera", $"Capture failed: {ex.Message}", "OK");
        }
        finally
        {
            // Small debounce so rapid multi-shot doesn't hit a busy camera
            await Task.Delay(250);
            ShutterButton.IsEnabled = true;
        }
    }

    private void TriggerFlashEffect()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                FlashOverlay.Opacity = 0.7;
                await FlashOverlay.FadeTo(0, 200);
            }
            catch { }
        });
    }

    private void OnMediaCaptureFailed(object? sender, MediaCaptureFailedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
            await DisplayAlert("Camera", $"Capture failed: {e.FailureReason}", "OK"));
    }

    // Fires on a background thread; e.Media is only valid inside this handler.
    private void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
    {
        try
        {
            int n = Interlocked.Increment(ref _captureCounter);
            string path = Path.Combine(FileSystem.CacheDirectory, $"cam_{DateTime.Now:yyyyMMdd_HHmmss}_{n}.jpg");
            using (var fs = File.Create(path))
            {
                e.Media.CopyTo(fs);
            }
#if ANDROID
            NormalizeExifRotation(path);
#endif
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _selected.Add(new SelectedImageItem
                {
                    File = new FileResult(path, "image/jpeg"),
                    Preview = ImageSource.FromFile(path),
                    CachePath = path
                });
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await DisplayAlert("Camera", $"Could not save photo: {ex.Message}", "OK"));
        }
    }

    private async void OnGalleryThumbTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RecentImage image)
            return;

        if (image.IsSelected)
        {
            var existing = _selected.FirstOrDefault(s => s.GalleryItem == image);
            if (existing != null)
                RemoveSelected(existing);
            return;
        }

        if (_recentImagesService == null)
            return;

        var path = await _recentImagesService.CopyToCacheAsync(image);
        if (path == null)
        {
            await DisplayAlert("Gallery", "Could not read the selected image.", "OK");
            return;
        }

        image.IsSelected = true;
        _selected.Add(new SelectedImageItem
        {
            File = new FileResult(path),
            Preview = image.Thumbnail,
            CachePath = path,
            GalleryItem = image
        });
    }

    private async void OnOpenFullGalleryClicked(object sender, TappedEventArgs e)
    {
        try
        {
            var picked = await FilePicker.PickMultipleAsync();
            if (picked == null)
                return;

            foreach (var file in picked)
            {
                if (_selected.Any(s => s.File.FullPath == file.FullPath))
                    continue;

                _selected.Add(new SelectedImageItem
                {
                    File = file,
                    Preview = ImageSource.FromFile(file.FullPath)
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Gallery", $"Could not open gallery: {ex.Message}", "OK");
        }
    }

    private void OnRemoveSelectedClicked(object sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is SelectedImageItem item)
            RemoveSelected(item);
    }

    private void RemoveSelected(SelectedImageItem item)
    {
        _selected.Remove(item);
        if (item.GalleryItem != null)
            item.GalleryItem.IsSelected = false;
        TryDeleteCache(item.CachePath);
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await CompleteAsync(_selected.Select(s => s.File).ToList());
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await CancelAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () => await CancelAsync());
        return true;
    }

    private async Task CancelAsync()
    {
        foreach (var item in _selected.ToList())
            TryDeleteCache(item.CachePath);
        await CompleteAsync(null);
    }

    private async Task CompleteAsync(List<FileResult>? result)
    {
        if (_completed)
            return;
        _completed = true;

        try { await Navigation.PopModalAsync(); }
        catch { }

        _tcs.TrySetResult(result);
    }

    private void UpdateSelectionUi()
    {
        SelectedStrip.IsVisible = _selected.Count > 0;
        DoneButton.IsVisible = _selected.Count > 0;
        DoneButton.Text = $"✓  {_selected.Count}";
    }

    private static void TryDeleteCache(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try { File.Delete(path); }
        catch { }
    }

#if ANDROID
    /// <summary>
    /// CameraX JPEGs carry rotation in EXIF only; the existing ImageCompressionHelper decodes
    /// via BitmapFactory which ignores EXIF, so portrait captures would upload sideways.
    /// Physically rotate the pixels here (capped at ~2048px — the upload pipeline downsizes
    /// to 1024px anyway) and rewrite the file.
    /// </summary>
    private static void NormalizeExifRotation(string path)
    {
        try
        {
            using var exif = new global::Android.Media.ExifInterface(path);
            int orientation = exif.GetAttributeInt(
                global::Android.Media.ExifInterface.TagOrientation,
                (int)global::Android.Media.Orientation.Normal);

            float degrees = orientation switch
            {
                (int)global::Android.Media.Orientation.Rotate90 => 90f,
                (int)global::Android.Media.Orientation.Rotate180 => 180f,
                (int)global::Android.Media.Orientation.Rotate270 => 270f,
                _ => 0f
            };
            if (degrees == 0f)
                return;

            var bounds = new global::Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            global::Android.Graphics.BitmapFactory.DecodeFile(path, bounds);
            int sample = 1;
            int maxDim = Math.Max(bounds.OutWidth, bounds.OutHeight);
            while (maxDim / sample > 2048)
                sample *= 2;

            var options = new global::Android.Graphics.BitmapFactory.Options { InSampleSize = sample };
            using var bitmap = global::Android.Graphics.BitmapFactory.DecodeFile(path, options);
            if (bitmap == null)
                return;

            var matrix = new global::Android.Graphics.Matrix();
            matrix.PostRotate(degrees);
            using var rotated = global::Android.Graphics.Bitmap.CreateBitmap(
                bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);

            using var fs = File.Create(path);
            rotated.Compress(global::Android.Graphics.Bitmap.CompressFormat.Jpeg!, 92, fs);
        }
        catch
        {
            // Leave the file as captured; worst case the image uploads unrotated.
        }
    }
#endif

    public class SelectedImageItem
    {
        public FileResult File { get; set; } = null!;
        public ImageSource? Preview { get; set; }
        public string? CachePath { get; set; }
        public RecentImage? GalleryItem { get; set; }
    }
}
