#if ANDROID
using Android.Opengl;
using Android.Webkit;
#endif
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Text;

namespace C4iSytemsMobApp.Services
{
    public class IBeaconScanner
    {
        private readonly IBluetoothLE _ble;
        private readonly IAdapter _adapter;
        private bool _runScanLoop = false;
        private bool _isRestarting = false;
                
        // public int RssiThreshold { get; set; } = -80; // default: ignore weaker than -80
        private List<DeviceFound> _deviceFound;
        public event Action<BluetoothState> OnStateChanged;
        public Func<List<DeviceFound>, Task>? OnDeviceFoundAsync;
        public event Action<bool>? OnScanningInProgress;

        public IBeaconScanner()
        {
            _ble = CrossBluetoothLE.Current;
            _ble.StateChanged += BleStateChanged;
            _adapter = _ble.Adapter;
            //_adapter.ScanTimeout = 10000;
            _adapter.ScanMode = ScanMode.LowPower;           
            _adapter.DeviceDiscovered += OnDeviceDiscovered;
            _adapter.DeviceAdvertised += OnDeviceAdvertised;
            // _adapter.ScanTimeoutElapsed += (s, e) => RestartScanIfNeeded();
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
            // Ignore weak RSSI
            //if (e.Device.Rssi < RssiThreshold)
            //    return;

            var device = e.Device;
            try
            {
                var Macid = GuidToMac(device.Id.ToString());
                if (!_deviceFound.Any(d => d.MacID == Macid))
                {
                    _deviceFound.Add(new DeviceFound
                    {
                        MacID = Macid,
                        DeviceName = device.Name
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBus.Send("INFO", $"Guid --> MAC Error:\n{ex.Message}");
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

            if (_adapter.IsScanning)
            {
                await _adapter.StopScanningForDevicesAsync();
            }
            OnScanningInProgress?.Invoke(false);
        }


        private async Task ScanLoop()
        {
            while (_runScanLoop)
            {
                if (!_adapter.IsScanning)
                {
                    try
                    {
                        _deviceFound = new List<DeviceFound>();
                        OnScanningInProgress?.Invoke(true);
                        await _adapter.StartScanningForDevicesAsync();
                        OnScanningInProgress?.Invoke(false);
                        if (OnDeviceFoundAsync != null)
                        {
                            if (_deviceFound.Count > 0)
                            {
                                await OnDeviceFoundAsync.Invoke(_deviceFound);
                                await Task.Delay(200);
                            }
                        }
                    }
                    catch
                    {
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
                if (_runScanLoop)
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
            return _ble.State;
        }

    }

    public class DeviceFound
    {
        public string MacID { get; set; }
        public string DeviceName { get; set; }
    }


}
