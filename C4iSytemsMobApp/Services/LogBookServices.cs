using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System.Net.Http.Json;


namespace C4iSytemsMobApp.Services
{
    public class LogBookServices : ILogBookServices
    {       
        public LogBookServices()
        {
            // Constructor logic if needed

        }

       
        public async Task<HttpResponseMessage?> WriteEntryToLogBook(string _apiurl)
        {            
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            //client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
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

        public async Task<(bool isSuccess, string errorMessage)> LogActivityTask(string activityDescription)
        {
            var (guardId, clientSiteId, userId, isError, msg) = await GetSecureStorageValues();

            string gpsCoordinates = await SecureStorage.GetAsync("GpsCoordinates");

            if (string.IsNullOrWhiteSpace(gpsCoordinates))
            {               
                return (false, "GPS coordinates not available. Please ensure location services are enabled");
            }


            if (guardId <= 0 || clientSiteId <= 0 || userId <= 0) return (false, msg);
            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/PostActivity" +
                 $"?guardId={guardId}" +
                 $"&clientsiteId={clientSiteId}" +
                 $"&userId={userId}" +
                 $"&activityString={Uri.EscapeDataString(activityDescription)}" +
                 $"&gps={Uri.EscapeDataString(gpsCoordinates)}";


            try
            {
                try
                {
                    HttpClient _httpClient = new HttpClient();
                    HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
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
            }
            catch (Exception ex)
            {
                return (false, $"Failed: {ex.Message}");
            }
        }

        private async Task<(int guardId, int clientSiteId, int userId, bool isError, string msg)> GetSecureStorageValues()
        {
            bool isError = false;
            string msg = string.Empty;
            int.TryParse(await SecureStorage.GetAsync("GuardId"), out int guardId);
            int.TryParse(await SecureStorage.GetAsync("SelectedClientSiteId"), out int clientSiteId);
            int.TryParse(await SecureStorage.GetAsync("UserId"), out int userId);

            if (guardId <= 0)
            {
                msg = "Guard ID not found. Please validate the License Number first.";
                return (-1, -1, -1, isError, msg);
            }
            if (clientSiteId <= 0)
            {
                msg = "Please select a valid Client Site.";
                return (-1, -1, -1, isError, msg);
            }
            if (userId <= 0)
            {
                msg = "User ID is invalid. Please log in again.";
                return (-1, -1, -1, isError, msg);
            }

            return (guardId, clientSiteId, userId, isError, msg);
        }
    }
}
