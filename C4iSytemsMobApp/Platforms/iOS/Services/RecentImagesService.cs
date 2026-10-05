using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using CoreGraphics;
using Foundation;
using Photos;
using UIKit;

namespace C4iSytemsMobApp.Platforms.iOS.Services
{
    /// <summary>
    /// iOS half of the camera picker's recent-photos strip, using the Photos framework.
    /// Same contract as the Android MediaStore version: metadata first, thumbnails lazily,
    /// and the chosen image copied into FileSystem.CacheDirectory for the upload pipeline.
    ///
    /// The caller has already asked for Permissions.Photos. With "Limited" access the
    /// fetch returns only the photos the user chose to share, which is the iOS equivalent
    /// of Android 14's partial access.
    /// </summary>
    public class RecentImagesService : IRecentImagesService
    {
        /// <summary>Longest side of the cached copy; the upload pipeline downsizes to 1024px anyway.</summary>
        private const float MaxCopyDimension = 2048f;

        public Task<IReadOnlyList<RecentImage>> GetRecentImagesAsync(int count, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var results = new List<RecentImage>();
                try
                {
                    var options = new PHFetchOptions
                    {
                        SortDescriptors = new[] { new NSSortDescriptor("creationDate", false) },
                        FetchLimit = (nuint)count
                    };

                    var assets = PHAsset.FetchAssets(PHAssetMediaType.Image, options);
                    foreach (var obj in assets)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (obj is not PHAsset asset)
                            continue;

                        results.Add(new RecentImage
                        {
                            AssetId = asset.LocalIdentifier,
                            DisplayName = "photo.jpg"   // CopyToCacheAsync always writes a JPEG
                        });
                    }
                }
                catch
                {
                    // Return whatever was collected; strip is best-effort only.
                }
                return (IReadOnlyList<RecentImage>)results;
            }, ct);
        }

        public async Task<bool> LoadThumbnailAsync(RecentImage image, CancellationToken ct = default)
        {
            if (image.Thumbnail != null)
                return true;

            var asset = FindAsset(image.AssetId);
            if (asset == null)
                return false;

            var options = new PHImageRequestOptions
            {
                // One callback with the final image, rather than a degraded one first.
                DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat,
                ResizeMode = PHImageRequestOptionsResizeMode.Fast,
                NetworkAccessAllowed = true   // photos kept only in iCloud
            };

            var tcs = new TaskCompletionSource<UIImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
            PHImageManager.DefaultManager.RequestImageForAsset(asset, new CGSize(256, 256),
                PHImageContentMode.AspectFill, options, (result, _) => tcs.TrySetResult(result));

            using var thumb = await tcs.Task;
            var bytes = thumb?.AsJPEG(0.8f)?.ToArray();
            if (bytes == null)
                return false;

            await MainThread.InvokeOnMainThreadAsync(() =>
                image.Thumbnail = ImageSource.FromStream(() => new MemoryStream(bytes)));
            return true;
        }

        public async Task<string?> CopyToCacheAsync(RecentImage image, CancellationToken ct = default)
        {
            try
            {
                var asset = FindAsset(image.AssetId);
                if (asset == null)
                    return null;

                var options = new PHImageRequestOptions
                {
                    DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat,
                    NetworkAccessAllowed = true,
                    Version = PHImageRequestOptionsVersion.Current   // includes the user's edits
                };

                var tcs = new TaskCompletionSource<NSData?>(TaskCreationOptions.RunContinuationsAsynchronously);
                PHImageManager.DefaultManager.RequestImageDataAndOrientation(asset, options,
                    (data, _, _, _) => tcs.TrySetResult(data));

                using var data = await tcs.Task;
                if (data == null)
                    return null;

                /* Photos hands back HEIC for most iPhone shots. Re-encode as an upright JPEG so
                   the file behaves like an Android one all the way through upload. */
                using var uiImage = UIImage.LoadFromData(data);
                if (uiImage == null)
                    return null;

                var path = Path.Combine(FileSystem.CacheDirectory, $"gal_{Guid.NewGuid():N}.jpg");
                return SaveUprightJpeg(uiImage, path) ? path : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Re-saves a camera capture as an upright JPEG. AVFoundation JPEGs carry rotation in
        /// EXIF only, like CameraX on Android, so this is the iOS counterpart of
        /// CameraGalleryPickerPage.NormalizeExifRotation.
        /// </summary>
        public static void NormalizeOrientation(string path)
        {
            try
            {
                using var uiImage = UIImage.FromFile(path);
                if (uiImage == null || uiImage.Orientation == UIImageOrientation.Up)
                    return;

                SaveUprightJpeg(uiImage, path);
            }
            catch
            {
                // Leave the file as captured; worst case the image uploads unrotated.
            }
        }

        /// <summary>
        /// Draws the image with its orientation applied (UIImage.Draw honours it), capped at
        /// MaxCopyDimension, and writes the pixels as a JPEG with no rotation flag.
        /// </summary>
        private static bool SaveUprightJpeg(UIImage source, string path)
        {
            var size = source.Size;   // already in display orientation
            var scale = Math.Min(1.0, MaxCopyDimension / Math.Max(size.Width, size.Height));
            var target = new CGSize(Math.Round(size.Width * scale), Math.Round(size.Height * scale));

            var format = new UIGraphicsImageRendererFormat { Scale = 1, Opaque = true };
            using var renderer = new UIGraphicsImageRenderer(target, format);
            using var upright = renderer.CreateImage(_ => source.Draw(new CGRect(CGPoint.Empty, target)));

            using var jpeg = upright.AsJPEG(0.92f);
            if (jpeg == null)
                return false;

            File.WriteAllBytes(path, jpeg.ToArray());
            return true;
        }

        private static PHAsset? FindAsset(string? assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return null;

            var assets = PHAsset.FetchAssetsUsingLocalIdentifiers(new[] { assetId }, null);
            return assets.Count > 0 ? assets.FirstObject as PHAsset : null;
        }
    }
}
