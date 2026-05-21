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

        // Antigravity: Diagnostic logging helper to debug desktop/mobile compression issues
        private static void LogDiagnostic(string message)
        {
            try
            {
                string logPath = Path.Combine(FileSystem.CacheDirectory, "compression_log.txt");
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch
            {
                // Fallback to debug window if file write fails
                System.Diagnostics.Debug.WriteLine($"[ImageCompressionLogFailed] {message}");
            }
        }

        /// <summary>
        /// Downscales and compresses the provided FileResult using visually lossless JPEG compression (60% quality, max 1024px width/height).
        /// If any exception occurs or if it is an unsupported file format, it will safely fallback and return the original uncompressed stream.
        /// </summary>
        /// <param name="file">The FileResult object representing the selected image file.</param>
        /// <param name="maxDimension">The maximum allowable width or height in pixels. Defaults to 1024px.</param>
        /// <param name="quality">The JPEG compression quality, between 1 and 100. Defaults to 60.</param>
        /// <returns>A Stream of the compressed image data, or the original file stream as a safe fallback.</returns>
        public static async Task<Stream> CompressImageAsync(FileResult file, float maxDimension = 1024f, int quality = 60)
        {
            if (file == null)
            {
                LogDiagnostic("CompressImageAsync called with null FileResult.");
                throw new ArgumentNullException(nameof(file));
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            LogDiagnostic($"CompressImageAsync started for: {file.FileName} ({extension})");

            // Check if extension is supported for graphics processing
            if (!AllowedExtensions.Contains(extension))
            {
                LogDiagnostic($"Unsupported extension '{extension}' for compression. Bypassing compression.");
                return await file.OpenReadAsync();
            }

            try
            {
                // Open the original file stream
                using Stream originalStream = await file.OpenReadAsync();
                long originalLength = originalStream.Length;
                LogDiagnostic($"Opened original stream. Size: {originalLength} bytes.");

                // Antigravity: Copy stream to a seekable MemoryStream to guarantee compatibility with Windows/Android decoding platforms
                using var memoryStream = new MemoryStream();
                await originalStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                LogDiagnostic($"Copied original stream to MemoryStream. Position reset to 0.");

                // Load the image using platform native graphics engine
                LogDiagnostic("Attempting PlatformImage.FromStream...");
                using Microsoft.Maui.Graphics.IImage image = PlatformImage.FromStream(memoryStream);
                if (image == null)
                {
                    LogDiagnostic($"[ImageCompression] PlatformImage failed to parse {file.FileName} (returned null). Using original stream.");
                    return await file.OpenReadAsync();
                }

                LogDiagnostic($"Successfully loaded image. Dimensions: {image.Width}x{image.Height}");

                // Downsize the image keeping the original aspect ratio
                Microsoft.Maui.Graphics.IImage processedImage = image;
                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    LogDiagnostic($"Downsizing image to max dimension {maxDimension}...");
                    processedImage = image.Downsize(maxDimension, disposeOriginal: false);
                    LogDiagnostic($"Downsized successfully. New dimensions: {processedImage.Width}x{processedImage.Height}");
                }
                else
                {
                    LogDiagnostic("Image size is within limits. No downsizing needed.");
                }

                try
                {
                    var compressedMs = new MemoryStream();
                    float qualityFloat = Math.Clamp(quality / 100f, 0.01f, 1.0f);

                    LogDiagnostic($"Saving compressed image using ImageFormat.Jpeg at quality {qualityFloat}...");
                    // Compress and save as JPEG to memory stream
                    processedImage.Save(compressedMs, ImageFormat.Jpeg, qualityFloat);

                    // Rewind the memory stream to the beginning
                    compressedMs.Position = 0;
                    
                    LogDiagnostic($"[ImageCompression] Compressed {file.FileName} from {originalLength} bytes to {compressedMs.Length} bytes.");
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
                LogDiagnostic($"[ImageCompression] Error compressing image {file.FileName}: {ex}{Environment.NewLine}Stack Trace: {ex.StackTrace}");
                return await file.OpenReadAsync();
            }
        }
    }
}
