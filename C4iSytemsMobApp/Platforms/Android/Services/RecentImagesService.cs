using Android.Content;
using Android.Provider;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using ABitmap = Android.Graphics.Bitmap;
using ASize = Android.Util.Size;

namespace C4iSytemsMobApp.Platforms.Android.Services
{
    public class RecentImagesService : IRecentImagesService
    {
        public async Task<IReadOnlyList<RecentImage>> GetRecentImagesAsync(int count, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var results = new List<RecentImage>();
                try
                {
                    var resolver = Platform.AppContext.ContentResolver;
                    var contentUri = MediaStore.Images.Media.ExternalContentUri;
                    if (resolver == null || contentUri == null)
                        return (IReadOnlyList<RecentImage>)results;

                    string[] projection =
                    {
                        MediaStore.Images.Media.InterfaceConsts.Id,
                        MediaStore.Images.Media.InterfaceConsts.DisplayName
                    };

                    using var cursor = resolver.Query(contentUri, projection, null, null,
                        $"{MediaStore.Images.Media.InterfaceConsts.DateAdded} DESC");
                    if (cursor == null)
                        return (IReadOnlyList<RecentImage>)results;

                    int idCol = cursor.GetColumnIndexOrThrow(MediaStore.Images.Media.InterfaceConsts.Id);
                    int nameCol = cursor.GetColumnIndexOrThrow(MediaStore.Images.Media.InterfaceConsts.DisplayName);

                    while (cursor.MoveToNext() && results.Count < count)
                    {
                        ct.ThrowIfCancellationRequested();

                        long id = cursor.GetLong(idCol);
                        string name = cursor.GetString(nameCol) ?? $"image_{id}.jpg";

                        var thumbBytes = LoadThumbnailBytes(resolver, id);
                        if (thumbBytes == null)
                            continue;

                        results.Add(new RecentImage
                        {
                            MediaStoreId = id,
                            DisplayName = name,
                            Thumbnail = ImageSource.FromStream(() => new MemoryStream(thumbBytes))
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

        public async Task<string?> CopyToCacheAsync(RecentImage image, CancellationToken ct = default)
        {
            try
            {
                var resolver = Platform.AppContext.ContentResolver;
                var contentUri = MediaStore.Images.Media.ExternalContentUri;
                if (resolver == null || contentUri == null)
                    return null;

                var uri = ContentUris.WithAppendedId(contentUri, image.MediaStoreId);
                using var input = resolver.OpenInputStream(uri);
                if (input == null)
                    return null;

                var ext = Path.GetExtension(image.DisplayName);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = ".jpg";

                var path = Path.Combine(FileSystem.CacheDirectory, $"gal_{Guid.NewGuid():N}{ext}");
                using (var output = File.Create(path))
                {
                    await input.CopyToAsync(output, ct);
                }
                return path;
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? LoadThumbnailBytes(ContentResolver resolver, long id)
        {
            try
            {
                ABitmap? bitmap;
                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    var uri = ContentUris.WithAppendedId(MediaStore.Images.Media.ExternalContentUri!, id);
                    bitmap = resolver.LoadThumbnail(uri, new ASize(256, 256), null);
                }
                else
                {
#pragma warning disable CS0618, CA1422 // MediaStore.Images.Thumbnails is the pre-API-29 mechanism
                    bitmap = MediaStore.Images.Thumbnails.GetThumbnail(resolver, id, ThumbnailKind.MiniKind, null);
#pragma warning restore CS0618, CA1422
                }

                if (bitmap == null)
                    return null;

                using var ms = new MemoryStream();
                bitmap.Compress(ABitmap.CompressFormat.Jpeg!, 80, ms);
                bitmap.Recycle();
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}
