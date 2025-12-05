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
        private readonly IAdapter _adapter;
        private List<DeviceFound> _deviceFound;

        public IBeaconScanner()
        {
            _adapter = CrossBluetoothLE.Current.Adapter;
            _adapter.ScanTimeout = 10000;
            _adapter.ScanMode = ScanMode.Balanced;
            //var scanFilterOptions = new ScanFilterOptions();
            //scanFilterOptions.ServiceUuids = new[] { guid1, guid2, etc }; // cross platform filter
            //scanFilterOptions.ManufacturerDataFilters = new[] { new ManufacturerDataFilter(1), new ManufacturerDataFilter(2) }; // android only filter
            //scanFilterOptions.DeviceAddresses = new[] { "80:6F:B0:43:8D:3B", "80:6F:B0:25:C3:15" }; // android only filter

            //_adapter.DeviceDiscovered += (s, a) => deviceList.Add(a.Device);
            _adapter.DeviceDiscovered += OnDeviceDiscovered;
            _adapter.DeviceAdvertised += OnDeviceAdvertised;
        }

        public async Task StartScanningAsync()
        {
            if (CrossBluetoothLE.Current.State == BluetoothState.Off)
            {
                //Console.WriteLine("Bluetooth is OFF.");
                MessageBus.Send("INFO","Bluetooth is OFF.");
                return;
            }

            MessageBus.Send("INFO", $"Scanning for iBeacon devices. Please wait...");
            _deviceFound = new List<DeviceFound>();
            await _adapter.StartScanningForDevicesAsync();            
            MessageBus.Send("INFO", $"iBeacon Scanning Completed. Processing scanned data.");
            await ProcessDeviceFound(_deviceFound);
        }

        private async void OnDeviceDiscovered(object sender, DeviceEventArgs e)
        {
            var device = e.Device;
            try
            {
                //if (device.Name?.ToLower().StartsWith("inode") == true)
                //{
                    //MessageBus.Send("INFO", $"Device Discovered \nName: {device.Name}\nMAC:{GuidToMac(device.Id.ToString())}\n------------------------------");
                    var Macid = GuidToMac(device.Id.ToString());
                   // MessageBus.Send("DATA", $"{Macid}-{device.Name}");                   

                    if (!_deviceFound.Any(d => d.MacID == Macid))
                    {
                        _deviceFound.Add(new DeviceFound
                        {
                            MacID = Macid,
                            DeviceName = device.Name
                        });
                    }
                //}

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

        private async Task ProcessDeviceFound(List<DeviceFound> deviceFound)
        {
            foreach(var d in deviceFound) 
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
            await _adapter.StopScanningForDevicesAsync();
            MessageBus.Send("INFO", $"^^^^^^^^^^^^^^^^^^^^^^^^^^^\niBeacon Scanning Stopped....\n^^^^^^^^^^^^^^^^^^^^^^^^^^^");
        }
    }

    public class DeviceFound
    {
        public string MacID { get; set; }
        public string DeviceName { get; set; }
    }


}
