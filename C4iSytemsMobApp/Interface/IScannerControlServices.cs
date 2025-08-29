
namespace C4iSytemsMobApp.Interface
{
    public interface IScannerControlServices
    {
        Task<List<string>?> CheckScannerOnboardedAsync(string _clientSiteId);
    }
}
