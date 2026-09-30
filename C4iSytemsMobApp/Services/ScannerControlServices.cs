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
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;


namespace C4iSytemsMobApp.Services
{
    public class ScannerControlServices : IScannerControlServices
    {        
        private readonly IScanDataDbServices _scanDataDbServices;
        private string deviceType = "Unknown";
        public ScannerControlServices()
        {
            // Constructor logic if needed
            _scanDataDbServices = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();

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
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<List<string>>();
                    return settings;
                }
            }

            return new List<string>(); // Example return value
        }

        public async Task<List<DropdownItem>?> GetClientSiteSmartWandsAsync(string _clientSiteId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetClientSiteSmartWands?siteId={_clientSiteId}";
            // Here you would typically make an HTTP request to the API endpoint
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<List<DropdownItem>>();
                    return settings;
                }
            }
            return new List<DropdownItem>(); // Example return value
        }

        public async Task<TagInfoApiResponse?> FetchTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId, ScanningType _scannerType)
        {
            await CheckIfSmartWandIsDeRegisteredAsync(_clientSiteId); // Check if smartwand is deregistered before fetching tag info
            string savedSmartWandIdKeyName = $"{_clientSiteId}_SavedSmartWandId";
            var savedSmartWandId = Preferences.Get(savedSmartWandIdKeyName, 0);
            string gpsCoordinates = await PermissionService.GetGpsLocationWithOutCheckingPermissionAsync();
            string gpsCoordinatesEncoded = Uri.EscapeDataString(gpsCoordinates);
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetScannerTagInfoData?siteId={_clientSiteId}&TagUid={_tagUid}&GuardId={_guardId}&UserId={_userId}&TagsTypeId={(int)_scannerType}&SmartWandId={savedSmartWandId}&gpsCoordinates={gpsCoordinatesEncoded}";
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
            finally
            {
                client.Dispose();
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
            finally
            {
                client.Dispose();
            }

            return false; // Example return value
        }

        /// <summary>
        /// The tag type exactly as dbo.SmartWandTagsType stores it - 'Bluetooth' and 'NFC'
        /// (DbScript/306). SaveClientSiteSmartWandTags looks the type row up with an exact
        /// string match and dereferences the result, so ScanningType.BLUETOOTH.ToString(),
        /// which is "BLUETOOTH", would find nothing and throw on the server.
        /// </summary>
        private static string ToTagTypeValue(ScanningType scannerType) =>
            scannerType == ScanningType.BLUETOOTH ? "Bluetooth" : "NFC";

        public async Task<TagInfoApiResponse?> SaveTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _tagLabel, ScanningType _scannerType)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/SaveNFCtagInfoData";

            HttpClient client = new HttpClient();
            try
            {
                var csswt = new ClientSiteSmartWandTags()
                {
                    // Id 0 makes this an insert; the server rejects a UID that already exists.
                    Id = 0,
                    ClientSiteId = Convert.ToInt32(_clientSiteId),
                    UId = _tagUid,
                    LabelDescription = _tagLabel,
                    TagsTypeId = (int)_scannerType,   // overwritten server-side from TagsType
                    FqBypass = false,
                    TagsType = ToTagTypeValue(_scannerType),
                    IsDeleted = false
                };

                var json = JsonSerializer.Serialize(csswt);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<TagInfoApiResponse>();

                return new TagInfoApiResponse { IsSuccess = false, message = "Unable to reach the server." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new TagInfoApiResponse { IsSuccess = false, message = ex.Message };
            }
            finally
            {
                client.Dispose();
            }
        }

        public async Task<GuardNameDetails?> GetGuardNameDetailsAsync()
        {
            int.TryParse(Preferences.Get("GuardId", "0"), out int guardId);
            if (guardId <= 0)
                return new GuardNameDetails { IsSuccess = false, message = "Guard ID not found. Please log in again." };

            var apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetGuardNameDetails?guardId={guardId}";

            HttpClient client = new HttpClient();
            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<GuardNameDetails>();

                return new GuardNameDetails { IsSuccess = false, message = "Unable to reach the server." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new GuardNameDetails { IsSuccess = false, message = ex.Message };
            }
            finally
            {
                client.Dispose();
            }
        }

        public async Task<TagEditInfo?> GetTagForEditAsync(string _tagUid, ScanningType _scannerType)
        {
            /* "nfc" / "bluetooth" - the values SmartWandTagsType holds. The type is part of the
               lookup because an NFC tag and a beacon can carry the same UID and are separate
               records. */
            var tagType = _scannerType == ScanningType.BLUETOOTH ? "bluetooth" : "nfc";

            /* The LOGGED-IN site, not the PCAR-resolved one: the server decides what this guard
               may reach from it - own site, plus RC-linked sites when the link has smart wand
               enabled, or anything at all on a patrol car tour. That is the same call the scan
               path makes, and the decision belongs there rather than on the handset. */
            int.TryParse(Preferences.Get("SelectedClientSiteId", "0"), out int loggedInSiteId);

            var apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetTagForEdit" +
                         $"?tagUid={Uri.EscapeDataString(_tagUid ?? string.Empty)}&tagType={tagType}&siteId={loggedInSiteId}";

            HttpClient client = new HttpClient();
            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<TagEditInfo>();

                return new TagEditInfo { IsSuccess = false, tagFound = false, message = "Unable to reach the server." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new TagEditInfo { IsSuccess = false, tagFound = false, message = ex.Message };
            }
            finally
            {
                client.Dispose();
            }
        }

        public async Task<TagInfoApiResponse?> UpdateTagDescriptionAsync(TagEditInfo _tag, string _newDescription)
        {
            if (_tag == null || _tag.Id <= 0)
                return new TagInfoApiResponse { IsSuccess = false, message = "Tag to update was not supplied." };

            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/SaveNFCtagInfoData";

            HttpClient client = new HttpClient();
            try
            {
                /* Id carries the update: SaveClientSiteSmartWandTags inserts when Id is 0 and
                   updates the matching row otherwise. Every other field is echoed back exactly
                   as it was read, so editing a description cannot move the tag to another site
                   or change its type as a side effect. */
                var csswt = new ClientSiteSmartWandTags()
                {
                    Id = _tag.Id,
                    ClientSiteId = _tag.ClientSiteId,
                    UId = _tag.UId,
                    LabelDescription = _newDescription,
                    TagsTypeId = _tag.TagsTypeId,
                    FqBypass = false,
                    TagsType = _tag.TagsType,
                    IsDeleted = false
                };

                var json = JsonSerializer.Serialize(csswt);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<TagInfoApiResponse>();

                return new TagInfoApiResponse { IsSuccess = false, message = "Unable to reach the server." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new TagInfoApiResponse { IsSuccess = false, message = ex.Message };
            }
            finally
            {
                client.Dispose();
            }
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
            finally
            {
                client.Dispose();
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
            finally
            {
                client.Dispose();
            }
        }

        public async Task<int> GetSmartWandByDeviceIdAsync()
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetSmartWandByDeviceId";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            try
            {
                HttpResponseMessage response = await client.PostAsJsonAsync(apiUrl, App.DeviceId);
                if (response.IsSuccessStatusCode)
                {
                    var taginfo = await response.Content.ReadFromJsonAsync<int>();
                    return taginfo;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                // Optionally log error or handle it
                Console.WriteLine($"Error: {ex.Message}");
                return 0;
            }
            finally
            {
                client.Dispose();
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
                HttpResponseMessage response = await client.PostAsJsonAsync(apiUrl, App.DeviceId);  //client.PostAsync(apiUrl, content);
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
            finally
            {
                client.Dispose();
            }
        }

        public async Task<List<ClientSiteSmartWandTagsLocal>> GetSmartWandTagsForSite(string siteId)
        {
            string apiUrl = $"{AppConfig.ApiBaseUrl}Scanner/GetClientSiteAllSmartWandTags?siteId={siteId}";
            // Here you would typically make an HTTP request to the API endpoint
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<List<ClientSiteSmartWandTagsLocal>>();
                    return settings;
                }
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
                        
            string savedSmartWandIdKeyName = $"{LoggedInClientSiteId.Value}_SavedSmartWandId";
            var savedSmartWandId = Preferences.Get(savedSmartWandIdKeyName, 0);
            string gpsCoordinates = "";
            var _hasGpsLocationPermission = await PermissionService.CheckIfHasLocationPermission();
            if (_hasGpsLocationPermission)
            {
                var _gpsLocation = await PermissionService.CheckAndGetGpsLocationAsync();
                gpsCoordinates = _gpsLocation;
            }
            else
            {
                var _gpsLocation = await PermissionService.CheckAndGetGpsLocationAsync();
                if (string.IsNullOrEmpty(_gpsLocation))
                    return (false, "GPS coordinates not available. Please ensure location services are enabled", _ChaceCount);
                else
                    gpsCoordinates = _gpsLocation;
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
            var _rcLinkedSites = await _scanDataDbServices.GetRCLinkedDuressClientSitesListBySiteId(LoggedInClientSiteId.Value);
            bool isTagSiteLinked = false;
            bool _scannedfromlinkedSite = false;
            if (App.TourMode == PatrolTouringMode.STND)
            {
                if (TagInfoDetails != null && TagInfoDetails.ClientSiteId != LoggedInClientSiteId)
                {
                    // Check if tag's site is linked with logged in site
                    if (_rcLinkedSites != null && _rcLinkedSites.Count > 0)
                    {
                        isTagSiteLinked = _rcLinkedSites.Any(x => x.ClientSiteId == TagInfoDetails.ClientSiteId);
                        if (!isTagSiteLinked)
                        {
                            if (_scannerType == ScanningType.NFC)
                                message = "Tag does not belong to logged in site. Please check.";
                            else if (_scannerType == ScanningType.BLUETOOTH)
                                message = "iBeacon does not belong to logged in site. Please check.";
                            return (false, message, _ChaceCount);
                        }
                        else
                        {
                            _scannedfromlinkedSite = true;
                        }
                    }
                    else
                    {
                        if (_scannerType == ScanningType.NFC)
                            message = "Tag does not belong to logged in site. Please check.";
                        else if (_scannerType == ScanningType.BLUETOOTH)
                            message = "iBeacon does not belong to logged in site. Please check.";
                        return (false, message, _ChaceCount);
                    }
                }
            }

            if (TagInfoDetails == null && _scannerType == ScanningType.BLUETOOTH)
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
                DeviceId = App.DeviceId,
                DeviceName = App.DeviceName,
                IsScanFromLinkedSite = _scannedfromlinkedSite
            };

            await _scanDataDbServices.SaveScanData(newrecord);
            _ChaceCount = _scanDataDbServices.GetCacheRecordsCount();


            if (_scannerType == ScanningType.NFC)
            {
                if (TagInfoDetails == null)
                {
                    message = $"[NFC] Unknown tag [{_TagUid}] scan record saved to cache.";
                }
                else
                {
                    message = $"[NFC] {(TagInfoDetails.LabelDescription.Length > 35 ? $"{(TagInfoDetails.LabelDescription.Substring(0, 35).Replace("\"", "").Replace("'", ""))} ..." : TagInfoDetails.LabelDescription.Replace("\"", "").Replace("'", ""))} scan record saved to cache.";
                }
            }
            else if (_scannerType == ScanningType.BLUETOOTH)
            {
                if (TagInfoDetails == null)
                {
                    message = $"[BLE] Unknown iBeacon [{_TagUid}] scan record saved to cache.";
                }
                else
                {
                    message = $"[BLE] {(TagInfoDetails.LabelDescription.Length > 35 ? $"{(TagInfoDetails.LabelDescription.Substring(0, 35).Replace("\"", "").Replace("'", ""))} ..." : TagInfoDetails.LabelDescription.Replace("\"", "").Replace("'", ""))} scan record saved to cache.";
                }
            }

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

        public async Task<ClientSiteSmartWandTagsLocal> GetTagDetailsFromLocalDbAsync(string _TagUid)
        {
            return await  _scanDataDbServices.GetSmartWandTagDetailOfTagAsync(_TagUid);
        }

        public async Task<string> GetClientSiteNameFromLocalDb(int clientSiteId)
        {
            return await _scanDataDbServices.GetClientSitesNameLocalById(clientSiteId);
        }

        public string GetClientSiteNameFromLocalDbNonAsync(int clientSiteId)
        {
            return _scanDataDbServices.GetClientSitesNameLocalByIdNonAsync(clientSiteId);
        }

        private class SiteNameResponse
        {
            public string siteName { get; set; }
        }

        public async Task<string> GetClientSiteNameAsync(int clientSiteId)
        {
            if (clientSiteId <= 0)
                return string.Empty;

            /* Local first: instant, and the only option with no signal. ClientSitesLocal is
               filled once at guard login from the IR site list, so on a patrol car tour the site
               the guard has just driven to and scanned is often not in it - which is why the
               label was falling back to the raw id. */
            try
            {
                var cached = _scanDataDbServices.GetClientSitesNameLocalByIdNonAsync(clientSiteId);
                if (!string.IsNullOrWhiteSpace(cached))
                    return cached;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Local site name lookup failed: {ex.Message}");
            }

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return string.Empty;

            try
            {
                var apiUrl = $"{AppConfig.ApiBaseUrl}GuardSecurityNumber/GetSiteName?clientsiteId={clientSiteId}";

                using var client = new HttpClient();
                var response = await client.GetAsync(apiUrl);

                // 404 is the server's "no such site"; nothing to report, just no name.
                if (!response.IsSuccessStatusCode)
                    return string.Empty;

                var result = await response.Content.ReadFromJsonAsync<SiteNameResponse>();
                return result?.siteName ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Site name lookup failed: {ex.Message}");
                return string.Empty;
            }
        }

    }
}
