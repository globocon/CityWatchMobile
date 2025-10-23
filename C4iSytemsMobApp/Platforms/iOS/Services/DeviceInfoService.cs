using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Platforms.iOS.Services;
using UIKit;

[assembly: Dependency(typeof(DeviceInfoService))]
namespace C4iSytemsMobApp.Platforms.iOS.Services
{
    public class DeviceInfoService : IDeviceInfoService
    {
        public string GetDeviceName()
        {
            return UIDevice.CurrentDevice.Name; // e.g. "John’s iPhone"
        }

        public string GetDeviceId()
        {
            // Legal, public identifier (NOT UDID)
            return UIDevice.CurrentDevice.IdentifierForVendor?.AsString();
        }
    }
}
