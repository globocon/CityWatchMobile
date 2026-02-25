using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System;
using System.Net.Http.Json;


namespace C4iSytemsMobApp.Services
{
    public class LogBookServices : ILogBookServices
    {
        int guardId;
        int clientSiteId;
        int userId;
        bool isError;
        string msg;
        public LogBookServices()
        {
            // Constructor logic if needed
            GetSecureStorageValues();
        }


        public async Task<HttpResponseMessage?> WriteEntryToLogBook(string _apiurl)
        {
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            try
            {
                HttpResponseMessage response = await client.GetAsync(_apiurl);
                return response;
            }
            catch (Exception ex)
            {
                // Optionally log error or handle it
                Console.WriteLine($"Error: {ex.Message}");
                throw; // Re-throw the exception to propagate it
            }
        }

        public async Task<(bool isSuccess, string errorMessage)> LogActivityTask(string activityDescription, 
            int scanningType = 0, string tagUID = "NA", bool IsSystemEntry = false, int NFCScannedFromSiteId = -1)
        {
            string gpsCoordinates = Preferences.Get("GpsCoordinates", "");

            if (string.IsNullOrWhiteSpace(gpsCoordinates))
            {
                return (false, "GPS coordinates not available. Please ensure location services are enabled");
            }

            GetSecureStorageValues();

            if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return (false, msg);

            var postClientSiteId = clientSiteId;
            if(NFCScannedFromSiteId > 0)
            {
                postClientSiteId = NFCScannedFromSiteId;
            }

            PostActivityRequest request = new PostActivityRequest()
            {
                guardId = guardId,
                clientsiteId = postClientSiteId,
                userId = userId,
                activityString = activityDescription,
                gps = gpsCoordinates,
                systemEntry = IsSystemEntry,
                scanningType = scanningType,
                tagUID = tagUID,
                EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
                EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
                EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
                EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
                EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute(),
                IsNewGuard = false
            };
                        
            HttpClient _httpClient = new HttpClient();
            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/PostActivity";

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(apiUrl, request);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Log entry added successfully.");
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    return (false, $"Failed: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed: {ex.Message}");
            }
            finally
            {
                _httpClient.Dispose();
            }

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

        public async Task<Dictionary<string, string>> GetCustomFieldConfigAsync()
        {
            throw new NotImplementedException();
        }
        public async Task<List<Dictionary<string, string?>>> GetCustomFieldLogsAsync()
        {
            HttpClient _httpClient = new HttpClient();
            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}LogBook/GetCustomFieldLogs?siteId={clientSiteId}";


                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var rows = await response.Content.ReadFromJsonAsync<List<Dictionary<string, string?>>>();
                return rows ?? new List<Dictionary<string, string?>>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Custom Field logs: {ex.Message}");
                return new List<Dictionary<string, string?>>();
            }
            finally
            {
                _httpClient.Dispose();
            }
        }
        public async Task<(bool isSuccess, string errorMessage)> SaveCustomFieldLogAsync(Dictionary<string, string> record)
        {
            // CustomFieldLogRequest
            var request = new CustomFieldLogRequest
            {
                SiteId = clientSiteId,
                Records = record
            };

            var apiUrl = $"{AppConfig.ApiBaseUrl}LogBook/SaveCustomFieldLog";
            HttpClient _httpClient = new HttpClient();
            var response = await _httpClient.PostAsJsonAsync(apiUrl, request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
                return (result.IsSuccess, result.message);
            }
            else
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                return (false, $"Failed: {errorMessage}");
            }
        }
        public async Task<List<PatrolCarLog>> GetPatrolCarLogsAsync()
        {
            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}LogBook/GetPatrolCarLogs?siteId={clientSiteId}";

                HttpClient _httpClient = new HttpClient();
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var rows = await response.Content.ReadFromJsonAsync<List<PatrolCarLog>>();
                return rows ?? new List<PatrolCarLog>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Patrol Car logs: {ex.Message}");
                return new List<PatrolCarLog>();
            }
        }
        public async Task<(bool isSuccess, string errorMessage)> SavePatrolCarLogAsync(PatrolCarLog record)
        {
            var request = new PatrolCarLogRequest
            {
                SiteId = clientSiteId,
                Records = record
            };

            var apiUrl = $"{AppConfig.ApiBaseUrl}LogBook/SavePatrolCarLog";
            HttpClient _httpClient = new HttpClient();
            var response = await _httpClient.PostAsJsonAsync(apiUrl, request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
                return (result.IsSuccess, result.message);
            }
            else
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                return (false, $"Failed: {errorMessage}");
            }
        }
    }

    public class CustomFieldLogRequest
    {
        public int SiteId { get; set; }
        public Dictionary<string, string> Records { get; set; }
    }

    public class PatrolCarLogRequest
    {
        public int SiteId { get; set; }
        public PatrolCarLog Records { get; set; }
    }
}
