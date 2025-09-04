
using C4iSytemsMobApp.Models;

namespace C4iSytemsMobApp.Interface
{
    public interface IScannerControlServices
    {
        Task<List<string>?> CheckScannerOnboardedAsync(string _clientSiteId);
        Task<TagInfoApiResponse?> FetchTagInfoDetailsAsync(string _clientSiteId, string _tagUid, string _guardId, string _userId);
    }
}
