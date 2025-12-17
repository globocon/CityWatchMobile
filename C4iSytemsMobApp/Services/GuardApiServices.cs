using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System;
using System.Net.Http.Json;
using System.Text.Json;


namespace C4iSytemsMobApp.Services
{
    public class GuardApiServices : IGuardApiServices
    {
        int guardId;
        int clientSiteId;
        int userId;
        bool isError;
        string msg;
        public GuardApiServices()
        {
            // Constructor logic if needed
            GetSecureStorageValues();
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
                
        public async Task<List<SelectListItem>> GetStatesAsync()
        {
            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetStates";

                HttpClient _httpClient = new HttpClient();
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var rows = await response.Content.ReadFromJsonAsync<List<SelectListItem>>();
                return rows ?? new List<SelectListItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching States: {ex.Message}");
                return new List<SelectListItem>();
            }
        }
        public async Task<(bool isSuccess, string errorMessage, NewGuard? _newGuard)> RegisterNewGuardAsync(NewGuard request)
        {            
            var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/RegisterNewGuardFromMobile";
            HttpClient _httpClient = new HttpClient();
            var d = JsonSerializer.Serialize(request);
            var response = await _httpClient.PostAsJsonAsync(apiUrl, request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<NewGuard>>();               
                return (result.isSuccess, result.message, result.data);   
            }
            else
            {
                var errorResult = await response.Content.ReadFromJsonAsync<ApiResponse<NewGuard>>();
                return (errorResult.isSuccess, errorResult.message,null);
            }
        }        
    }

}
