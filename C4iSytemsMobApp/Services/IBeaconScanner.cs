#if ANDROID
using Android.Opengl;
using Android.Webkit;
#endif
using System.Text;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace C4iSytemsMobApp.Services
{
    public class IBeaconScanner
    {
        private readonly IBluetoothLE? _ble;
        private readonly IAdapter? _adapter;
        private bool _runScanLoop = false;
        private bool _isRestarting = false;

        // public int RssiThreshold { get; set; } = -80; // default: ignore weaker than -80
        private List<DeviceFound> _deviceFound;
        public event Action<BluetoothState>? OnStateChanged;
        public Func<DeviceFound, Task>? OnDeviceDiscoveredAsync;
        public Func<List<DeviceFound>, Task>? OnDeviceFoundAsync;
        public event Action<bool>? OnScanningInProgress;
        public bool IsBluetoothSupported { get; private set; }
        //public bool IsBluetoothSupported => _ble != null;


        public IBeaconScanner()
        {
            try
            {
                IsBluetoothSupported = false;

#if WINDOWS
    // Windows Server / VM → Bluetooth not supported
    if (DeviceInfo.Platform == DevicePlatform.WinUI)
        return;
#endif

                // This line can throw TypeInitializationException internally
                _ble = CrossBluetoothLE.Current;

                if (_ble == null)
                {
                    IsBluetoothSupported = false;
                    return;
                }

                _adapter = _ble.Adapter;

                if (_adapter == null)
                {
                    IsBluetoothSupported = false;
                    return;
                }

                IsBluetoothSupported = true;

                _ble.StateChanged += BleStateChanged;
                _adapter.DeviceDiscovered += OnDeviceDiscovered;
                _adapter.DeviceAdvertised += OnDeviceAdvertised;
                _adapter.ScanMode = ScanMode.LowLatency;
            }
            catch (TypeInitializationException)
            {
                // 🔑 This is the real failure mode when no adapter exists
                IsBluetoothSupported = false;
                _ble = null;
                _adapter = null;
            }
            catch (Exception)
            {
                // Defensive fallback
                IsBluetoothSupported = false;
                _ble = null;
                _adapter = null;
            }
        }


        public async Task StartScanningAsync()
        {
            //if (CrossBluetoothLE.Current.State == BluetoothState.Off)
            //{
            //    //Console.WriteLine("Bluetooth is OFF.");
            //    MessageBus.Send("INFO", "Bluetooth is OFF.");
            //    return;
            //}

            //MessageBus.Send("INFO", $"Scanning for iBeacon devices. Please wait...");
            //_deviceFound = new List<DeviceFound>();
            //await _adapter.StartScanningForDevicesAsync();
            //MessageBus.Send("INFO", $"iBeacon Scanning Completed. Processing scanned data.");
            //await ProcessDeviceFound(_deviceFound);
        }

        private async void OnDeviceDiscovered(object sender, DeviceEventArgs e)
        {
            var device = e.Device;
            try
            {
                // On iOS, device.Id is a random UUID. Attempt to find the real MAC in advertisement data first.
                var realMac = TryGetMacFromAdvertisement(device);
                var FakeMac = GuidToMac(device.Id.ToString());
                var Macid = realMac ?? FakeMac;
                var deviceName = device.Name ?? "Unknown";

                // if (deviceName.ToLower().Contains("inode") || deviceName.ToLower().Contains("beat") || deviceName.ToLower().Contains("jodu"))
                // {
                //     Console.WriteLine($"[BLE] Discovered: {deviceName} ID: {device.Id} MAC_Derived: {Macid} {(realMac != null ? "(Real MAC)" : "(Derived from UUID)")}");
                // }

                Console.WriteLine($"[BLE] Discovered: {deviceName} ID: {device.Id} MAC_Derived: {Macid} {(realMac != null ? "(Real MAC)" : "(Derived from UUID)")}");

                var devFound = new DeviceFound
                {
                    MacID = Macid,
                    DeviceName = device.Name
                };

                if (!_deviceFound.Any(d => d.MacID == Macid))
                {
                    _deviceFound.Add(devFound);
                }

                // if (OnDeviceDiscoveredAsync != null)
                // {
                //     await OnDeviceDiscoveredAsync.Invoke(devFound);
                // }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BLE] Error in OnDeviceDiscovered: {ex.Message}");
            }
        }

        private async void OnDeviceAdvertised(object sender, DeviceEventArgs e)
        {
            //var device = e.Device;
            //try
            //{
            //    if (device.Name?.ToLower().StartsWith("inode") == true)
            //    {
            //        MessageBus.Send("INFO", $"Device Advertised \nName: {device.Name}\nMAC:{GuidToMac(device.Id.ToString())}\n------------------------------");
            //        MessageBus.Send("DATA", $"{GuidToMac(device.Id.ToString())}-{device.Name}");
            //    }

            //}
            //catch (Exception ex)
            //{
            //    MessageBus.Send("INFO", $"Guid --> MAC Error:\n{ex.Message}");
            //}
        }

        public void Start()
        {
            if (CrossBluetoothLE.Current.State == BluetoothState.Off)
            {
                // MessageBus.Send("INFO", "Bluetooth is OFF.");
                return;
            }

            if (_runScanLoop) return;

            _runScanLoop = true;
            Task.Run(async () => await ScanLoop());
        }

        public async Task Stop()
        {
            _runScanLoop = false;

            if (_adapter != null && _adapter.IsScanning)
            {
                await _adapter.StopScanningForDevicesAsync();
            }
            OnScanningInProgress?.Invoke(false);
        }


        private async Task ScanLoop()
        {
            while (_runScanLoop)
            {
                if (_adapter != null && !_adapter.IsScanning)
                {
                    try
                    {
                        _deviceFound = new List<DeviceFound>();
                        OnScanningInProgress?.Invoke(true);

                        // Use a 4-second timeout for the scan session to keep it looping and fresh
                        var scanTask = _adapter.StartScanningForDevicesAsync(allowDuplicatesKey: true);
                        var timeoutTask = Task.Delay(4000);

                        await Task.WhenAny(scanTask, timeoutTask);

                        if (!scanTask.IsCompleted)
                        {
                            await _adapter.StopScanningForDevicesAsync();
                        }

                        OnScanningInProgress?.Invoke(false);

                        if (OnDeviceFoundAsync != null && _deviceFound.Count > 0)
                        {
                            await OnDeviceFoundAsync.Invoke(_deviceFound);
                            await Task.Delay(200);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BLE] ScanLoop Error: {ex.Message}");
                        OnScanningInProgress?.Invoke(false);
                        await Task.Delay(2000);
                    }
                }

                await Task.Delay(200);
            }
        }

        private async void RestartScanIfNeeded()
        {
            if (!_runScanLoop || _isRestarting)
                return;

            _isRestarting = true;
            await Task.Delay(500);

            try
            {
                if (_runScanLoop && _adapter != null)
                {
                    _deviceFound = new List<DeviceFound>();
                    OnScanningInProgress?.Invoke(true);
                    await _adapter.StartScanningForDevicesAsync();
                    OnScanningInProgress?.Invoke(false);
                    if (OnDeviceFoundAsync != null)
                        await OnDeviceFoundAsync.Invoke(_deviceFound);
                    await Task.Delay(200);
                }
            }
            catch
            {
                // ignored
            }

            _isRestarting = false;
        }

        private async Task ProcessDeviceFound(List<DeviceFound> deviceFound)
        {
            foreach (var d in deviceFound)
            {
                MessageBus.Send("DATA", $"{d.MacID}-{d.DeviceName}");
                await Task.Delay(300); // wait 300 ms before sending next message.
            }
        }

        /// <summary>
        /// Attempts to extract the hardware MAC address from Manufacturer Specific Data advertisement records.
        /// This is the most reliable way to get a consistent MAC address on iOS.
        /// </summary>
        private string? TryGetMacFromAdvertisement(IDevice device)
        {
            if (device.AdvertisementRecords == null) return null;

            foreach (var record in device.AdvertisementRecords)
            {
                // Many beacons use Manufacturer Specific Data (0xFF) to broadcast their MAC
                // Some "inode" beacons have the MAC at the start of the manufacturer data
                if (record.Type == AdvertisementRecordType.ManufacturerSpecificData && record.Data != null && record.Data.Length >= 6)
                {
                    // Heuristic: Look for a 6-byte sequence at common positions
                    // Most commonly at index 0 or index 2 depending on the beacon type
                    // We'll take the first 6 bytes if it's a plausible MAC address format
                    var macBytes = new byte[6];
                    Array.Copy(record.Data, 0, macBytes, 0, 6);

                    // Format as standard MAC address
                    return string.Join(":", macBytes.Select(b => b.ToString("X2")));
                }
            }
            return null;
        }

        /// <summary>
        /// Converts a GUID/UUID to a MAC-formatted string.
        /// NOTE: On iOS, this will result in a virtual/fake MAC address because the UUID is random.
        /// </summary>
        public static string GuidToMac(string guidString)
        {
            // Parse GUID
            Guid guid = Guid.Parse(guidString);

            // Get last segment (4th element in 5-segment format)
            string lastSegment = guid.ToString().Split('-')[4];

            // Ensure 12 hex characters
            if (lastSegment.Length != 12)
                throw new Exception("Last segment is not 12 hex characters.");

            // Convert to MAC with colons
            string mac = string.Join(":", Enumerable
                .Range(0, lastSegment.Length / 2)
                .Select(i => lastSegment.Substring(i * 2, 2).ToUpper()));

            return mac;
        }

        public async Task StopScanningAsync()
        {
            //await _adapter.StopScanningForDevicesAsync();
            //MessageBus.Send("INFO", $"^^^^^^^^^^^^^^^^^^^^^^^^^^^\niBeacon Scanning Stopped....\n^^^^^^^^^^^^^^^^^^^^^^^^^^^");
        }

        private void BleStateChanged(object sender, Plugin.BLE.Abstractions.EventArgs.BluetoothStateChangedArgs e)
        {
            Console.WriteLine($"Bluetooth state changed: {e.NewState}");

            if (e.NewState != BluetoothState.On)
            {
                // If Bluetooth turned off, stop scanning
                _ = Stop();
            }

            // Notify UI or logic
            OnStateChanged?.Invoke(e.NewState);
        }

        public BluetoothState GetCurrentState()
        {
            if (_ble == null) return BluetoothState.Unknown;
            return _ble.State;
        }

    }

    public class DeviceFound
    {
        public string MacID { get; set; }
        public string DeviceName { get; set; }
    }


}
