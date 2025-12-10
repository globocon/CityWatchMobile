using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using System;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;


namespace C4iSytemsMobApp.Services
{
    public class ScannerControlServices : IScannerControlServices
    {
        private readonly IDeviceInfoService infoService;
        private readonly IScanDataDbServices _scanDataDbServices;
        private string devicename;
        private string deviceid;
        private string deviceType = "Unknown";
        public ScannerControlServices()
        {
            // Constructor logic if needed
            infoService = IPlatformApplication.Current.Services.GetService<IDeviceInfoService>();
            _scanDataDbServices = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
            devicename = infoService?.GetDeviceName();
            deviceid = infoService?.GetDeviceId();

#if ANDROID
            deviceType = "Android";
#elif IOS
            deviceType = "iOS";
#elif WINDOWS
            deviceType = "Windows";
#elif MACCATALYST
        deviceType = "MacCatalyst";
#elif TIZEN
        deviceType = "Tizen";
#endif
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

        public async Task<List<DropdownItem>?> GetClientSiteSmartWandsAsync(string _clientSiteId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetClientSiteSmartWands?siteId={_clientSiteId}";
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var settings = await response.Content.ReadFromJsonAsync<List<DropdownItem>>();
                return settings;
            }

            return new List<DropdownItem>(); // Example return value
        }

        public async Task<TagInfoApiResponse?> FetchTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId, ScanningType _scannerType)
        {
            await CheckIfSmartWandIsDeRegisteredAsync(_clientSiteId); // Check if smartwand is deregistered before fetching tag info
            string savedSmartWandIdKeyName = $"{_clientSiteId}_SavedSmartWandId";
            var savedSmartWandId = Preferences.Get(savedSmartWandIdKeyName, 0);
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetScannerTagInfoData?siteId={_clientSiteId}&TagUid={_tagUid}&GuardId={_guardId}&UserId={_userId}&TagsTypeId={(int)_scannerType}&SmartWandId={savedSmartWandId}";
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

        public async Task<SmartWandDeviceRegister> CheckAndRegisterSmartWandAsync(int _selectedSmartWandId, string deviceid, string devicename, string deviceType)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/CheckAndRegisterDeviceWithSmartWand";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            try
            {
                SmartWandDeviceRegister csswt = new SmartWandDeviceRegister()
                {
                    SmartWandId = _selectedSmartWandId,
                    DeviceId = deviceid,
                    DeviceName = devicename,
                    DeviceType = deviceType,
                    IsSuccess = false,
                    Message = null
                };

                var json = JsonSerializer.Serialize(csswt);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var taginfo = await response.Content.ReadFromJsonAsync<SmartWandDeviceRegister>();
                    return taginfo;
                }
                else
                {
                    csswt.Message = "Failed to register the Smart Wand device.";
                    return csswt;
                }

            }
            catch (Exception ex)
            {
                // Optionally log error or handle it
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public async Task CheckIfSmartWandIsDeRegisteredAsync(string _clientSiteId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/CheckIfSmartWandIsDeRegisteredAsync";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            try
            {
                //var json = JsonSerializer.Serialize(deviceid);
                //var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsJsonAsync(apiUrl, deviceid);  //client.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var taginfo = await response.Content.ReadFromJsonAsync<bool>();
                    if (taginfo)
                    {
                        // If the device is deregistered, clear the saved preferences
                        string savedSmartWandIdKeyName = $"{_clientSiteId}_SavedSmartWandId";
                        string savedSmartWandNameKeyName = $"{_clientSiteId}_SavedSmartWandName";
                        Preferences.Remove(savedSmartWandIdKeyName);
                        Preferences.Remove(savedSmartWandNameKeyName);
                    }
                    return;
                }
                else
                {
                    return;
                }

            }
            catch (Exception ex)
            {
                // Optionally log error or handle it
                Console.WriteLine($"Error: {ex.Message}");
                return;
            }
        }

        public async Task<List<ClientSiteSmartWandTagsLocal>> GetSmartWandTagsForSite(string siteId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetClientSiteAllSmartWandTags?siteId={siteId}";
            // Here you would typically make an HTTP request to the API endpoint
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var settings = await response.Content.ReadFromJsonAsync<List<ClientSiteSmartWandTagsLocal>>();
                return settings;
            }

