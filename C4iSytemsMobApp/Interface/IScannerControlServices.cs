using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Enums;
using C4iSytemsMobApp.Models;


namespace C4iSytemsMobApp.Interface
{
    public interface IScannerControlServices
    {
        Task<List<string>?> CheckScannerOnboardedAsync(string _clientSiteId);
        Task<List<DropdownItem>?> GetClientSiteSmartWandsAsync(string _clientSiteId);
        Task<TagInfoApiResponse?> FetchTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId, ScanningType _scannerType);
        Task<TagInfoApiResponse?> SaveNFCTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId, string _tagLabel);
        Task<bool> CheckIfGuardHasTagAddAccess(string _guardId);
        Task<SmartWandDeviceRegister> CheckAndRegisterSmartWandAsync(int _selectedSmartWandId, string deviceid, string devicename, string deviceType);
        Task<List<ClientSiteSmartWandTagsLocal>> GetSmartWandTagsForSite(string siteId);
        Task<(bool isSuccess, string errorMessage, int cachecount)> SaveScanDataToLocalCache(string _TagUid, ScanningType _scannerType, int? LoggedInClientSiteId, int? LoggedInUserId, int? LoggedInGuardId);
        Task<bool> CheckIfTagExistsForSiteInLocalDb(string _TagUid);
        Task<ClientSiteSmartWandTagsLocal> GetTagDetailsFromLocalDbAsync(string _TagUid);
    }
}
