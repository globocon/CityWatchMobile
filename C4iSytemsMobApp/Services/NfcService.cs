using C4iSytemsMobApp.Interface;
using Plugin.NFC;



namespace C4iSytemsMobApp.Services
{
    // Refer url: https://prototypemakers.medium.com/start-building-with-nfc-rfid-tags-on-ios-android-using-xamarin-today-2268cf86d3b4
    // https://github.com/franckbour/Plugin.NFC
    public class NfcService : INfcService
    {       
        public bool IsSupported => CrossNFC.IsSupported;
        public bool IsAvailable => CrossNFC.Current.IsAvailable;
        public bool IsEnabled => CrossNFC.Current.IsEnabled;
        public bool IsListening { get; private set; }
        public bool IsWritingTagSupported => CrossNFC.Current.IsWritingTagSupported;

        public event EventHandler<ITagInfo> OnMessageReceived;
        public event EventHandler<ITagInfo> OnMessagePublished;
        public event EventHandler<(ITagInfo TagInfo, bool Format)> OnTagDiscovered;
        public event EventHandler<bool> OnNfcStatusChanged;
        public event EventHandler<bool> OnTagListeningStatusChanged;
        public event EventHandler OniOSReadingSessionCancelled;

        private bool _isDeviceiOS;

        public NfcService() 
        {
            _isDeviceiOS = DeviceInfo.Platform == DevicePlatform.iOS;
            CrossNFC.Legacy = false;
        }

        public async Task InitializeAsync()
        {
            if (!IsSupported || !IsAvailable)
                return;

            SubscribeToEvents();

            // Some delay to prevent Java.Lang.IllegalStateException on Android
            await Task.Delay(500);
        }

        public async Task StartListeningAsync()
        {
            if (!IsSupported || !IsAvailable || !IsEnabled)
                return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CrossNFC.Current.StartListening();
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to start listening", ex);
            }
        }

        public async Task StopListeningAsync()
        {
            if (!IsSupported)
                return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CrossNFC.Current.StopListening();
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to stop listening", ex);
            }
        }

        public async Task StartPublishingAsync(bool formatTag = false)
        {
            if (!IsSupported || !IsAvailable || !IsEnabled)
                return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CrossNFC.Current.StartPublishing(formatTag);
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to start publishing", ex);
            }
        }

        public async Task StopPublishingAsync()
        {
            if (!IsSupported)
                return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CrossNFC.Current.StopPublishing();
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to stop publishing", ex);
            }
        }

        public async Task PublishMessageAsync(ITagInfo tagInfo, bool makeReadOnly = false)
        {
            if (!IsSupported)
                return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CrossNFC.Current.PublishMessage(tagInfo, makeReadOnly);
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to publish message", ex);
            }
        }

        public async Task ClearMessageAsync(ITagInfo tagInfo)
        {
            if (!IsSupported)
                return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CrossNFC.Current.ClearMessage(tagInfo);
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to clear message", ex);
            }
        }

        public void Configure(NfcConfiguration configuration)
        {
            CrossNFC.Current.SetConfiguration(configuration);
        }

        public string ByteArrayToHexString(byte[] bytes, string separator = null)
        {
            return NFCUtils.ByteArrayToHexString(bytes, separator);
        }

        public byte[] EncodeToByteArray(string text)
        {
            return NFCUtils.EncodeToByteArray(text);
        }

        private void SubscribeToEvents()
        {
            CrossNFC.Current.OnMessageReceived += (tagInfo) => OnMessageReceived?.Invoke(this, tagInfo);
            CrossNFC.Current.OnMessagePublished += (tagInfo) => OnMessagePublished?.Invoke(this, tagInfo);
            CrossNFC.Current.OnTagDiscovered += (tagInfo, format) => OnTagDiscovered?.Invoke(this, (tagInfo, format));
            CrossNFC.Current.OnNfcStatusChanged += (isEnabled) => OnNfcStatusChanged?.Invoke(this, isEnabled);
            CrossNFC.Current.OnTagListeningStatusChanged += (isListening) =>
            {
                IsListening = isListening;
                OnTagListeningStatusChanged?.Invoke(this, isListening);
            };

            if (_isDeviceiOS)
            {
                CrossNFC.Current.OniOSReadingSessionCancelled += (sender, e) =>
                    OniOSReadingSessionCancelled?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UnsubscribeFromEvents()
        {
            // Events are automatically unsubscribed when the service is disposed
            CrossNFC.Current.OnMessageReceived -= (tagInfo) => OnMessageReceived?.Invoke(this, tagInfo);
            CrossNFC.Current.OnMessagePublished += (tagInfo) => OnMessagePublished?.Invoke(this, tagInfo);
            CrossNFC.Current.OnTagDiscovered += (tagInfo, format) => OnTagDiscovered?.Invoke(this, (tagInfo, format));
            CrossNFC.Current.OnNfcStatusChanged -= (isEnabled) => OnNfcStatusChanged?.Invoke(this, isEnabled);
            CrossNFC.Current.OnTagListeningStatusChanged -= (isListening) =>
            {
                IsListening = isListening;
                OnTagListeningStatusChanged?.Invoke(this, isListening);
            };

            if (_isDeviceiOS)
                CrossNFC.Current.OniOSReadingSessionCancelled -= (sender, e) =>
                    OniOSReadingSessionCancelled?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            UnsubscribeFromEvents();
        }
    }
}
