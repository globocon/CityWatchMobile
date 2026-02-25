using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;


namespace C4iSytemsMobApp.Services
{
    public interface ISyncApiService
    {
        public Task<List<ClientSiteSmartWandTagsHitLogCache>> PushSmartWandTagsHitLogCacheAsync(List<ClientSiteSmartWandTagsHitLogCache> records);
        public Task<List<PostActivityRequestLocalCache>> PushActivityLogCacheAsync(List<PostActivityRequestLocalCache> records);
        public Task<List<OfflineFilesRecords>> PushActivityLogDocumentsCacheAsync(List<OfflineFilesRecords> records);
        public Task<List<PatrolCarLogRequestCache>> PushPatrolCarCacheAsync(List<PatrolCarLogRequestCache> records);
        public Task<List<CustomFieldLogRequestHeadCache>> PushCustomFieldLogCacheAsync(List<CustomFieldLogRequestHeadDto> records);
        public Task<(List<irOfflineCacheDto>, List<irOfflineFilesAttachmentsCache>)> PushIrRequestsLogCacheAsync(List<irOfflineFilesAttachmentsCache> attchRecords, List<irOfflineCacheDto> records);

    }
    public class SyncApiService : ISyncApiService
    {
        string gpsCoordinates;
        int guardId;
        int clientSiteId;
        int userId;
        bool isError;
        string msg;
        public SyncApiService()
        {
            GetSecureStorageValues();
            gpsCoordinates = Preferences.Get("GpsCoordinates", "");
        }

