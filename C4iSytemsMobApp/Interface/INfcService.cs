
namespace C4iSytemsMobApp.Interface
{
    public interface INfcService
    {
        bool IsNfcAvailable { get; }
        bool IsNfcEnabled { get; }
        void StartListening(Action<string> onTagRead);
        void StopListening();
    }
}
