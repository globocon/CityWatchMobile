using C4iSytemsMobApp.Models;


namespace C4iSytemsMobApp.Interface
{
    public interface IScannerControlServices
    {
        Task<List<string>?> CheckScannerOnboardedAsync(string _clientSiteId);
        Task<List<DropdownItem>?> GetClientSiteSmartWandsAsync(string _clientSiteId);
        Task<TagInfoApiResponse?> FetchTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId);
        Task<TagInfoApiResponse?> SaveNFCTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId, string _tagLabel);
        Task<bool> CheckIfGuardHasTagAddAccess(string _guardId);
        Task<SmartWandDeviceRegister> CheckAndRegisterSmartWandAsync(int _selectedSmartWandId, string deviceid, string devicename, string deviceType);
    }
}
