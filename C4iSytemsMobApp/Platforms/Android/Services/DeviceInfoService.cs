using Android.OS;
using Android.Provider;
using C4iSytemsMobApp.Interface;
using C4iSytemsMobApp.Platforms.Android.Services;
using Application = Android.App.Application;


[assembly: Dependency(typeof(DeviceInfoService))]
namespace C4iSytemsMobApp.Platforms.Android.Services
{
    public class DeviceInfoService : IDeviceInfoService
    {
        public string GetDeviceName()
        {
            //return $"{Build.Manufacturer} - {Build.Model} - {Build.Device}";

            var context = Application.Context;
            string name = null;

            // Try Global first (modern)
            try
            {
                name = Settings.Global.GetString(context.ContentResolver, "device_name");
            }
            catch { }

            // Fallback to Secure if null
            if (string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    name = Settings.Secure.GetString(context.ContentResolver, "device_name");
                }
                catch { }
            }

            // Last fallback: manufacturer + model
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"{Build.Manufacturer} - {Build.Model} - {Build.Device}";
            }

            return name;
        }

        public string GetDeviceId()
        {
            var context = Application.Context;
            return Settings.Secure.GetString(context.ContentResolver, Settings.Secure.AndroidId);
        }
    }
}
