using C4iSytemsMobApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

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
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var settings = await response.Content.ReadFromJsonAsync<ClientSiteMobileAppSettings>();
                return settings;
            }

            return null; // Example return value
        }
    }
}
