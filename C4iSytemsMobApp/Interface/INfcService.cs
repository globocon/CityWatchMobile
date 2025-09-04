
using Plugin.NFC;

namespace C4iSytemsMobApp.Interface
{
    public interface INfcService
    {
        bool IsSupported { get; }
        bool IsAvailable { get; }
        bool IsEnabled { get; }
        bool IsListening { get; }
        bool IsWritingTagSupported { get; }

        event EventHandler<ITagInfo> OnMessageReceived;
        event EventHandler<ITagInfo> OnMessagePublished;
        event EventHandler<(ITagInfo TagInfo, bool Format)> OnTagDiscovered;
        event EventHandler<bool> OnNfcStatusChanged;
        event EventHandler<bool> OnTagListeningStatusChanged;
        event EventHandler OniOSReadingSessionCancelled;

        Task InitializeAsync(); // Add this method
        Task StartListeningAsync();
        Task StopListeningAsync();
        Task StartPublishingAsync(bool formatTag = false);
        Task StopPublishingAsync();
        Task PublishMessageAsync(ITagInfo tagInfo, bool makeReadOnly = false);
        Task ClearMessageAsync(ITagInfo tagInfo);
        void Dispose();
        void Configure(NfcConfiguration configuration);
        string ByteArrayToHexString(byte[] bytes, string separator = null);
        byte[] EncodeToByteArray(string text);
    }
}
