using C4iSytemsMobApp.Interface;
using Plugin.NFC;


namespace C4iSytemsMobApp.Services
{
    public class NfcService : INfcService
    {
        private Action<string>? _onTagRead;

        public bool IsNfcAvailable => CrossNFC.IsSupported && CrossNFC.Current.IsAvailable;
        public bool IsNfcEnabled => CrossNFC.IsSupported && CrossNFC.Current.IsEnabled;

        public void StartListening(Action<string> onTagRead)
        {
            _onTagRead = onTagRead;
            CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
            CrossNFC.Current.StartListening();
        }

        public void StopListening()
        {
            CrossNFC.Current.OnMessageReceived -= Current_OnMessageReceived;
            CrossNFC.Current.StopListening();
            _onTagRead = null;
        }

        private void Current_OnMessageReceived(ITagInfo tagInfo)
        {
            if (tagInfo == null || !tagInfo.IsSupported)
                return;

            // Customized serial number
            //var identifier = tagInfo.Identifier;
            //var serialNumber = NFCUtils.ByteArrayToHexString(identifier, ":");
            //var title = !string.IsNullOrWhiteSpace(serialNumber) ? $"Tag [{serialNumber}]" : "Tag Info";

            //var tagId = tagInfo.SerialNumber ?? string.Empty;
            var tagId = tagInfo.ToString() ?? string.Empty;
            _onTagRead?.Invoke(tagId);
        }
    }
}
