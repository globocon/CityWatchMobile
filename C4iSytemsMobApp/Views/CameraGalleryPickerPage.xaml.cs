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
    private IRecentImagesService? _recentImagesService;
    private bool _completed;
    private bool _recentLoaded;
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await Camera.StartCameraPreview(CancellationToken.None); }
        catch { /* preview may already be running */ }

        if (!_recentLoaded)
        {
            _recentLoaded = true;
            await LoadRecentImagesAsync();
        }
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

            var images = await _recentImagesService.GetRecentImagesAsync(30);
            GalleryStrip.ItemsSource = images;
            GalleryStrip.IsVisible = images.Count > 0;
        }
        catch
        {
            GalleryStrip.IsVisible = false;
        }
    }

    private async void OnShutterClicked(object sender, EventArgs e)
    {
        try
        {
            ShutterButton.IsEnabled = false;
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

    private async void OnOpenFullGalleryClicked(object sender, EventArgs e)
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