            return new List<ClientSiteSmartWandTagsLocal>(); // Example return value
        }

        public async Task<(bool isSuccess, string errorMessage, int cachecount)> SaveScanDataToLocalCache(string _TagUid, ScanningType _scannerType,
            int? LoggedInClientSiteId, int? LoggedInUserId, int? LoggedInGuardId)
        {
            int _ChaceCount = 0;
            var message = "";

            if (!LoggedInClientSiteId.HasValue) return (false, "Invalid ClientSite !!!", _ChaceCount);

            if (!LoggedInUserId.HasValue) return (false, "Invalid User Id !!!", _ChaceCount);

            if (!LoggedInGuardId.HasValue) return (false, "Invalid Guard Id !!!", _ChaceCount);

            string gpsCoordinates = Preferences.Get("GpsCoordinates", "");
            string savedSmartWandIdKeyName = $"{LoggedInClientSiteId.Value}_SavedSmartWandId";
            var savedSmartWandId = Preferences.Get(savedSmartWandIdKeyName, 0);
            if (string.IsNullOrWhiteSpace(gpsCoordinates))
            {
                return (false, "GPS coordinates not available. Please ensure location services are enabled", _ChaceCount);
            }

            var _lastTagScannedRecord = _scanDataDbServices.GetLastScannedTagDateTime(LoggedInClientSiteId.Value, _TagUid);
            //Check if scanned tag recently with in a minute from the same site          
            if (_lastTagScannedRecord != null && _lastTagScannedRecord.LoggedInClientSiteId == LoggedInClientSiteId && (DateTime.UtcNow - _lastTagScannedRecord.HitUtcDateTime).TotalSeconds < 60)
            {                 
                if (_scannerType == ScanningType.NFC)
                    message = "Tag already scanned !!!";
                else if (_scannerType == ScanningType.BLUETOOTH)
                    message = "iBeacon already scanned !!!";

                return (false, message, _ChaceCount);
            }


            var TagInfoDetails = _scanDataDbServices.GetSmartWandTagDetailOfTag(_TagUid);
            if (App.TourMode == PatrolTouringMode.STND)
            {
                if (TagInfoDetails != null && TagInfoDetails.ClientSiteId != LoggedInClientSiteId)
                {                    
                    if (_scannerType == ScanningType.NFC)
                        message = "Tag does not belong to logged in site. Please check.";
                    else if (_scannerType == ScanningType.BLUETOOTH)
                        message = "iBeacon does not belong to logged in site. Please check.";

                    return (false, message, _ChaceCount);
                }
            }

            if(TagInfoDetails == null && _scannerType == ScanningType.BLUETOOTH)
            {
                return (false, "iBeacon not found.", _ChaceCount);
            }

            ClientSiteSmartWandTagsHitLogCache newrecord = new ClientSiteSmartWandTagsHitLogCache()
            {

                LoggedInClientSiteId = LoggedInClientSiteId.Value,
                LoggedInUserId = LoggedInUserId.Value,
                LoggedInGuardId = LoggedInGuardId.Value,
                TagUId = _TagUid,
                TagsTypeId = (int)_scannerType,
                HitUtcDateTime = DateTime.UtcNow,
                HitLocalDateTime = DateTime.UtcNow.ToLocalTime(),
                LastModifiedUtc = DateTime.Now,
                SmartWandId = savedSmartWandId,
                GPScoordinates = gpsCoordinates,
                IsSynced = false,
                UniqueRecordId = Guid.NewGuid(),
                EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
                EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
                EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
                EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
                EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute(),
            };

            await _scanDataDbServices.SaveScanData(newrecord);
            _ChaceCount = _scanDataDbServices.GetCacheRecordsCount();
                       
            if (_scannerType == ScanningType.NFC)
                message = "Tag scan record saved to cache.";
            else if (_scannerType == ScanningType.BLUETOOTH)
                message = "iBeacon scan record saved to cache.";

            return (true, message, _ChaceCount);
        }


        public async Task<bool> CheckIfTagExistsForSiteInLocalDb(string _TagUid)
        {
            var rtn = false;
            var _TagFound = _scanDataDbServices.GetSmartWandTagDetailOfTag(_TagUid);            
            if (_TagFound != null)
            {
                rtn = true;
            }
            return rtn;
        }

    }
}
