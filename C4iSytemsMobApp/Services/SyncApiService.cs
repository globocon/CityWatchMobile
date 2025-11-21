using C4iSytemsMobApp.Data.Entity;
using System.Net.Http.Json;


namespace C4iSytemsMobApp.Services
{
    public interface ISyncApiService
    {
        public Task<List<ClientSiteSmartWandTagsHitLogCache>> PushSmartWandTagsHitLogCacheAsync(List<ClientSiteSmartWandTagsHitLogCache> records);
        
    }
    public class SyncApiService: ISyncApiService
    {
        //private readonly HttpClient _http;
        string gpsCoordinates;
        int guardId;
        int clientSiteId;
        int userId;
        bool isError;
        string msg;

        public SyncApiService()
        {
            //_http = new() { BaseAddress = new Uri($"{AppConfig.ApiBaseUrl}") };
            GetSecureStorageValues();
            gpsCoordinates = Preferences.Get("GpsCoordinates", "");
        }
        public async Task<List<ClientSiteSmartWandTagsHitLogCache>> PushSmartWandTagsHitLogCacheAsync(List<ClientSiteSmartWandTagsHitLogCache> records)
        {
            var apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/SyncOfflineSmartWandTagHitData";
            
            try
            {
                HttpClient _httpClient = new HttpClient();
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(apiUrl, records);
                //HttpResponseMessage response = await _http.PostAsJsonAsync(apiUrl, records);
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<List<ClientSiteSmartWandTagsHitLogCache>>();
                    return settings;
                   
                }
                else
                {
                    throw new Exception("Error in syncing records");
                }
            }
            catch (Exception ex)
            {
                throw;
            }

            //try
            //{
            //    var resp = await _http.PostAsJsonAsync("orders/sync", records);
            //    return resp.IsSuccessStatusCode;
            //}
            //catch { return false; }
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