        public async Task<List<ClientSiteSmartWandTagsHitLogCache>> PushSmartWandTagsHitLogCacheAsync(List<ClientSiteSmartWandTagsHitLogCache> records)
        {

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;

            var apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/SyncOfflineSmartWandTagHitData";

            try
            {
                using (HttpClient _http = new HttpClient())
                {
                    await Task.Delay(3000);
                    HttpResponseMessage response = await _http.PostAsJsonAsync(apiUrl, records);
                    if (response.IsSuccessStatusCode)
                    {
                        var settings = await response.Content.ReadFromJsonAsync<List<ClientSiteSmartWandTagsHitLogCache>>();
                        return settings;

                    }
                    else
                    {
                        throw new Exception("Error in syncing smartwand tag hit records");
                    }
                }

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<PostActivityRequestLocalCache>> PushActivityLogCacheAsync(List<PostActivityRequestLocalCache> records)
        {

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;

            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SyncOfflinePostActivityLogData";

            try
            {
                using (HttpClient _http = new HttpClient())
                {
                    await Task.Delay(3000);
                    HttpResponseMessage response = await _http.PostAsJsonAsync(apiUrl, records);
                    if (response.IsSuccessStatusCode)
                    {
                        var settings = await response.Content.ReadFromJsonAsync<List<PostActivityRequestLocalCache>>();
                        return settings;
                    }
                    else
                    {
                        throw new Exception("Error in syncing activity log records");
                    }
                }

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<OfflineFilesRecords>> PushActivityLogDocumentsCacheAsync(List<OfflineFilesRecords> records)
        {

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;

            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/UploadMultipleOffLineSync";

            try
            {
                using (HttpClient _http = new HttpClient())
                {
                    await Task.Delay(3000);
                    var content = new MultipartFormDataContent();
                    foreach (var fileModel in records)
                    {

                        if (File.Exists(fileModel.FileNameWithPathCache))
                        {
                            var stream = await OpenFileReadAsync(fileModel.FileNameWithPathCache);
                            var fileContent = new StreamContent(stream);
                            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                            // Add file
                            content.Add(fileContent, "files", fileModel.FileNameCache);
                        }
                    }

                    // Add other form data
                    // 1. Add metadata as JSON
                    var json = JsonSerializer.Serialize(records);
                    var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                    content.Add(jsonContent, "offlineFilesRecordJsonString");
                    
                    HttpResponseMessage response = await _http.PostAsync(apiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        var settings = await response.Content.ReadFromJsonAsync<List<OfflineFilesRecords>>();
                        return settings;
                    }
                    else
                    {
                        throw new Exception("Error in syncing activity documents log records");
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<PatrolCarLogRequestCache>> PushPatrolCarCacheAsync(List<PatrolCarLogRequestCache> records)
        {

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;

            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SyncOfflinePatrolCarLogData";

            try
            {
                using (HttpClient _http = new HttpClient())
                {
                    await Task.Delay(3000);
                    HttpResponseMessage response = await _http.PostAsJsonAsync(apiUrl, records);
                    if (response.IsSuccessStatusCode)
                    {
                        var settings = await response.Content.ReadFromJsonAsync<List<PatrolCarLogRequestCache>>();
                        return settings;
                    }
                    else
                    {
                        throw new Exception("Error in syncing Patrol Car log records");
                    }
                }

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<CustomFieldLogRequestHeadCache>> PushCustomFieldLogCacheAsync(List<CustomFieldLogRequestHeadDto> records)
        {

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;

            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SyncOfflineCustomFieldLogData";

            try
            {
                using (HttpClient _http = new HttpClient())
                {
                    await Task.Delay(3000);
                    HttpResponseMessage response = await _http.PostAsJsonAsync(apiUrl, records);
                    if (response.IsSuccessStatusCode)
                    {
                        var settings = await response.Content.ReadFromJsonAsync<List<CustomFieldLogRequestHeadCache>>();
                        return settings;
                    }
                    else
                    {
                        throw new Exception("Error in syncing Custom Field log records");
                    }
                }

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<(List<irOfflineCacheDto>, List<irOfflineFilesAttachmentsCache>)> PushIrRequestsLogCacheAsync(List<irOfflineFilesAttachmentsCache> attchRecords, 
            List<irOfflineCacheDto> records)
        {

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return (null,null);

            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/SyncOfflineIrRecords";

            try
            {
                using (HttpClient _http = new HttpClient())
                {
                    await Task.Delay(1000);
                    _http.Timeout = TimeSpan.FromMinutes(8); // increase wait time
                    using var content = new MultipartFormDataContent();
                    var streams = new List<Stream>();
                    foreach (var fileModel in attchRecords)
                    {
                        if (File.Exists(fileModel.FileNameWithPathCache))
                        {
                            var stream = File.OpenRead(fileModel.FileNameWithPathCache);
                            streams.Add(stream);

                            var fileContent = new StreamContent(stream);
                            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                            content.Add(fileContent, "files", fileModel.FileNameCache);
                        }
                    }

                    



                    //foreach (var fileModel in attchRecords)
                    //{

                    //    if (File.Exists(fileModel.FileNameWithPathCache))
                    //    {
                    //        var stream = await OpenFileReadAsync(fileModel.FileNameWithPathCache);
                    //        var fileContent = new StreamContent(stream);
                    //        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    //        // Add file
                    //        content.Add(fileContent, "files", fileModel.FileNameCache);
                    //    }
                    //}

                    string DeviceType = "android";
#if IOS
    DeviceType = "ios";
#endif

                    // Add other form data
                    // 1. Add metadata as JSON
                    content.Add(new StringContent(DeviceType), "irDeviceType");

                    var jsonAttchRecords = JsonSerializer.Serialize(attchRecords);
                    var jsonAttchRecordsContent = new StringContent(jsonAttchRecords, Encoding.UTF8, "application/json");
                    content.Add(jsonAttchRecordsContent, "irOfflineFilesAttachmentsCacheJsonString");

                    var json = JsonSerializer.Serialize(records);
                    var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                    content.Add(jsonContent, "irOfflineCacheJsonString");

                    using var response = await _http.PostAsync(apiUrl, content);

                    // Optional explicit cleanup (usually not needed if content disposed)
                    foreach (var s in streams)
                    {
                        s.Dispose();
                    }

                    // HttpResponseMessage response = await _http.PostAsync(apiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string contentData = await response.Content.ReadAsStringAsync();
                        var responseJson = JsonSerializer.Deserialize<JsonElement>(contentData);

                        // Deserialize irOfflineCache list
                        var irOfflineCacheElement = responseJson.GetProperty("irOfflineCache");
                        List<irOfflineCacheDto> irOfflineCache = JsonSerializer.Deserialize<List<irOfflineCacheDto>>(
                            irOfflineCacheElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        // Deserialize irOfflineCache list
                        var irOfflineAttachmentsElement = responseJson.GetProperty("irOfflineAttachments");
                        List<irOfflineFilesAttachmentsCache> irOfflineAttachments = JsonSerializer.Deserialize<List<irOfflineFilesAttachmentsCache>>(
                            irOfflineAttachmentsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );
                       
                        return (irOfflineCache, irOfflineAttachments);
                    }
                    else
                    {
                        throw new Exception("Error in syncing ir request records");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        public async Task<Stream> OpenFileReadAsync(string fullPath)
        {
            // Ensures async-friendly stream opening
            return await Task.Run(() => File.OpenRead(fullPath));
        }

        private void GetSecureStorageValues()
        {
            string msg = string.Empty;
            int.TryParse(Preferences.Get("GuardId", "0"), out guardId);
            int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out clientSiteId);
            int.TryParse(Preferences.Get("UserId", "0"), out userId);

            if (guardId <= 0)
            {
                msg = "Guard ID not found. Please validate the License Number first.";
                isError = true;
            }
            if (clientSiteId <= 0)
            {
                msg = "Please select a valid Client Site.";
                isError = true;
            }
            if (userId <= 0)
            {
                msg = "User ID is invalid. Please log in again.";
                isError = true;
            }
        }
    }
}
