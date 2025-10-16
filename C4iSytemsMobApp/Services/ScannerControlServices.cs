using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;


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

        public async Task<TagInfoApiResponse?> FetchTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetNFCtagInfoData?siteId={_clientSiteId}&TagUid={_tagUid}&GuardId={_guardId}&UserId={_userId}";
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var taginfo = await response.Content.ReadFromJsonAsync<TagInfoApiResponse>();
                    return taginfo;
                }
            }
            catch (Exception ex)
            {
                // Optionally log error or handle it
                Console.WriteLine($"Error: {ex.Message}");
            }

            return null; // Example return value
        }

        public async Task<bool> CheckIfGuardHasTagAddAccess(string _guardId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/CheckIfGuardHasTagAddAccess?GuardId={_guardId}";
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<bool>();
                    return settings;
                }
            }
            catch (Exception ex)
            {
                // Optionally log error or handle it
                Console.WriteLine($"Error: {ex.Message}");
            }

            return false; // Example return value
        }

        public async Task<TagInfoApiResponse?> SaveNFCTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId, string _tagLabel)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/SaveNFCtagInfoData";
            //string apiParameters = $"?siteId={_clientSiteId}&TagUid={_tagUid}&GuardId={_guardId}&UserId={_userId}&_tagLabel=";
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            try
            {
                ClientSiteSmartWandTags csswt = new ClientSiteSmartWandTags()
                {
                    ClientSiteId = Convert.ToInt32(_clientSiteId),
                    UId = _tagUid,
                    LabelDescription = _tagLabel,
                    TagsTypeId = (int)ScanningType.NFC,
                    FqBypass = false,
                    TagsType = ScanningType.NFC.ToString(), //"NFC"
                    IsDeleted = false
                };

                var json = JsonSerializer.Serialize(csswt);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var taginfo = await response.Content.ReadFromJsonAsync<TagInfoApiResponse>();
                    return taginfo;
                }
            }
            catch (Exception ex)
            {
                // Optionally log error or handle it
                Console.WriteLine($"Error: {ex.Message}");
            }

            return null; // Example return value
        }
    }
}
