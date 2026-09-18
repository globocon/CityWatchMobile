using C4iSytemsMobApp.Models;

namespace C4iSytemsMobApp.Interface
{
    /// <summary>
    /// Provides access to the device gallery for the camera picker thumbnail strip.
    /// Android implementation: Platforms\Android\Services\RecentImagesService.
    /// </summary>
    public interface IRecentImagesService
    {
        /// <summary>
        /// Newest N device images (MediaStore, date added desc), metadata only — thumbnails are
        /// not loaded yet so this returns fast. Empty list if none or access denied.
        /// </summary>
        Task<IReadOnlyList<RecentImage>> GetRecentImagesAsync(int count, CancellationToken ct = default);

        /// <summary>Loads and assigns the thumbnail for one image. Returns false if it could not be loaded.</summary>
        Task<bool> LoadThumbnailAsync(RecentImage image, CancellationToken ct = default);

        /// <summary>Copies the image content to FileSystem.CacheDirectory; returns the full path, or null on failure.</summary>
        Task<string?> CopyToCacheAsync(RecentImage image, CancellationToken ct = default);
    }
}
