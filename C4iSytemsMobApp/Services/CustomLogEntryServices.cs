using AutoMapper;
using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Interface;

namespace C4iSytemsMobApp.Services
{
    public class CustomLogEntryServices : ICustomLogEntryServices
    {
        private readonly ILogBookServices _logBookServices;
        private readonly IMapper _mapper;
        private readonly IScanDataDbServices _scanDataDbService;
        private IDeviceInfoService infoService;
        private string devicename;
        private string deviceid;
        int guardId;
        int clientSiteId;
        int userId;
        string msg;
        bool isError;

        public CustomLogEntryServices()
        {
            _logBookServices = IPlatformApplication.Current.Services.GetService<ILogBookServices>();
            _mapper = IPlatformApplication.Current.Services.GetService<IMapper>();
            _scanDataDbService = IPlatformApplication.Current.Services.GetService<IScanDataDbServices>();
            infoService = IPlatformApplication.Current.Services.GetService<IDeviceInfoService>();
            devicename = infoService?.GetDeviceName();
            deviceid = infoService?.GetDeviceId();
            GetSecureStorageValues();
        }


        public async Task ProcessCustomFieldLogsOnlineDataToCache(List<Dictionary<string, string?>> rows)
        {
            if (rows == null || rows.Count == 0)
                return;

            var headList = new List<CustomFieldLogHeadCache>();

            foreach (var dict in rows)
            {
                // Rename timeSlot → Time Slot
                if (dict.ContainsKey("timeSlot"))
                {
                    var value = dict["timeSlot"];
                    dict.Remove("timeSlot");
                    dict["Time Slot"] = value;
                }

                // Create head
                var head = new CustomFieldLogHeadCache
                {
                    SiteId = clientSiteId,
                    KeyValuePairs = new List<CustomFieldLogDetailCache>()
                };

                // Create details from dictionary
                foreach (var kvp in dict)
                {
                    head.KeyValuePairs.Add(new CustomFieldLogDetailCache
                    {
                        DictKey = kvp.Key,
                        DictValue = kvp.Value
                    });
                }

                headList.Add(head);
            }

            // Insert in one batch
            await _scanDataDbService.RefreshCustomFieldCacheList(headList);

        }

        public async Task<List<Dictionary<string, string?>>> GetCustomFieldLogsFromCache()
        {
            var heads = await _scanDataDbService.GetCustomFieldLogCacheList();

            var result = new List<Dictionary<string, string?>>();

            foreach (var head in heads)
            {
                var dict = new Dictionary<string, string?>();

                foreach (var detail in head.KeyValuePairs)
                {
                    // Avoid duplicate keys crashing Dictionary
                    if (!dict.ContainsKey(detail.DictKey))
                        dict.Add(detail.DictKey, detail.DictValue);
                }

                result.Add(dict);
            }

            return result;
        }

        public async Task<bool> SaveCustomFieldLogRequestHeadDataToCache(Dictionary<string, string?> toupdate)
        {
            bool r = false;
            string? value = "";

            Dictionary<string, string?> copyoftoupdate = new ();

            foreach (var kvp in toupdate) {
                copyoftoupdate.Add(kvp.Key,kvp.Value);
            }

            if (toupdate.ContainsKey("Time Slot"))
            {
                value = toupdate["Time Slot"];
                toupdate.Remove("Time Slot");
                toupdate["timeSlot"] = value;
            }

            List<CustomFieldLogRequestDetailCache> CustomFieldLogRequestDetailCacheList = new List<CustomFieldLogRequestDetailCache>();
            foreach (var d in toupdate)
            {
                CustomFieldLogRequestDetailCacheList.Add(new CustomFieldLogRequestDetailCache()
                {
                    DictKey = d.Key,
                    DictValue = d.Value
                });
            }

            CustomFieldLogRequestHeadCache customFieldLogRequestHeadCache = new CustomFieldLogRequestHeadCache()
            {
                SiteId = clientSiteId,
                Details = CustomFieldLogRequestDetailCacheList,
                EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
                EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
                EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
                EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
                EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute(),
                IsSynced = false,
                UniqueRecordId = Guid.NewGuid(),
                DeviceId = deviceid,
                DeviceName = devicename
            };

            //Save request to cache for online syncing
            r = await _scanDataDbService.SaveCustomFieldLogRequestCacheData(customFieldLogRequestHeadCache);



            // Update record in local cache db
            var existingRecord = await _scanDataDbService.GetCustomFieldLogCacheListByKeyValue("Time Slot", value);
            if (existingRecord != null) {
                foreach (var record in existingRecord.KeyValuePairs) {
                    record.DictValue = copyoftoupdate[record.DictKey];
                }
                r = await _scanDataDbService.UpdateCustomFieldLogCacheList(existingRecord);
            }

            return r;
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
