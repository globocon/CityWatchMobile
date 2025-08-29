using C4iSytemsMobApp.Interface;
using System.Net.Http.Json;


namespace C4iSytemsMobApp.Services
{
    public class ScannerControlServices : IScannerControlServices
    {

        public ScannerControlServices()
        {
            // Constructor logic if needed
        }

        public async Task<List<string>?> CheckScannerOnboardedAsync(string _clientSiteId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetScannerControlSettings?siteId={_clientSiteId}";
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var settings = await response.Content.ReadFromJsonAsync<List<string>>();
                return settings;
            }

            return new List<string>(); // Example return value
        }
    }
}
