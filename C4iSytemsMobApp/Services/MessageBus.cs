
namespace C4iSytemsMobApp.Services
{
    public static class MessageBus
    {
        public static event Action<string,string> BeaconMessageReceived;

        public static void Send(string messageType,string message)
        {
            BeaconMessageReceived?.Invoke(messageType,message);
        }
    }
}
