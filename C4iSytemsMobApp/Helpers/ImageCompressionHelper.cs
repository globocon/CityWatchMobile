using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Storage;

namespace C4iSytemsMobApp.Helpers
{
    /// <summary>
    /// Utility helper class for handling mobile image compression and downsizing.
    /// Added to dramatically reduce data usage spikes by compressing uncompressed camera photos.
    /// Design prioritizes a safe, zero-risk fallback to original uncompressed stream if anything fails.
    /// </summary>
    public static class ImageCompressionHelper
    {
        // Allowed extensions for image compression
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".heic", ".bmp", ".gif" };

        /// <summary>
        /// Downscales and compresses the provided FileResult using visually lossless JPEG compression (75% quality, max 1280px width/height).
        /// If any exception occurs or if it is an unsupported file format, it will safely fallback and return the original uncompressed stream.
        /// </summary>
        /// <param name="file">The FileResult object representing the selected image file.</param>
        /// <param name="maxDimension">The maximum allowable width or height in pixels. Defaults to 1280px.</param>
        /// <param name="quality">The JPEG compression quality, between 1 and 100. Defaults to 75.</param>
        /// <returns>A Stream of the compressed image data, or the original file stream as a safe fallback.</returns>
        public static async Task<Stream> CompressImageAsync(FileResult file, float maxDimension = 1280f, int quality = 75)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            // Check if extension is supported for graphics processing
            if (!AllowedExtensions.Contains(extension))
            {
                // Fallback immediately to original stream without attempt
                return await file.OpenReadAsync();
            }

            try
            {
                // Open the original file stream
                using Stream originalStream = await file.OpenReadAsync();

                // Load the image using platform native graphics engine
                using Microsoft.Maui.Graphics.IImage image = PlatformImage.FromStream(originalStream);
                if (image == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImageCompression] PlatformImage failed to parse {file.FileName}. Using original stream.");
                    return await file.OpenReadAsync();
                }

                // Downsize the image keeping the original aspect ratio
                // Downsize returns a new IImage. We dispose of the original unless it returned the same reference.
                Microsoft.Maui.Graphics.IImage processedImage = image;
                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    processedImage = image.Downsize(maxDimension, disposeOriginal: false);
                }

                try
                {
                    var compressedMs = new MemoryStream();
                    float qualityFloat = Math.Clamp(quality / 100f, 0.01f, 1.0f);

                    // Compress and save as JPEG to memory stream
                    processedImage.Save(compressedMs, ImageFormat.Jpeg, qualityFloat);

                    // Rewind the memory stream to the beginning
                    compressedMs.Position = 0;
                    
                    System.Diagnostics.Debug.WriteLine($"[ImageCompression] Compressed {file.FileName} from {originalStream.Length} bytes to {compressedMs.Length} bytes.");
                    return compressedMs;
                }
                finally
                {
                    // If a new downsized image was created, dispose of it
                    if (processedImage != image)
                    {
                        processedImage.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                // Safe robust fallback in case of any issues with native graphics APIs
                System.Diagnostics.Debug.WriteLine($"[ImageCompression] Error compressing image {file.FileName}: {ex.Message}. Falling back to uncompressed stream.");
                return await file.OpenReadAsync();
            }
        }
    }
}
