using C4iSytemsMobApp.Models;
using System.Net.Http.Json;

namespace C4iSytemsMobApp.Services
{

    public interface ICrowdControlServices
    {        
        Task<ClientSiteMobileAppSettings?> GetCrowdControlSettingsAsync(string _clientSiteId);
    }

    public class CrowdControlServices : ICrowdControlServices
    {

        public CrowdControlServices()
        {
            // Constructor logic if needed
        }

        public async Task<ClientSiteMobileAppSettings?> GetCrowdControlSettingsAsync(string _clientSiteId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}CrowdCount/GetCrowdCountControlSettings?siteId={_clientSiteId}";
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<ClientSiteMobileAppSettings>();
                    return settings;
                }
            }
            catch (Exception)
            {
                //throw;
                return null; // Example return value
            }
            return null; // Example return value
        }
    }
}
