using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using C4iSytemsMobApp.Views;
using System;
using System.Net.Http.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
#if ANDROID
using Android.Content;
using Android.Net;
using AndroidX.Core.Content;
#endif
using System.IO;


namespace C4iSytemsMobApp.Services
{
    public class AppUpdateService : IAppUpdateService
    {
        private readonly HttpClient _httpClient;

        public AppUpdateService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<bool> CheckForUpdateAsync()
        {
#if ANDROID
        try
        {
            var currentVersion = Version.Parse(AppInfo.Current.VersionString);

            var apiUrl = $"{AppConfig.ApiBaseUrl}AppUpgrade/GetLatestAppVersion?platformType=Android";
            var latestInfo = await _httpClient.GetFromJsonAsync<MobileAppUpgrade>(apiUrl);

            if (latestInfo == null)
                return false;

            var latestVersion = Version.Parse($"{latestInfo.AppVersionMajor}.{latestInfo.AppVersionMinor}.{latestInfo.AppVersionPatch}");

            if (latestVersion != currentVersion)
            {
                string message = $"An active version ({latestVersion}) is available.\n" +
                                 $"You are using {currentVersion}.";

                if (!string.IsNullOrWhiteSpace(latestInfo.AppVersionNotes))
                    message += $"\n\nWhat's new:\n{latestInfo.AppVersionNotes}";

                bool update = await Application.Current.MainPage.DisplayAlert(
                    "Update Available", message, "Update Now", "Later");

                if (update){
                    await DownloadAndInstallApkAsync(latestInfo.AppDownloadUrl);
                    return true;
                }
                else
                {
                    // User choose not to update
                    // User declined the update
                    await Application.Current.MainPage.DisplayAlert(
                        "Update Required",
                        "Cannot continue without update.",
                        "Exit"
                    );
                    Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
                    return false;
                }
                    
            }
            else
            {
                // No update — go to LoginPage
                 return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex}");
        }
#endif
            return false;

        }



#if ANDROID
    private async Task DownloadAndInstallApkAsync(string apkUrl)
    {
        var context = Android.App.Application.Context;
        var popup = new DownloadProgressPopup();
        await Application.Current.MainPage.Navigation.PushModalAsync(popup);

        try
        {
            string fileName = Path.GetFileName(apkUrl);
            string filePath = Path.Combine(context.CacheDir!.AbsolutePath, fileName);

            using var response = await _httpClient.GetAsync(apkUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = total > 0;

            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[256 * 1024];  // read 256 kb of data
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (canReportProgress)
                {
                    double percent = (totalRead * 1.0 / total) * 100;
                    popup.UpdateProgress(percent);
                }
            }

            await Application.Current.MainPage.Navigation.PopModalAsync();

            // Trigger APK installer
            var file = new Java.IO.File(filePath);
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, $"{context.PackageName}.fileprovider", file);

            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, "application/vnd.android.package-archive");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download/Install failed: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to download update.", "OK");
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }
    }
#endif

    }
}



