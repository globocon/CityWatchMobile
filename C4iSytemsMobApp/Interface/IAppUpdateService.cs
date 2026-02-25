
namespace C4iSytemsMobApp.Interface
{
    public interface IAppUpdateService
    {
        Task<bool> CheckForUpdateAsync();
        Task<bool> CheckForUpdateInBackgroundAsync();
    }
}
