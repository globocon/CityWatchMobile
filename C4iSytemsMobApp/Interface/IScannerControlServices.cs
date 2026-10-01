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

        /// <summary>
        /// Registers a new tag of either kind. The NFC-only overload above stays for the add-NFC
        /// page; this one exists because a beacon has to be sent as tag type "Bluetooth".
        /// </summary>
        Task<TagInfoApiResponse?> SaveTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _tagLabel, ScanningType _scannerType);

        /// <summary>
        /// Reads a tag by UID so its description can be edited. Does not log a scan hit, unlike
        /// the patrol lookup - an admin opening a tag has not patrolled anything.
        /// </summary>
        Task<TagEditInfo?> GetTagForEditAsync(string _tagUid, ScanningType _scannerType);

        /// <summary>
        /// The logged-in guard's name, security number and initials, read from Guards, for
        /// naming them in a logbook entry the app composes.
        /// </summary>
        Task<GuardNameDetails?> GetGuardNameDetailsAsync();

        /// <summary>
        /// Saves a new description against an existing tag. Everything except the description is
        /// passed back unchanged, so this cannot silently re-home a tag to another site.
        /// </summary>
        Task<TagInfoApiResponse?> UpdateTagDescriptionAsync(TagEditInfo _tag, string _newDescription);
        Task<bool> CheckIfGuardHasTagAddAccess(string _guardId);
        Task<SmartWandDeviceRegister> CheckAndRegisterSmartWandAsync(int _selectedSmartWandId, string deviceid, string devicename, string deviceType);
        Task<List<ClientSiteSmartWandTagsLocal>> GetSmartWandTagsForSite(string siteId);
        Task<(bool isSuccess, string errorMessage, int cachecount)> SaveScanDataToLocalCache(string _TagUid, ScanningType _scannerType, int? LoggedInClientSiteId, int? LoggedInUserId, int? LoggedInGuardId);
        Task<bool> CheckIfTagExistsForSiteInLocalDb(string _TagUid);
        Task<ClientSiteSmartWandTagsLocal> GetTagDetailsFromLocalDbAsync(string _TagUid);
        Task<string> GetClientSiteNameFromLocalDb(int clientSiteId);
        public string GetClientSiteNameFromLocalDbNonAsync(int clientSiteId);

        /// <summary>
        /// A site's name, from the local cache when it is there and from the server when it is
        /// not. The local cache only holds the sites downloaded at guard login, which need not
        /// include a site a patrol car has driven to since.
        /// </summary>
        Task<string> GetClientSiteNameAsync(int clientSiteId);
        public Task<int> GetSmartWandByDeviceIdAsync();
    }
}
